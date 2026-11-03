using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml;
using ZXMAK2.Engine;
using ZXMAK2.Hardware.Evo;

// Run only in a quiet window, private runtime, no media or host audio device.
internal static class MixerBenchmark
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    [STAThread] static int Main(string[] args)
    {
        PortableBootstrap.Initialize();Thread.CurrentThread.CurrentCulture=CultureInfo.InvariantCulture;
        try {
            string workload=args[0];int round=int.Parse(args[1]);int frames=int.Parse(args[2]);
            string affinity=Environment.GetEnvironmentVariable("ZXMAK2_BENCH_AFFINITY");
            if(!String.IsNullOrEmpty(affinity))Process.GetCurrentProcess().ProcessorAffinity=new IntPtr(Convert.ToInt64(affinity,16));
            Spectrum machine;IDisposable fixture;
            if(workload=="reset-service") {
                var doc=new XmlDocument();doc.Load(args[3]);
                foreach(XmlAttribute a in doc.SelectNodes("//@sdPath|//@sdImage|//@ideImage|//@ideCdDrive|//@hostFolder|//@fileName|//@neoGsSdImage"))
                    if(!String.IsNullOrEmpty(a.Value))throw new Exception("Benchmark profile has media: "+a.Name);
                machine=new Spectrum();fixture=machine;machine.BusManager.Init(machine,true);machine.BusManager.Disconnect();machine.BusManager.Clear();
                machine.BusManager.LoadConfigXml(doc.DocumentElement.SelectSingleNode("Bus"));
                machine.DebugReset();machine.BusManager.FindDevice<MemoryPentEvo>().ResetState();
            } else {
                var type=typeof(Opl3Probe).GetNestedType("Fixture",BindingFlags.NonPublic);
                object o=Activator.CreateInstance(type,Flags,null,new object[]{44100,true,true},CultureInfo.InvariantCulture);
                fixture=(IDisposable)o;machine=(Spectrum)type.GetField("Machine",Flags).GetValue(o);
                var regMethod=type.GetMethod("Reg",Flags);Action<int,int> reg=(r,v)=>regMethod.Invoke(o,new object[]{r,v});
                if(workload=="pcm-18voices") {
                    reg(0x105,1);var tone=typeof(Opl3Probe).GetMethod("Tone",BindingFlags.Static|BindingFlags.NonPublic);
                    for(int ch=0;ch<18;ch++)tone.Invoke(null,new object[]{reg,ch,0x30});
                } else if(workload!="pcm-idle")throw new Exception("Unknown workload");
            }
            try {
                // Warmup includes JIT, native generation and reset-service boot.
                for(int i=0;i<300;i++){machine.ExecuteFrame();machine.BusManager.SoundFrame.GetBuffer();}
                AppDomain.MonitoringIsEnabled=true;GC.Collect();
                int gc0=GC.CollectionCount(0),gc1=GC.CollectionCount(1),gc2=GC.CollectionCount(2);
                var durations=new double[frames];long bytes=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var total=Stopwatch.StartNew();uint sink=0;
                for(int i=0;i<frames;i++) {
                    long start=Stopwatch.GetTimestamp();machine.ExecuteFrame();
                    var pcm=machine.BusManager.SoundFrame.GetBuffer();sink^=pcm[0];
                    durations[i]=(Stopwatch.GetTimestamp()-start)*1000D/Stopwatch.Frequency;
                }
                total.Stop();bytes=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-bytes;Array.Sort(durations);
                Console.WriteLine("BENCH workload={0} round={1} frames={2} total_ms={3:F3} mean_ms={4:F4} p50={5:F4} p95={6:F4} p99={7:F4} max={8:F4} allocated={9} gc={10}/{11}/{12} cpuMHz={13:F1} pc={14:X4} sink={15:X8}",
                    workload,round,frames,total.Elapsed.TotalMilliseconds,total.Elapsed.TotalMilliseconds/frames,
                    durations[(frames-1)/2],durations[(int)((frames-1)*.95)],durations[(int)((frames-1)*.99)],durations[frames-1],bytes,
                    GC.CollectionCount(0)-gc0,GC.CollectionCount(1)-gc1,GC.CollectionCount(2)-gc2,
                    machine.BusManager.FindDevice<MemoryPentEvo>().CpuClockMultiplier*3.5,machine.CPU.regs.PC,sink);
            } finally {machine.BusManager.Disconnect();fixture.Dispose();}
            return 0;
        } catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
