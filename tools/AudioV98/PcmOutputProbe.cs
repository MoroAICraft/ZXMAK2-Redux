using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using ZXMAK2.Hardware;
using ZXMAK2.Host.Entities;
using ZXMAK2.Host.WinForms.Mdx;

// Candidate unit/regression harness. Build against candidate runtime, or link
// current SoundDeviceBase.cs/FrameSound.cs against copied published dependencies.
internal static class PcmOutputProbe
{
    private const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    private static int checks;
    private static void Check(bool value,string label) { checks++; if(!value) throw new Exception(label); }
    private static uint P(short l,short r) { return (uint)(ushort)l|((uint)(ushort)r<<16); }
    private static short L(uint p) {return unchecked((short)p);}
    private sealed class Pcm : SoundDeviceBase
    {
        internal Pcm(int rate) { SampleRate=rate; typeof(SoundDeviceBase).GetMethod("ApplyTimings",F).Invoke(this,new object[]{3500000,rate}); }
        protected override bool UsesPcmBuffer {get{return true;}}
        internal void Begin(){base.OnBeginFrame();}
        internal void Put(int index,short l,short r){WritePcmSample(index,l,r);}
        internal void End(){base.OnEndFrame();}
    }
    private sealed class Dac : SoundDeviceBase
    {
        private static readonly FieldInfo Last=typeof(SoundDeviceBase).GetField("m_lastDacTact",F);
        internal Dac(int rate){SampleRate=rate;typeof(SoundDeviceBase).GetMethod("ApplyTimings",F).Invoke(this,new object[]{3500000,rate});}
        internal void Begin(){Last.SetValue(this,-1);base.OnBeginFrame();}
        internal void Put(int index,short l,short r){UpdateDac((double)index/AudioBuffer.Length,l,r);}
        internal void End(){base.OnEndFrame();}
    }
    private static void Publication(int rate)
    {
        var pcm=new Pcm(rate);var published=pcm.AudioBuffer;int n=published.Length;
        var expected=new uint[n];var rng=new Random(713); ulong hash=14695981039346656037UL;
        for(int f=0;f<50;f++) {
            pcm.Begin();
            Check(ReferenceEquals(published,pcm.AudioBuffer),"published buffer identity");
            for(int chunk=0;chunk<n;) {
                int end=Math.Min(n,chunk+rng.Next(1,61));
                for(int i=chunk;i<end;i++) {
                    short l=(short)rng.Next(-32768,32768),r=(short)rng.Next(-32768,32768);
                    expected[i]=P(l,r);pcm.Put(i,l,r);
                }
                chunk=end;
            }
            pcm.End();
            Check(expected.SequenceEqual(published),"fullscale stereo/chunk identity");
            foreach(uint p in published) unchecked{hash=(hash^p)*1099511628211UL;}
            pcm.Begin();
            Check(expected.SequenceEqual(published),"next begin changed completed frame");
            pcm.Put(0,short.MinValue,short.MaxValue);
            Check(expected.SequenceEqual(published),"next write changed completed frame");
            pcm.End();
            Check(published[0]==P(short.MinValue,short.MaxValue)&&published.Skip(1).All(p=>p==0),"missing core tail not silence");
            pcm.Begin();
            for(int i=0;i<n/2;i++)pcm.Put(i,12000,-7000);
            // Simulate a completed chip reset: pre-reset samples must survive,
            // later samples are the reset chip's zero output, no retroactive edit.
            for(int i=n/2;i<n;i++)pcm.Put(i,0,0);
            pcm.End();
            Check(published.Take(n/2).All(p=>p==P(12000,-7000))&&published.Skip(n/2).All(p=>p==0),"midframe reset boundary");
        }
        bool range=false;try{pcm.Put(n,1,1);}catch(IndexOutOfRangeException){range=true;}
        Check(range,"out of range write accepted");
        Console.WriteLine("PCM_PUBLICATION rate={0} frames=50 hash={1:X16} PASS",rate,hash);
    }
    private static void DacFingerprint(int rate)
    {
        var dac=new Dac(rate);int n=dac.AudioBuffer.Length;ulong hash=14695981039346656037UL;
        for(int f=0;f<50;f++) {
            dac.Begin();
            for(int i=0;i<n;i++) {
                int t=f*n+i;dac.Put(i,(short)((t*3571+131)&65535),(short)((t*2179+733)&65535));
            }
            dac.End();foreach(uint p in dac.AudioBuffer) unchecked{hash=(hash^p)*1099511628211UL;}
        }
        Console.WriteLine("DEFAULT_DAC rate={0} hash={1:X16}",rate,hash);
    }
    private static void ContinuousDc(int rate)
    {
        int n=rate/50;var source=new uint[n];var mix=new FrameSound(rate,new[]{source},false,100,null,true);
        double pole=Math.Exp(-2*Math.PI*5/rate),prev=0,state=0;bool first=true;
        for(int f=0;f<60;f++) {
            for(int i=0;i<n;i++) source[i]=P((short)(7000*Math.Sin((f*n+i)*.13)-10000),0);
            mix.Refresh();var output=mix.GetBuffer();
            for(int i=0;i<n;i++) {
                double input=L(source[i]);if(first){prev=input;first=false;}
                state=(1+pole)/2*(input-prev)+pole*state;prev=input;
                Check(L(output[i])==(short)Math.Round(state*.5),"continuous DC recurrence changed");
            }
            Check(ReferenceEquals(output,mix.GetBuffer()),"repeat consumer invalidated filter");
        }
        Console.WriteLine("CONTINUOUS_DC rate={0} PASS independent recurrence + cached consumer",rate);
    }
    private static unsafe void ActualBackendRecovery()
    {
        foreach(bool modern in new[]{false,true}) {
            var ds=(DirectSound)FormatterServices.GetUninitializedObject(typeof(DirectSound));
            var free=new ConcurrentQueue<uint[]>();var play=new ConcurrentQueue<uint[]>();
            for(int i=0;i<4;i++)free.Enqueue(new uint[882]);var ev=new AutoResetEvent(false);
            Action<string,object> set=(name,value)=>typeof(DirectSound).GetField(name,F).SetValue(ds,value);
            set("_fillQueue",free);set("_playQueue",play);set("_frameEvent",ev);set("_isAvailable",true);
            var source=new uint[882];var frame=new FrameSound(44100,new[]{source},true,100,null,modern);
            var info=new FrameInfo(null,0,0,44100,false,"");var output=new uint[882];
            var request=typeof(DirectSound).GetMethod("OnBufferRequest",F);
            Action drain=()=>{fixed(uint* ptr=output)request.Invoke(ds,new object[]{Pointer.Box(ptr,typeof(uint*)),output.Length});};
            ds.PushFrame(info,frame);drain();set("_isAvailable",false);
            for(int f=0;f<50;f++){
                for(int i=0;i<source.Length;i++)source[i]=P(-20000,-20000);
                frame.Refresh();ds.PushFrame(info,frame);
            }
            frame.Refresh();set("_isAvailable",true);ds.PushFrame(info,frame);drain();
            Check(output.All(p=>p==0),"backend recovery stale DC");
            // Exhaustion uses the same early return before GetBuffer.
            for(int i=0;i<4;i++){frame.Refresh();ds.PushFrame(info,frame);}
            for(int i=0;i<source.Length;i++)source[i]=P(20000,20000);
            frame.Refresh();ds.PushFrame(info,frame); // unconsumed frame
            for(int i=0;i<4;i++)drain();
            frame.Refresh();ds.PushFrame(info,frame);drain();
            Check(output.All(p=>p==0),"full pool recovery stale DC");
            Console.WriteLine("ACTUAL_BACKEND_GAPS modern={0} unavailable + full pool PASS",modern);
            ev.Dispose();GC.SuppressFinalize(ds);
        }
    }
    private static void Cost()
    {
        AppDomain.MonitoringIsEnabled=true;
        int n=882,frames=1000;var source=new short[n];for(int i=0;i<n;i++)source[i]=(short)(12000*Math.Sin(i*.3));
        var dac=new Dac(44100);var pcm=new Pcm(44100);
        // warmup includes queue capacity and JIT; reflection Begin is part of
        // old-path diagnostic overhead, not the real device's steady state.
        for(int i=0;i<10;i++){dac.Begin();pcm.Begin();for(int j=0;j<n;j++){dac.Put(j,source[j],source[j]);pcm.Put(j,source[j],source[j]);}dac.End();pcm.End();}
        for(int pass=0;pass<4;pass++) {
            bool direct=(pass&1)!=0;GC.Collect();long before=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
            var sw=Stopwatch.StartNew();
            for(int f=0;f<frames;f++) {
                if(direct){pcm.Begin();for(int i=0;i<n;i++)pcm.Put(i,source[i],source[i]);pcm.End();}
                else{dac.Begin();for(int i=0;i<n;i++)dac.Put(i,source[i],source[i]);dac.End();}
            }
            sw.Stop();long allocated=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-before;
            Console.WriteLine("COMPONENT_COST direct={0} frames={1} elapsed_ms={2:F3} allocated_bytes={3}",direct,frames,sw.Elapsed.TotalMilliseconds,allocated);
        }
    }
    private static int Main(string[] args)
    {
        Thread.CurrentThread.CurrentCulture=CultureInfo.InvariantCulture;
        try {
            foreach(int rate in new[]{22050,44100,48000,96000}){Publication(rate);DacFingerprint(rate);ContinuousDc(rate);}
            ActualBackendRecovery();if(!args.Contains("--no-cost"))Cost();Console.WriteLine("PASS candidate PCM/output checks={0}",checks);return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
