using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Globalization;
using ZXMAK2.Hardware;
using ZXMAK2.Host.Entities;
using ZXMAK2.Host.Interfaces;
using ZXMAK2.Host.WinForms.Mdx;

// Read-only component audit: compile against copied published v97 binaries.
// No actual DirectSound device, media, register writes or guest modification.
internal static class MixerAuditProbe
{
    private const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    private static short L(uint v) { return unchecked((short)v); }
    private static short R(uint v) { return unchecked((short)(v >> 16)); }
    private static uint P(short l, short r) { return (uint)(ushort)l | ((uint)(ushort)r << 16); }
    private sealed class Meter
    {
        internal double Sum, Square, Sin, Cos; internal int Peak, RightPeak; internal long Count;
        internal void Add(uint p, long n, int fs, double hz)
        {
            double l = L(p); Sum += l; Square += l*l; Count++;
            Peak = Math.Max(Peak, Math.Abs((int)l)); RightPeak = Math.Max(RightPeak, Math.Abs((int)R(p)));
            double a = 2 * Math.PI * hz * n / fs; Sin += l*Math.Sin(a); Cos += l*Math.Cos(a);
        }
        internal double Fundamental { get { return 2*Math.Sqrt(Sin*Sin+Cos*Cos)/Count; } }
        internal double Rms { get { return Math.Sqrt(Square/Count); } }
        internal double Residual { get { return Math.Sqrt(Math.Max(0, Square/Count - Math.Pow(Sum/Count,2) - Fundamental*Fundamental/2)); } }
    }
    private sealed class Dac : SoundDeviceBase
    {
        private static readonly MethodInfo Timing = typeof(SoundDeviceBase).GetMethod("ApplyTimings", F);
        private static readonly FieldInfo Last = typeof(SoundDeviceBase).GetField("m_lastDacTact", F);
        internal Dac(int rate) { SampleRate = rate; Timing.Invoke(this, new object[] {3500000,rate}); }
        internal void Begin() { Last.SetValue(this,-1); base.OnBeginFrame(); }
        internal void Value(int i, short l, short r) { UpdateDac((double)i/AudioBuffer.Length,l,r); }
        internal void End() { base.OnEndFrame(); }
    }
    // Prototype of opt-in publication. No production class or native generation
    // is changed: staging prevents BeginFrame from damaging last completed PCM.
    private sealed class PcmStaging : SoundDeviceBase
    {
        private readonly uint[] pending;
        internal PcmStaging(int rate) {
            SampleRate=rate;
            typeof(SoundDeviceBase).GetMethod("ApplyTimings",F).Invoke(this,new object[]{3500000,rate});
            pending=new uint[AudioBuffer.Length];
        }
        internal void Begin() { Array.Clear(pending,0,pending.Length); }
        internal void Value(int index,short l,short r) { pending[index]=P(l,r); }
        internal void End() { Array.Copy(pending,AudioBuffer,pending.Length); }
    }
    private static void FrequencyResponse()
    {
        Console.WriteLine("kind,rate,hz,gain_db,peak,residual_rms,dc");
        foreach (int fs in new[] {22050,44100,48000,96000})
        foreach (int hz in new[] {20,50,100,1000,5000,10000,15000,20000})
        {
            if(hz >= fs/2) continue;
            var src = new uint[fs/50];
            // 22050 is divisible by 50; zero right channel tests isolation.
            var mix = new FrameSound(fs,new[]{src},false,100,null,true);
            var dac = new Dac(fs); var mm = new Meter(); var dm = new Meter();
            for (int f=0; f<100; f++)
            {
                dac.Begin();
                for(int i=0;i<src.Length;i++) {
                    short s=(short)Math.Round(12000*Math.Sin(2*Math.PI*hz*(f*(long)src.Length+i)/fs));
                    src[i]=P(s,0); dac.Value(i,s,0);
                }
                dac.End(); mix.Refresh(); var pcm=mix.GetBuffer();
                if(f>=50) for(int i=0;i<src.Length;i++) {
                    long n=f*(long)src.Length+i; mm.Add(pcm[i],n,fs,hz); dm.Add(dac.AudioBuffer[i],n,fs,hz);
                }
            }
            Check(mm.RightPeak==0 && dm.RightPeak==0,"right channel leak");
            Console.WriteLine("modern,{0},{1},{2:F6},{3},{4:F6},{5:F6}",fs,hz,20*Math.Log10(mm.Fundamental/6000),mm.Peak,mm.Residual,mm.Sum/mm.Count);
            Console.WriteLine("pcm_dac,{0},{1},{2:F6},{3},{4:F6},{5:F6}",fs,hz,20*Math.Log10(dm.Fundamental/12000),dm.Peak,dm.Residual,dm.Sum/dm.Count);
        }
    }
    private static void IndependenceAndLimiter()
    {
        int fs=44100,n=fs/50; var signal=new uint[n];
        for(int i=0;i<n;i++) signal[i]=P((short)(12000*Math.Sin(i*.31)),(short)(10000*Math.Cos(i*.17)));
        var expected=new FrameSound(fs,new[]{signal},false,100,null,true).GetBuffer();
        foreach(int count in new[]{2,4,8,16,64}) {
            var ss=new uint[count][]; ss[0]=signal;
            for(int j=1;j<count;j++) ss[j]=Enumerable.Repeat(P(short.MinValue,short.MinValue),n).ToArray();
            var actual=new FrameSound(fs,ss,false,100,null,true).GetBuffer();
            Check(expected.SequenceEqual(actual),"idle source affects output");
        }
        Console.WriteLine("idle_source_independence=PASS counts2,4,8,16,64 including PSG -32768 DC");
        foreach(int master in new[]{0,50,100,150,200}) {
            var m=new FrameSound(fs,new[]{signal},false,master,null,true); var metric=new Meter();
            for(int f=0;f<100;f++) {
                for(int i=0;i<n;i++) signal[i]=P((short)Math.Round(30000*Math.Sin(2*Math.PI*1000*(f*(long)n+i)/fs)),0);
                m.Refresh(); var pcm=m.GetBuffer();
                if(f>=50) for(int i=0;i<n;i++) metric.Add(pcm[i],f*(long)n+i,fs,1000);
            }
            Console.WriteLine("master,{0},1000hz_input30000,out_peak={1},fundamental={2:F4},residual_rms={3:F4}",master,metric.Peak,metric.Fundamental,metric.Residual);
        }
    }
    private static unsafe void Backend()
    {
        var ds=(DirectSound)FormatterServices.GetUninitializedObject(typeof(DirectSound));
        var free=new ConcurrentQueue<uint[]>(); var play=new ConcurrentQueue<uint[]>();
        for(int i=0;i<4;i++) free.Enqueue(new uint[882]);
        var ev=new AutoResetEvent(false);
        Action<string,object> set=(name,value)=>typeof(DirectSound).GetField(name,F).SetValue(ds,value);
        set("_fillQueue",free); set("_playQueue",play); set("_frameEvent",ev);
        set("_zeroValue",0U); set("_isAvailable",true); set("_bufferCount",4);
        var source=new uint[882]; for(int i=0;i<source.Length;i++) source[i]=P((short)(i*23-10000),(short)(10000-i*7));
        var frame=new FrameSound(44100,new[]{source}); var expected=frame.GetBuffer(); var output=new uint[882];
        var info=new FrameInfo(null,0,0,44100,false,"");
        var request=typeof(DirectSound).GetMethod("OnBufferRequest",F);
        ds.PushFrame(info,frame);
        fixed(uint* ptr=output) request.Invoke(ds,new object[]{Pointer.Box(ptr,typeof(uint*)),output.Length});
        Check(expected.SequenceEqual(output),"DirectSound copy not bit exact");
        fixed(uint* ptr=output) request.Invoke(ds,new object[]{Pointer.Box(ptr,typeof(uint*)),output.Length});
        Check(output[output.Length-1]==0,"underrun not silence");
        for(int i=0;i<20;i++) ds.PushFrame(info,frame);
        Check(play.Count==4 && free.Count==0,"unbounded pool");
        Console.WriteLine("backend_copy=PASS bit_exact882; underrun_ramp=PASS; bounded_pool4=PASS; no_native_endpoint_created");
        ev.Dispose(); GC.SuppressFinalize(ds);
    }
    private static void DropHistory()
    {
        var input=new uint[882];
        var normal=new FrameSound(44100,new[]{input},false,100,null,true);
        var skipped=new FrameSound(44100,new[]{input},false,100,null,true);
        normal.GetBuffer(); skipped.GetBuffer();
        // One second of host-unavailable frames: bus Refresh occurs but backend
        // does not call GetBuffer; chip PCM continues and settles to a new DC.
        for(int f=0;f<50;f++) {
            for(int i=0;i<input.Length;i++) input[i]=P(-20000,-20000);
            normal.Refresh(); normal.GetBuffer(); skipped.Refresh();
        }
        normal.Refresh(); skipped.Refresh();
        int a=L(normal.GetBuffer()[0]),b=L(skipped.GetBuffer()[0]);
        Console.WriteLine("unavailable_recovery_component,continuous_first={0},lazy_first={1}; this is a stale DC history transient, not live device evidence",a,b);
        Check(a==0 && b < -9900,"unexpected lazy filter history behavior");
    }
    private static void PcmPrototype()
    {
        foreach(int rate in new[]{22050,44100,48000,96000}) {
            var pcm=new PcmStaging(rate); var published=pcm.AudioBuffer;
            var expected=new uint[published.Length]; var random=new Random(731);
            for(int f=0;f<20;f++) {
                pcm.Begin(); Check(Object.ReferenceEquals(published,pcm.AudioBuffer),"published identity");
                for(int i=0;i<expected.Length;i++) {
                    short l=(short)random.Next(-32768,32768), r=(short)random.Next(-32768,32768);
                    expected[i]=P(l,r); pcm.Value(i,l,r);
                }
                pcm.End(); Check(expected.SequenceEqual(published),"identity PCM not exact");
                pcm.Begin(); Check(expected.SequenceEqual(published),"begin overwrites completed audio");
                pcm.Value(0,123,456); Check(expected.SequenceEqual(published),"partial next frame overwrites completed audio");
                pcm.End(); Check(published[0]==P(123,456) && published.Skip(1).All(v=>v==0),"missing core/stale tail");
            }
        }
        Console.WriteLine("PCM_STAGING_PROTOTYPE PASS random full-scale stereo exact, stable identity, next Begin/partial writes isolated, missing tail silence; chip code unchanged");
    }
    private static unsafe void ActualBackendGap()
    {
        var ds=(DirectSound)FormatterServices.GetUninitializedObject(typeof(DirectSound));
        var free=new ConcurrentQueue<uint[]>(); var play=new ConcurrentQueue<uint[]>();
        for(int i=0;i<4;i++) free.Enqueue(new uint[882]);
        var ev=new AutoResetEvent(false);
        Action<string,object> set=(name,value)=>typeof(DirectSound).GetField(name,F).SetValue(ds,value);
        set("_fillQueue",free); set("_playQueue",play); set("_frameEvent",ev);
        set("_isAvailable",true); set("_bufferCount",4);
        var src=new uint[882]; var frame=new FrameSound(44100,new[]{src},false,100,null,true);
        var info=new FrameInfo(null,0,0,44100,false,""); var output=new uint[882];
        var request=typeof(DirectSound).GetMethod("OnBufferRequest",F);
        Action drain=()=> { fixed(uint* ptr=output) request.Invoke(ds,new object[]{Pointer.Box(ptr,typeof(uint*)),output.Length}); };
        ds.PushFrame(info,frame); drain();
        set("_isAvailable",false);
        for(int f=0;f<50;f++) {
            for(int i=0;i<src.Length;i++) src[i]=P(-20000,-20000);
            frame.Refresh(); ds.PushFrame(info,frame);
        }
        Check(play.Count==0,"unavailable backend queued PCM");
        frame.Refresh(); set("_isAvailable",true); ds.PushFrame(info,frame); drain();
        Check(L(output[0]) == -9996,"actual backend gap did not reproduce");
        Console.WriteLine("ACTUAL_PushFrame_unavailable_gap first={0}, expected_settled0",L(output[0]));
        // Prototype minimal Refresh behavior: if the previous cache was never
        // consumed, invalidate filter history rather than mix inaudible frames.
        frame=new FrameSound(44100,new[]{src},false,100,null,true);
        Array.Clear(src,0,src.Length); ds.PushFrame(info,frame); drain();
        set("_isAvailable",false);
        for(int f=0;f<50;f++) {
            for(int i=0;i<src.Length;i++) src[i]=P(-20000,-20000);
            RefreshGapSafe(frame); ds.PushFrame(info,frame);
        }
        RefreshGapSafe(frame); set("_isAvailable",true); ds.PushFrame(info,frame); drain();
        Check(output.All(v=>v==0),"reset-on-discard recovery not silent");
        Console.WriteLine("PROTOTYPE_Refresh_reset_history_if_unconsumed recovered_zero=PASS; no skipped-frame mixing");
        ev.Dispose(); GC.SuppressFinalize(ds);
    }
    private static void RefreshGapSafe(FrameSound frame)
    {
        if(typeof(FrameSound).GetField("_buffer",F).GetValue(frame)==null) {
            typeof(FrameSound).GetField("_widePrimed",F).SetValue(frame,false);
            typeof(FrameSound).GetField("_wideOutputLeft",F).SetValue(frame,0D);
            typeof(FrameSound).GetField("_wideOutputRight",F).SetValue(frame,0D);
            typeof(FrameSound).GetField("_dcPrimed",F).SetValue(frame,false);
            typeof(FrameSound).GetField("_dcPreviousOutputLeft",F).SetValue(frame,0F);
            typeof(FrameSound).GetField("_dcPreviousOutputRight",F).SetValue(frame,0F);
        }
        frame.Refresh();
    }
    private static void DacFingerprints()
    {
        foreach(int rate in new[]{22050,44100,48000,96000}) {
            var dac=new Dac(rate);int n=dac.AudioBuffer.Length;ulong hash=14695981039346656037UL;
            for(int f=0;f<50;f++) {
                dac.Begin();
                for(int i=0;i<n;i++) {
                    int t=f*n+i;dac.Value(i,(short)((t*3571+131)&65535),(short)((t*2179+733)&65535));
                }
                dac.End();foreach(uint p in dac.AudioBuffer)unchecked{hash=(hash^p)*1099511628211UL;}
            }
            Console.WriteLine("DEFAULT_DAC rate={0} hash={1:X16}",rate,hash);
        }
    }
    private static int Main()
    {
        try {
            Thread.CurrentThread.CurrentCulture=CultureInfo.InvariantCulture;
            Console.WriteLine("v97 copied DLL audit; x86={0}; no production modifications",IntPtr.Size==4);
            FrequencyResponse(); IndependenceAndLimiter(); Backend(); DropHistory(); PcmPrototype(); ActualBackendGap(); DacFingerprints();
            Console.WriteLine("PASS checks={0}; synthetic measurements, no subjective listening or actual Windows endpoint capture",checks);
            return 0;
        } catch(Exception ex) {Console.Error.WriteLine(ex); return 1;}
    }
}
