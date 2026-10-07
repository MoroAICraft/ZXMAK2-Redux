using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Threading;
using System.Xml;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Hardware;
using ZXMAK2.Hardware.Evo;
using ZXMAK2.Host.Entities;
using ZXMAK2.Host.WinForms.Mdx;

// Research only. Calls copied, unchanged v81 binaries; never rebuilds a DLL.
// Synthetic signals isolate components, NOT a matched music/listening test.
internal static class AudioResearchProbe
{
    private const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    private static int checks;
    private static void Require(bool value,string text) { checks++; if(!value) throw new Exception(text); }
    private static short L(uint v) { return unchecked((short)v); }
    private static short R(uint v) { return unchecked((short)(v>>16)); }
    private static uint Pack(short l,short r) { return (uint)(ushort)l|((uint)(ushort)r<<16); }
    private sealed class Metric
    {
        internal double Sum,Square,Sin,Cos; internal long Count,Clips; internal int Peak,RightPeak;
        internal void Add(uint value,long sample,int rate,double hz) {
            int l=L(value),r=R(value); Sum+=l; Square+=(double)l*l; Count++;
            Peak=Math.Max(Peak,Math.Abs(l)); RightPeak=Math.Max(RightPeak,Math.Abs(r));
            if(l==short.MinValue || l==short.MaxValue) Clips++;
            double phase=2*Math.PI*hz*sample/rate; Sin+=l*Math.Sin(phase); Cos+=l*Math.Cos(phase);
        }
        internal double Rms { get { return Math.Sqrt(Square/Count); } }
        internal double Fundamental { get { return 2*Math.Sqrt(Sin*Sin+Cos*Cos)/Count; } }
        internal double Mean { get { return Sum/Count; } }
    }
    private static void Mixer()
    {
        foreach(int count in new[]{1,2,4,8,12}) {
            var sources=new uint[count][]; for(int j=0;j<count;j++) sources[j]=new uint[882];
            for(int i=0;i<882;i++) sources[0][i]=Pack((short)((i&1)==0 ? 12000:-12000),0);
            var pcm=new FrameSound(44100,sources).GetBuffer();
            Require(L(pcm[0])==12000/count && R(pcm[0])==0,"Mixer divisor/channel mismatch");
            Console.WriteLine("MIX one signal+silence sources={0} amplitude={1} attenuation={2:F3}dB",count,L(pcm[0]),20*Math.Log10(L(pcm[0])/12000D));
        }
        var source=new uint[882]; for(int i=0;i<source.Length;i++) source[i]=Pack((short)((i&1)==0 ? 30000:-30000),0);
        foreach(int gain in new[]{100,140,200}) {
            var pcm=new FrameSound(44100,new[]{source},false,100,new[]{gain}).GetBuffer();
            Console.WriteLine("LIMIT singleSource input30000 sourceGain={0} result={1}",gain,L(pcm[0]));
            Require(R(pcm[0])==0,"Limiter channel leak");
        }
    }
    private static Metric Sine(FrameSound mix,uint[] source,int rate,int hz)
    {
        var result=new Metric(); int samples=rate/50;
        for(int frame=0;frame<60;frame++) {
            for(int i=0;i<samples;i++) source[i]=Pack((short)Math.Round(12000*Math.Sin(2*Math.PI*hz*(frame*(long)samples+i)/rate)),0);
            mix.Refresh(); var output=mix.GetBuffer();
            if(frame>=10) for(int i=0;i<samples;i++) result.Add(output[i],frame*(long)samples+i,rate,hz);
        }
        return result;
    }
    private static void DcResponse()
    {
        foreach(int rate in new[]{44100,48000,96000}) foreach(int hz in new[]{20,50,100,1000}) {
            var input=new uint[rate/50]; var output=Sine(new FrameSound(rate,new[]{input},true),input,rate,hz);
            Console.WriteLine("HPF rate={0} hz={1} gain={2:F3}dB rms={3:F3} mean={4:F3} rightPeak={5}",
                rate,hz,20*Math.Log10(output.Fundamental/12000),output.Rms,output.Mean,output.RightPeak);
            Require(output.RightPeak==0,"HPF channel leak");
        }
    }
    private sealed class Dac : SoundDeviceBase
    {
        private static readonly MethodInfo Timing=typeof(SoundDeviceBase).GetMethod("ApplyTimings",F);
        private static readonly FieldInfo Last=typeof(SoundDeviceBase).GetField("m_lastDacTact",F);
        internal Dac(int rate) { SampleRate=rate; Timing.Invoke(this,new object[]{3500000,rate}); }
        internal void Begin() { Last.SetValue(this,-1); base.OnBeginFrame(); }
        internal void Value(int index,short left,short right) { UpdateDac((double)index/AudioBuffer.Length,left,right); }
        internal void End() { base.OnEndFrame(); }
    }
    private static void PcmResponse()
    {
        foreach(int rate in new[]{44100,48000}) foreach(int hz in new[]{100,1000,5000,10000,15000,20000}) {
            var dac=new Dac(rate); int size=rate/50; var metric=new Metric();
            for(int frame=0;frame<60;frame++) {
                dac.Begin(); for(int i=0;i<size;i++) dac.Value(i,(short)Math.Round(12000*Math.Sin(2*Math.PI*hz*(frame*(long)size+i)/rate)),0);
                dac.End(); if(frame>=10) for(int i=0;i<size;i++) metric.Add(dac.AudioBuffer[i],frame*(long)size+i,rate,hz);
            }
            Console.WriteLine("PCM through shared DAC rate={0} hz={1} gain={2:F3}dB rms={3:F3} mean={4:F3} rightPeak={5}",
                rate,hz,20*Math.Log10(metric.Fundamental/12000),metric.Rms,metric.Mean,metric.RightPeak);
            Require(metric.RightPeak==0,"DAC channel leak");
        }
    }
    private static int Inventory(string xml,string name)
    {
        var machine=OptimizationProbe.Create(xml); var bm=machine.BusManager;
        var mixed=bm.SoundFrame; var sources=(uint[][])typeof(FrameSound).GetField("_sources",F).GetValue(mixed);
        Console.WriteLine("INVENTORY {0} count={1} sampleRate={2} RejectDc={3} master={4}",name,sources.Length,mixed.SampleRate,
            typeof(FrameSound).GetField("_rejectDc",F).GetValue(mixed),typeof(FrameSound).GetField("_masterVolume",F).GetValue(mixed));
        foreach(var dev in bm.FindDevices<ISoundRenderer>()) Console.WriteLine("SOURCE standalone {0} volume={1}",dev.GetType().Name,dev.Volume);
        foreach(var owner in bm.FindDevices<IAdditionalSoundRenderers>()) foreach(var dev in owner.SoundRenderers)
            Console.WriteLine("SOURCE owner={0} renderer={1} volume={2}",owner.GetType().Name,dev.GetType().Name,dev.Volume);
        bm.Disconnect(); return sources.Length;
    }
    private static string BoardProfile(string xml,string slot,string type)
    {
        var doc=new XmlDocument(); doc.LoadXml(xml); var bus=doc.DocumentElement;
        foreach(XmlNode node in bus.SelectNodes("Device").Cast<XmlNode>().ToArray()) {
            string value=node.Attributes["type"].Value;
            if(value.Contains("TurboSoundFmPro")||value.Contains("AYCHRV")||value.Contains("ZxmMoonSoundDevice")||value.Contains("ZxOmniSoundDevice")||value.Contains("ZxMultiSoundMaxDevice")) bus.RemoveChild(node);
        }
        var ula=(XmlElement)bus.SelectSingleNode("Device[contains(@type,'UlaPentEvo')]");
        ula.SetAttribute("internalSound","None"); ula.SetAttribute("zxBusSlot1Device",slot);
        ula.SetAttribute("zxBusSlot1Enabled","True"); ula.SetAttribute("zxBusSlot2Enabled","False");
        var sound=doc.CreateElement("Device"); sound.SetAttribute("type",type); bus.AppendChild(sound);
        return bus.OuterXml;
    }
    private static unsafe void BackendCopy()
    {
        // REAL DirectSound copy/underrun methods, no native device/window/audio.
        var target=(DirectSound)FormatterServices.GetUninitializedObject(typeof(DirectSound));
        var free=new ConcurrentQueue<uint[]>(); var play=new ConcurrentQueue<uint[]>();
        for(int i=0;i<4;i++) free.Enqueue(new uint[882]);
        var signal=new AutoResetEvent(false);
        typeof(DirectSound).GetField("_fillQueue",F).SetValue(target,free);
        typeof(DirectSound).GetField("_playQueue",F).SetValue(target,play);
        typeof(DirectSound).GetField("_frameEvent",F).SetValue(target,signal);
        typeof(DirectSound).GetField("_zeroValue",F).SetValue(target,0U);
        var input=new uint[882]; for(int i=0;i<input.Length;i++) input[i]=Pack((short)(i*23-10000),(short)(10000-i*7));
        var frame=new FrameSound(44100,new[]{input}); var expected=frame.GetBuffer(); var output=new uint[882];
        var info=new FrameInfo(null,0,0,44100,false,""); target.PushFrame(info,frame);
        var request=typeof(DirectSound).GetMethod("OnBufferRequest",F);
        fixed(uint* ptr=output) request.Invoke(target,new object[]{Pointer.Box(ptr,typeof(uint*)),output.Length});
        Require(expected.SequenceEqual(output),"Host copy changes PCM");
        Console.WriteLine("BACKEND copied882 stereo samples bit-exact PASS");
        fixed(uint* ptr=output) request.Invoke(target,new object[]{Pointer.Box(ptr,typeof(uint*)),output.Length});
        Require(output[output.Length-1]==0,"Underrun does not end at silence");
        Console.WriteLine("BACKEND simulated underrun lastSample ramp first={0}/{1} final=0",L(output[0]),R(output[0]));
        for(int i=0;i<20;i++) target.PushFrame(info,frame);
        Require(play.Count==4 && free.Count==0,"Host pool not bounded");
        Console.WriteLine("BACKEND unsynchronized producer20 pushes queue4: remaining16 dropped (not a live underrun measurement)");
        signal.Dispose(); GC.SuppressFinalize(target);
    }
    private static class Midi
    {
        [DllImport("omni_midi.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="omni_midi_create")]
        internal static extern IntPtr Create(byte[] bank,int length,int rate);
        [DllImport("omni_midi.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="omni_midi_byte")]
        internal static extern void Byte(IntPtr synth,byte value);
        [DllImport("omni_midi.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="omni_midi_render")]
        internal static extern void Render(IntPtr synth,[Out] short[] output,int frames);
        [DllImport("omni_midi.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="omni_midi_destroy")]
        internal static extern void Destroy(IntPtr synth);
    }
    private static void MidiNote()
    {
        byte[] bank=File.ReadAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"GeneralUser-GS.sf2"));
        var synth=Midi.Create(bank,bank.Length,44100); Require(synth!=IntPtr.Zero,"MIDI synth unavailable");
        try {
            foreach(byte value in new byte[]{0xC0,0,0xB0,7,127,0xB0,11,127,0x90,69,100}) Midi.Byte(synth,value);
            var pcm=new short[882*2]; var dac=new Dac(44100); var raw=new Metric(); var filtered=new Metric();
            for(int frame=0;frame<100;frame++) {
                Midi.Render(synth,pcm,882); dac.Begin();
                for(int i=0;i<882;i++) {
                    if(frame>=10) raw.Add(Pack(pcm[2*i],pcm[2*i+1]),frame*882L+i,44100,440);
                    dac.Value(i,pcm[2*i],pcm[2*i+1]);
                }
                dac.End(); if(frame>=10) foreach(uint value in dac.AudioBuffer) filtered.Add(value,0,44100,0);
            }
            Console.WriteLine("MIDI GM synthetic piano69 vel100 CC7/11=127 after10 warmupFrames rawRMS={0:F3} rawPeak={1} DACrms={2:F3} DACpeak={3} clipsRaw={4}",raw.Rms,raw.Peak,filtered.Rms,filtered.Peak,raw.Clips);
            Require(raw.Peak>0 && filtered.Peak>0,"MIDI silent");
            Console.WriteLine("LIMIT: TinySoundFont GM path used by Max/Omni, NOT MIDI through MoonSound OPL4, NOT user's song.");
        } finally { Midi.Destroy(synth); }
    }
    private static void PortWrite(List<byte> code,ushort port,byte value)
    { code.AddRange(new byte[]{0x01,(byte)port,(byte)(port>>8),0x3E,value,0xED,0x79}); }
    private static void RegisterWrite(List<byte> code,ushort port,byte reg,byte value)
    { PortWrite(code,port,reg); PortWrite(code,(ushort)(port+1),value); }
    private static void MoonVolume()
    {
        foreach(int volume in new[]{0,50,100}) {
            string xml="<Bus><Device type='ZXMAK2.Hardware.Spectrum.UlaSpectrum48'/><Device type='ZXMAK2.Hardware.Spectrum.MemorySpectrum48'/><Device type='ZXMAK2.Hardware.Evo.ZxmMoonSoundDevice' volume='"+volume+"'/></Bus>";
            var machine=OptimizationProbe.Create(xml); var mem=OptimizationProbe.Prepare(machine,null,0);
            var board=machine.BusManager.FindDevice<ZxmMoonSoundDevice>();
            Require(board.CoreAvailable,"Moon core unavailable");
            var code=new List<byte>();
            RegisterWrite(code,0x78C6,0x05,0x03);
            RegisterWrite(code,0x9A7E,0xF9,0x00);
            RegisterWrite(code,0x9A7E,0x08,0x00);
            RegisterWrite(code,0x9A7E,0x20,0xFE);
            RegisterWrite(code,0x9A7E,0x38,0x07);
            RegisterWrite(code,0x9A7E,0x50,0x01);
            RegisterWrite(code,0x9A7E,0x68,0x80);
            code.Add(0x18); code.Add(0xFE);
            for(int i=0;i<code.Count;i++) mem.WRMEM_DBG((ushort)(0x4000+i),code[i]);
            machine.DebugReset(); machine.CPU.regs.PC=0x4000;
            var metric=new Metric(); ulong hash=14695981039346656037UL;
            for(int frame=0;frame<50;frame++) {
                machine.ExecuteFrame();
                if(frame>=10) foreach(uint value in board.AudioBuffer) {
                    metric.Add(value,0,44100,0); unchecked { hash=(hash^value)*1099511628211UL; }
                }
            }
            Console.WriteLine("MOON nativeROM waveform0 actualCPU port writes volumeProperty={0} RMS={1:F3} peak={2} hash={3:X16}",board.Volume,metric.Rms,metric.Peak,hash);
            Require(metric.Peak>0,"Moon synthetic waveform silent");
            machine.BusManager.Disconnect();
        }
        Console.WriteLine("LIMIT: Moon PCM register voice test, NOT MIDI player/track startup.");
    }
    private static int Main(string[] args)
    {
        try {
            Console.WriteLine("AUDIO RESEARCH unchanged v81; x86={0}; no production patch",IntPtr.Size==4);
            Mixer(); DcResponse(); PcmResponse(); BackendCopy(); MidiNote(); MoonVolume();
            Inventory(File.ReadAllText(args[0]),"Pentagon test");
            string evo=OptimizationProbe.EvoBus(args[1]).OuterXml;
            Inventory(evo,"Evo current TSFM+Moon");
            Inventory(BoardProfile(evo,"MoonSound",typeof(ZxmMoonSoundDevice).AssemblyQualifiedName),"Evo Moon-only music");
            Inventory(BoardProfile(evo,"MultiSoundMax",typeof(ZxMultiSoundMaxDevice).AssemblyQualifiedName),"Evo Max-only music");
            Inventory(BoardProfile(evo,"OmniSound",typeof(ZxOmniSoundDevice).AssemblyQualifiedName),"Evo Omni-only music");
            Console.WriteLine("PASS checks={0}; synthetic component tests, not musical reference/real Windows-output verification",checks);
            return 0;
        } catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
