using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Xml;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Interfaces;

// Diagnostic-only measurement. No sampled/profiled build is shipped.
internal static class PhaseCostProbe
{
    private static readonly BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
    private static long syncCalls, refreshCalls, memoryCalls, portCalls, invalidations;
    private static readonly Dictionary<string,long> boundaryTicks = new Dictionary<string,long>();
    private static void Boundary(EventManager events, string field)
    {
        var info = typeof(EventManager).GetField(field,F);
        var chain = (Action)info.GetValue(events); if (chain == null) return;
        Action wrapped = null;
        foreach (Action action in chain.GetInvocationList())
        {
            Action original = action;
            string label = original.Target.GetType().Name + "." + original.Method.Name;
            boundaryTicks[label] = 0;
            wrapped += delegate {
                long start = Stopwatch.GetTimestamp(); original();
                boundaryTicks[label] += Stopwatch.GetTimestamp()-start;
            };
        }
        info.SetValue(events,wrapped);
    }
    private static Spectrum Create(string path, string snapshot, int rate)
    {
        var vm = OptimizationProbe.Create(path);
        OptimizationProbe.Prepare(vm,snapshot,rate);
        return vm;
    }
    private static void Counters(Spectrum vm)
    {
        var events = ((IBusManager)vm.BusManager).Events;
        foreach (string name in new[] {"SyncCpuClock","InvalidateMemoryBuffer"})
        {
            var field = events.GetType().GetField(name,F); var original = (Action)field.GetValue(events);
            if (original == null) continue;
            if (name == "SyncCpuClock") field.SetValue(events,new Action(delegate {syncCalls++; original();}));
            else field.SetValue(events,new Action(delegate {invalidations++; original();}));
        }
        var portField = events.GetType().GetField("ApplyPortTiming",F);
        var port = (Action<ushort>)portField.GetValue(events);
        if (port != null) portField.SetValue(events,new Action<ushort>(delegate(ushort a) {portCalls++; port(a);}));
        var memoryField = events.GetType().GetField("CompleteMemoryAccess",F);
        Delegate memory = (Delegate)memoryField.GetValue(events);
        // Keep the strongly typed ref-byte signature, not DynamicInvoke per access.
        var observer = new MemoryObserver(memory);
        memoryField.SetValue(events,Delegate.CreateDelegate(memoryField.FieldType,observer,
            typeof(MemoryObserver).GetMethod("Call")));
        var refresh = vm.CPU.REFRESH;
        vm.CPU.REFRESH = delegate {refreshCalls++; if (refresh != null) refresh();};
    }
    private sealed class MemoryObserver
    {
        private delegate void Complete(ushort a,CpuMemoryAccess access,ref byte value);
        private readonly Complete original;
        public MemoryObserver(Delegate callback)
        { original = callback == null ? null : (Complete)Delegate.CreateDelegate(typeof(Complete),callback.Target,callback.Method); }
        public void Call(ushort a,CpuMemoryAccess access,ref byte value)
        {memoryCalls++; if(original != null) original(a,access,ref value);}
    }
    private static void Profile(string xml,string name,string snapshot,int rate)
    {
        var vm = Create(xml,snapshot,rate); for(int i=0;i<30;i++) vm.ExecuteFrame();
        boundaryTicks.Clear(); var events=(EventManager)((IBusManager)vm.BusManager).Events;
        Boundary(events,"m_beginFrame"); Boundary(events,"m_endFrame");
        long mixTicks=0; int gc0=GC.CollectionCount(0),gc2=GC.CollectionCount(2);
        var watch=Stopwatch.StartNew();
        for(int i=0;i<100;i++) {vm.ExecuteFrame(); long start=Stopwatch.GetTimestamp();
            vm.BusManager.SoundFrame.GetBuffer(); mixTicks+=Stopwatch.GetTimestamp()-start;}
        watch.Stop(); Console.WriteLine("PROFILE {0}: frame+mix={1}ms; mix={2:F3}ms; gc0={3}; gc2={4}",
            name,watch.ElapsedMilliseconds,mixTicks*1000.0/Stopwatch.Frequency,GC.CollectionCount(0)-gc0,GC.CollectionCount(2)-gc2);
        foreach(var item in boundaryTicks) Console.WriteLine("BOUNDARY subset {0}: {1:F3}ms",item.Key,item.Value*1000.0/Stopwatch.Frequency);
        syncCalls=refreshCalls=memoryCalls=portCalls=invalidations=0; Counters(vm);
        for(int i=0;i<20;i++) vm.ExecuteFrame();
        Console.WriteLine("COUNTS 20 frames {0}: sync={1}; refresh={2}; memory={3}; port={4}; invalidation={5}",
            name,syncCalls,refreshCalls,memoryCalls,portCalls,invalidations);
        vm.BusManager.Disconnect();
    }
    private static void IdentityExperiment(string xml,bool remove,int round)
    {
        var vm=Create(xml,"zexall.sna",0);
        if (vm.BusManager.FindDevice<ICpuClock>() != null) throw new Exception("Identity experiment requires NO clock controller");
        if(remove) {
            var events=((IBusManager)vm.BusManager).Events;
            foreach(string field in new[] {"SyncCpuClock","CompleteMemoryAccess","InvalidateMemoryBuffer","ApplyPortTiming"})
                events.GetType().GetField(field,F).SetValue(events,null);
            vm.CPU.REFRESH=null;
        }
        for(int i=0;i<100;i++) vm.ExecuteFrame(); var watch=Stopwatch.StartNew();
        for(int i=0;i<500;i++) vm.ExecuteFrame(); watch.Stop(); vm.BusManager.Disconnect();
        Console.WriteLine("IDENTITY experiment round={0} remove-noops={1} {2}ms",round,remove,watch.ElapsedMilliseconds);
    }
    private static int Main(string[] args)
    {
        try {
            Process.GetCurrentProcess().ProcessorAffinity=new IntPtr(1);
            Process.GetCurrentProcess().PriorityClass=ProcessPriorityClass.High;
            string full=File.ReadAllText(Path.Combine(args[0],"machines.test.config"));
            if (args.Length > 2 && args[2] == "setup") {
                var vm=Create(full,"zexall.sna",0);
                var config=new XmlDocument(); config.LoadXml(full);
                for(int round=-1;round<4;round++) {
                    int gc0=GC.CollectionCount(0),gc2=GC.CollectionCount(2);
                    var watch=Stopwatch.StartNew();
                    for(int i=0;i<20;i++) vm.BusManager.LoadConfigXml(config.DocumentElement);
                    watch.Stop();
                    if(round>=0) Console.WriteLine("SETUP round={0} 20-profile-reloads={1}ms gc0={2} gc2={3}",
                        round,watch.ElapsedMilliseconds,GC.CollectionCount(0)-gc0,GC.CollectionCount(2)-gc2);
                }
                vm.BusManager.Disconnect();return 0;
            }
            Profile(full,"Pentagon-zexall","zexall.sna",0);
            Profile(full,"Pentagon-video","testVideo.z80",0);
            Profile(OptimizationProbe.EvoBus(args[1]).OuterXml,"Evo14-RAM",null,4);
            Console.WriteLine("LIMIT: boundary timing is a subset, not all rendering; counters run separately. No ETW attribution.");
            for(int round=0;round<3;round++) {
                IdentityExperiment(full,round%2!=0,round);
                IdentityExperiment(full,round%2==0,round);
            }
            return 0;
        } catch(Exception ex) {Console.Error.WriteLine(ex);return 1;}
    }
}
