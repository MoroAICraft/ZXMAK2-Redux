using System;
using System.IO;
using System.Reflection;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Engine.Interfaces;

internal static class RoutingSafetyProbe
{
    private static readonly BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    private static int checks;
    private static void Check(bool condition,string text) {if(!condition)throw new Exception(text);checks++;}
    private class LegacyClock : BusDeviceBase,ICpuClock
    {
        public int Rate=2; public int CpuClockMultiplier {get{return Rate;}}
        public int MaxCpuClockMultiplier {get{return 4;}}
        public override void BusInit(IBusManager bus) {} public override void BusConnect() {}
        public override void BusDisconnect() {}
    }
    private class SyncClock : LegacyClock,ICpuRefreshClock
    {
        public int MemoryCalls,RefreshCalls,PortCalls;
        public int CompleteMemoryAccess(ushort address,CpuMemoryAccess access,long tact,ref byte value)
        {MemoryCalls++;return 0;}
        public void InvalidateMemoryBuffer() {}
        public int GetPortWait(ushort address) {PortCalls++;return 0;}
        public void LatchClockAtRefresh() {RefreshCalls++;}
    }
    private static void Hooks(Spectrum vm,bool present)
    {
        var events=((IBusManager)vm.BusManager).Events;
        foreach(string name in new[] {"SyncCpuClock","CompleteMemoryAccess","InvalidateMemoryBuffer","ApplyPortTiming"})
            Check((events.GetType().GetField(name,F).GetValue(events)!=null)==present,"Hook "+name);
        Check((vm.CPU.REFRESH!=null)==present,"Refresh hook");
    }
    private static void Clocks(string xml)
    {
        var vm=OptimizationProbe.Create(xml); Hooks(vm,false);
        vm.BusManager.Disconnect(); var legacy=new LegacyClock();vm.BusManager.Add(legacy);vm.BusManager.Connect();
        Hooks(vm,false); OptimizationProbe.Prepare(vm,null,0); vm.ExecuteFrame();
        long t=vm.CPU.Tact;legacy.Rate=4;vm.ExecuteFrame();Check(vm.CPU.Tact>t,"Legacy clock stopped");
        vm.BusManager.Disconnect();vm.BusManager.Remove(legacy);
        var clock=new SyncClock();vm.BusManager.Add(clock);vm.BusManager.Connect();Hooks(vm,true);
        OptimizationProbe.Prepare(vm,null,0);vm.ExecuteFrame();
        Check(clock.MemoryCalls>0&&clock.RefreshCalls>0&&clock.PortCalls>0,"Active timing callbacks absent");
        int calls=clock.MemoryCalls;clock.Rate=4;vm.ExecuteFrame();Hooks(vm,true);
        Check(clock.MemoryCalls==calls,"Maximum rate unexpectedly applies memory completion");
        clock.Rate=2;vm.ExecuteFrame();Check(clock.MemoryCalls>calls,"Callbacks did not resume after live rate change");
        vm.BusManager.Disconnect();Hooks(vm,false);vm.BusManager.Remove(clock);vm.BusManager.Connect();Hooks(vm,false);
        vm.BusManager.Disconnect();
    }
    private static void Tables(string xml)
    {
        var vm=OptimizationProbe.Create(xml);var events=((IBusManager)vm.BusManager).Events;
        events.GetType().GetMethod("Clear").Invoke(events,null);
        string[] fields={"m_mapReadMemoryM1","m_mapReadMemory","m_mapReadPort","m_mapWriteMemory","m_mapWritePort","m_mapReadNoMreq","m_mapWriteNoMreq"};
        var old=new Array[fields.Length];for(int i=0;i<fields.Length;i++)old[i]=(Array)events.GetType().GetField(fields[i],F).GetValue(events);
        int count=0; ushort addr=0x8123; int order=0;
        events.SubscribeRdMemM1(65535,addr,delegate(ushort a,ref byte v){count++;v=0x5A;});
        events.SubscribeRdMem(65535,addr,delegate(ushort a,ref byte v){count++;v=0x6B;});
        events.SubscribeWrMem(65535,addr,delegate(ushort a,byte v){count++;});
        events.SubscribeRdIo(65535,addr,delegate(ushort a,ref byte v,ref bool handled){count++;v=0x7C;handled=true;order=1;});
        events.SubscribeRdIo(65535,addr,delegate(ushort a,ref byte v,ref bool handled){Check(order==1&&handled&&v==0x7C,"Multicast read ordering/handled changed");order=2;});
        events.SubscribeWrIo(65535,addr,delegate(ushort a,byte v,ref bool handled){count++;handled=true;});
        events.SubscribeRdNoMreq(65535,addr,delegate(ushort a){count++;});
        events.SubscribeWrNoMreq(65535,addr,delegate(ushort a){count++;});
        Check(vm.CPU.RDMEM_M1(addr)==0x5A,"M1 routing");Check(vm.CPU.RDMEM(addr)==0x6B,"Read routing");
        vm.CPU.WRMEM(addr,1);Check(vm.CPU.RDPORT(addr)==0x7C&&order==2,"IO routing");vm.CPU.WRPORT(addr,1);
        vm.CPU.RDNOMREQ(addr);vm.CPU.WRNOMREQ(addr);Check(count==7,"Routing count");
        events.GetType().GetMethod("Clear").Invoke(events,null);
        for(int i=0;i<fields.Length;i++) {
            var array=(Array)events.GetType().GetField(fields[i],F).GetValue(events);
            Check(ReferenceEquals(old[i],array),"Private map not reused");
            for(int a=0;a<65536;a++)Check(array.GetValue(a)==null,"Stale delegate in "+fields[i]);
        }
        vm.CPU.BUS=0xA5;Check(vm.CPU.RDMEM(addr)==0xA5&&vm.CPU.RDPORT(addr)==0xA5,"Cleared open bus changed");
        vm.CPU.WRMEM(addr,1);vm.CPU.WRPORT(addr,1);vm.CPU.RDNOMREQ(addr);vm.CPU.WRNOMREQ(addr);
        Check(count==7,"Old handler survived clear");vm.BusManager.Disconnect();
    }
    private static int Main(string[] args)
    {
        try {string xml=File.ReadAllText(Path.Combine(args[0],"machines.test.config"));Clocks(xml);Tables(xml);
            Console.WriteLine("PASS: "+checks+" routing/hook checks; no/legacy/live clock, reconnect, all 7 cleared maps, multicast/open bus");return 0;}
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
