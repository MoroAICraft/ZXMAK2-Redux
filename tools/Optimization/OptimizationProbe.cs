using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Xml;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Hardware.Evo;

// Bounded measurements use the UNCHANGED published Test.exe's private helpers.
// Fingerprints/observers run separately, never contaminate performance results.
internal static class OptimizationProbe
{
    private static readonly BindingFlags Private = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly Type Tests = Assembly.LoadFrom(Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "Test.exe")).GetType("Test.Program");
    internal static Spectrum Create(string xml)
    {
        // Diagnostic-only: compare complete machine/PCM fingerprints while
        // intentionally selecting the v81 final mixer on an audio candidate.
        // Older control DLLs ignore the XML attribute, so both use identical
        // test source without requiring the new BusManager property.
        if (Environment.GetEnvironmentVariable("ZXMAK2_STATE_LEGACY") == "1") {
            var doc = new XmlDocument(); doc.LoadXml(xml);
            doc.DocumentElement.SetAttribute("legacyAudioMix", "true");
            xml = doc.OuterXml;
        }
        return (Spectrum)Tests.GetMethod("GetTestMachine", Private).Invoke(null, new object[] { xml });
    }
    private static void Reload(Spectrum vm, XmlNode bus)
    {
        if (Environment.GetEnvironmentVariable("ZXMAK2_STATE_LEGACY") == "1")
            ((XmlElement)bus).SetAttribute("legacyAudioMix", "true");
        vm.BusManager.LoadConfigXml(bus);
    }
    internal static XmlNode EvoBus(string path)
    {
        var config = new XmlDocument(); config.Load(path);
        var bus = config.SelectSingleNode("//Bus");
        foreach (XmlElement node in bus.SelectNodes("Device").Cast<XmlElement>().ToArray())
        {
            if (node.GetAttribute("type").Contains("CmosPentEvo")) { bus.RemoveChild(node); continue; }
            if (node.GetAttribute("type").Contains("ZsdPentEvo"))
            { node.SetAttribute("sdImage", ""); node.SetAttribute("sdPath", ""); node.SetAttribute("sdSource", "Image"); }
            if (node.HasAttribute("ideImage")) node.SetAttribute("ideImage", "");
        }
        return bus;
    }
    private static ulong ioHash, reads, writes, fetches;
    private static void Trace(ushort addr, byte value, bool handled, long tact)
    {
        unchecked { ioHash = (ioHash ^ addr) * 1099511628211UL;
            ioHash = (ioHash ^ value) * 1099511628211UL;
            ioHash = (ioHash ^ (handled ? 1UL : 0UL)) * 1099511628211UL;
            ioHash = (ioHash ^ (ulong)tact) * 1099511628211UL; }
    }
    private static void Hash(SHA256 sha, Array data)
    {
        var bytes = new byte[Buffer.ByteLength(data)];
        Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
        sha.TransformBlock(bytes, 0, bytes.Length, bytes, 0);
    }
    internal static IMemoryDevice Prepare(Spectrum vm, string snapshot, int rate)
    {
        vm.IsRunning = true; vm.DebugReset();
        var memory = vm.BusManager.FindDevice<IMemoryDevice>();
        // Production models deliberately randomize power-on RAM. Normalize only
        // our disposable test machine before warmup/snapshot load, not the model.
        for (int page = 0; page < memory.RamPages.Length; page++)
            for (int offset = 0; offset < memory.RamPages[page].Length; offset++)
                memory.RamPages[page][offset] = (byte)(page * 17 + offset * 3);
        vm.ExecuteFrame();
        if (snapshot != null)
        {
            using (var stream = (Stream)Tests.GetMethod("GetTestStream", Private).Invoke(null, new object[] { snapshot }))
                vm.BusManager.LoadManager.GetSerializer(Path.GetExtension(snapshot)).Deserialize(stream);
        }
        else
        {
            // RAM program exercises instruction fetch, RAM writes, border/AY,
            // port reads and conditional branches, at a fixed address in RAM.
            byte[] code = { 0x21,0x00,0x80,0x34,0x7E,0xD3,0xFE,0xDB,0xFE,
                0x01,0xFD,0xFF,0x3E,0x00,0xED,0x79,0x06,0xBF,0x3E,0x30,0xED,0x79,
                0x01,0xFD,0xFF,0x3E,0x07,0xED,0x79,0x06,0xBF,0x3E,0x3E,0xED,0x79,
                0x01,0xFD,0xFF,0x3E,0x08,0xED,0x79,0x06,0xBF,0x3E,0x0F,0xED,0x79,
                0xC3,0x03,0x40 };
            for (int i = 0; i < code.Length; i++) memory.WRMEM_DBG((ushort)(0x4000 + i), code[i]);
            vm.CPU.regs.PC = 0x4000; vm.CPU.regs.SP = 0x7F00;
            var evo = memory as MemoryPentEvo;
            if (evo != null)
            {
                var field = typeof(MemoryPentEvo).GetField("m_pFF77", BindingFlags.Instance | BindingFlags.NonPublic);
                field.SetValue(evo, ((int)field.GetValue(evo) & ~8) | (rate == 4 ? 8 : 0));
                evo.TURBO = rate == 1; evo.LatchClockAtRefresh();
            }
        }
        return memory;
    }
    private static void Fingerprint(Spectrum vm, string name, string snapshot, int rate)
    {
        var memory = Prepare(vm, snapshot, rate);
        ioHash = 14695981039346656037UL; reads = writes = fetches = 0;
        var events = ((IBusManager)vm.BusManager).Events;
        events.SubscribeRdMemM1(0, 0, delegate(ushort a, ref byte v) { fetches++; Trace(a,v,false,vm.CPU.Tact); });
        events.SubscribeRdMem(0, 0, delegate(ushort a, ref byte v) { reads++; Trace(a,v,false,vm.CPU.Tact); });
        events.SubscribeWrMem(0, 0, delegate(ushort a, byte v) { writes++; Trace(a,v,false,vm.CPU.Tact); });
        events.SubscribeRdIo(0, 0, delegate(ushort a, ref byte v, ref bool h) { Trace(a,v,h,vm.CPU.Tact); });
        events.SubscribeWrIo(0, 0, delegate(ushort a, byte v, ref bool h) { Trace(a,v,h,vm.CPU.Tact); });
        using (var sha = SHA256.Create())
        {
            for (int frame = 0; frame < 80; frame++)
            {
                vm.ExecuteFrame();
                Hash(sha, vm.BusManager.FindDevice<IUlaDevice>().VideoData.Buffer);
                Hash(sha, vm.BusManager.SoundFrame.GetBuffer());
                var regs = vm.CPU.regs;
                Hash(sha, new ushort[] { regs.AF,regs.BC,regs.DE,regs.HL,regs._AF,regs._BC,regs._DE,regs._HL,
                    regs.IX,regs.IY,regs.IR,regs.PC,regs.SP,regs.MW });
                Hash(sha, new long[] { vm.CPU.Tact, vm.BusManager.GetFrameTact(), vm.BusManager.FrameTactCount });
            }
            foreach (var page in memory.RamPages) Hash(sha, page);
            sha.TransformFinalBlock(new byte[0], 0, 0);
            Console.WriteLine("STATE {0}: {1} IO={2:X16} fetch={3} read={4} write={5} tact={6}",
                name, BitConverter.ToString(sha.Hash).Replace("-",""), ioHash,fetches,reads,writes,vm.CPU.Tact);
        }
    }
    private static int Main(string[] args)
    {
        try
        {
            // Optional harness-only pinning avoids P/E-core migration on hybrid
            // CPUs. Never applied to the released emulator or functional probes.
            string affinity = Environment.GetEnvironmentVariable("ZXMAK2_BENCH_AFFINITY");
            if ((args[0] == "bench" || args[0] == "evo-bench") && !String.IsNullOrEmpty(affinity))
            {
                Process.GetCurrentProcess().ProcessorAffinity = new IntPtr(Convert.ToInt64(affinity, 16));
                Console.WriteLine("HARNESS CPU affinity=0x{0}", affinity);
            }
            if (args[0] == "bench")
            {
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
                var cases = new[] { "ExecTests:testVideo.z80", "ExecTests:zexall.sna", "ExecTests:testOutFe.z80",
                    "ExecLightTests:zexall.sna", "ExecCpuTests:zexall.sna" };
                Console.WriteLine("BOUNDED: x86={0}; 500 frames; 1 warmup + 5 measured rounds; high priority", IntPtr.Size == 4);
                for (int round = -1; round < 5; round++)
                    foreach (var item in cases)
                    {
                        var split = item.Split(':'); var saved = Console.Out; var capture = new StringWriter();
                        Console.SetOut(capture);
                        try { Tests.GetMethod(split[0], Private).Invoke(null, new object[] { split[1], round < 0 ? 100 : 500 }); }
                        finally { Console.SetOut(saved); }
                        if (round >= 0) Console.WriteLine("ROUND {0} {1}",round,capture.ToString().Trim());
                    }
            }
            else if (args[0] == "evo-bench")
            {
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
                var bus = EvoBus(args[1]);
                for (int round = -1; round < 3; round++) foreach (int rate in new[] { 1,2,4 })
                {
                    var vm = Create(bus.OuterXml); Prepare(vm,null,rate);
                    int collections = GC.CollectionCount(0);
                    var watch = Stopwatch.StartNew();
                    for (int i = 0; i < (round < 0 ? 30 : 300); i++)
                    { vm.ExecuteFrame(); vm.BusManager.SoundFrame.GetBuffer(); }
                    watch.Stop(); vm.BusManager.Disconnect();
                    if (round >= 0) Console.WriteLine("EVO {0} rate={1} {2} ms gc0={3}",
                        round,rate,watch.ElapsedMilliseconds,GC.CollectionCount(0)-collections);
                }
            }
            else
            {
                var full = File.ReadAllText(Path.Combine(args[1], "machines.test.config"));
                foreach (string snapshot in new[] { "testVideo.z80", "zexall.sna", "testOutFe.z80" })
                { var vm = Create(full); Fingerprint(vm,snapshot,snapshot,0); vm.BusManager.Disconnect(); }
                foreach (var profile in new[] {
                    "<Bus><Device type='ZXMAK2.Hardware.Spectrum.UlaSpectrum48' /><Device type='ZXMAK2.Hardware.Spectrum.MemorySpectrum48' /></Bus>",
                    "<Bus><Device type='ZXMAK2.Hardware.Spectrum.UlaSpectrum128' /><Device type='ZXMAK2.Hardware.Spectrum.MemorySpectrum128' /></Bus>" })
                { var vm = Create(profile); Fingerprint(vm,vm.BusManager.FindDevice<IUlaDevice>().GetType().Name,null,0); vm.BusManager.Disconnect(); }
                var bus = EvoBus(args[2]);
                var evoVm = Create(bus.OuterXml);
                foreach (int rate in new[] { 1,2,4 })
                {
                    if (rate != 1) Reload(evoVm, bus);
                    Fingerprint(evoVm,"PentEvo-rate-" + rate,null,rate);
                }
                // Same bus now switches from optional timing interfaces to none,
                // then back: catch stale cached references, not just fresh objects.
                var plain = new XmlDocument(); plain.LoadXml(full);
                Reload(evoVm, plain.DocumentElement); Fingerprint(evoVm,"switch-to-Pentagon",null,0);
                Reload(evoVm, bus); Fingerprint(evoVm,"switch-back-Evo",null,2);
                evoVm.BusManager.Disconnect();
            }
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
