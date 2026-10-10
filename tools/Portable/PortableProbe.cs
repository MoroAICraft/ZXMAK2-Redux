using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using ZXMAK2;
using ZXMAK2.Dependency;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Hardware;
using ZXMAK2.Hardware.Evo;
using ZXMAK2.Hardware.General;
using ZXMAK2.Hardware.Circuits.Ata;
using ZXMAK2.Hardware.Circuits.SecureDigital;
using ZXMAK2.Host.Entities;
using ZXMAK2.Host.Interfaces;
using ZXMAK2.Host.WinForms.Services;
using ZXMAK2.Host.Presentation;
using ZXMAK2.Model.Disk;
using ZXMAK2.Serializers;

internal static class PortableProbe
{
    static int checks;
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    sealed class Query : IUserQuery, IActionUserQuery
    {
        public DlgResult Result = DlgResult.Yes; public int Calls;
        public DlgResult ShowActions(string m,string c,string a,string b,string x) { Calls++;return Result; }
        public DlgResult Show(string m,string c,DlgButtonSet b,DlgIcon i) { Calls++;return Result; }
        public object ObjectSelector(object[] v,string c) { return null; }
        public bool QueryText(string c,string t,ref string v) { return false; }
        public bool QueryValue(string c,string t,string f,ref int v,int min,int max) { return false; }
    }
    sealed class Messages : IUserMessage
    {
        public int Errors;
        public void ErrorDetails(Exception error) { Errors++; }
        public void Error(Exception error) { Errors++; }
        public void Error(string text,params object[] values) { Errors++; }
        public void Warning(Exception error) { Errors++; }
        public void Warning(string text,params object[] values) { Errors++; }
        public void Info(string text,params object[] values) { }
    }
    sealed class Host : IHostService
    {
        public IHostKeyboard Keyboard {get{return null;}} public IHostMouse Mouse {get{return null;}}
        public IHostJoystick Joystick {get{return null;}} public SyncSource SyncSource {get;set;}
        public int SampleRate {get{return 44100;}} public bool IsCaptured {get{return false;}}
        public IMediaRecorder MediaRecorder {get;set;}
        public bool CheckSyncSourceSupported(SyncSource value) {return true;}
        public void PushFrame(IFrameInfo info,IFrameVideo video,IFrameSound sound) { }
        public void CancelPush() { } public void Capture() { } public void Uncapture() { } public void Dispose() { }
    }
    static void Reject(Action action, string message)
    {
        bool rejected = false;
        try { action(); } catch (IOException) { rejected = true; }
        Check(rejected, message);
    }
    static byte[] Sector(Stream stream, long index)
    {
        var bytes = new byte[512]; stream.Position = index * 512;
        Check(stream.Read(bytes, 0, bytes.Length) == 512, "short sector"); return bytes;
    }
    static byte FileByte(string path, long position)
    {
        using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
        {file.Position=position;return (byte)file.ReadByte();}
    }
    static void GuestWrite(HostFolderFat32Stream stream, string name, byte value)
    {
        var boot = Sector(stream, 0);
        var data = BitConverter.ToUInt16(boot,14) + (long)boot[16] * BitConverter.ToUInt32(boot,36);
        var root = Sector(stream, data);
        var parts = name.Split('.'); var shortName = parts[0].PadRight(8) + parts[1].PadRight(3);
        for (int offset = 0; offset < 512; offset += 32)
        {
            if (System.Text.Encoding.ASCII.GetString(root,offset,11) != shortName) continue;
            uint cluster = (uint)(BitConverter.ToUInt16(root,offset+20) << 16) | BitConverter.ToUInt16(root,offset+26);
            var index = data + (cluster - 2) * boot[13]; var sector = Sector(stream,index); sector[0] = value;
            stream.Position = index * 512; stream.Write(sector,0,sector.Length); return;
        }
        throw new Exception("test FAT entry missing");
    }
    static HostFolderFat32Stream Folder(ZsdPentEvo sd) { return ((SdCard)typeof(ZsdPentEvo).GetField("card",Private).GetValue(sd)).HostFolderStream; }
    static XmlElement Node(string xml) { var doc = new XmlDocument();doc.LoadXml(xml);return doc.DocumentElement; }
    static void Paths(string external)
    {
        Check(PortablePaths.Root == AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar), "root follows DLL rather than EXE");
        var path = Path.Combine(PortablePaths.DataDirectory("Temp"),"relative-test.txt");
        var stored = PortablePaths.Store(path); Check(!Path.IsPathRooted(stored), "internal path became absolute");
        var cwd = Environment.CurrentDirectory;
        try { Environment.CurrentDirectory = external; PortablePaths.WriteAtomic(stored, s => s.WriteByte(0x41));
            Check(File.ReadAllBytes(path)[0] == 0x41 && !File.Exists(Path.Combine(external,stored)), "relative save used CWD"); }
        finally { Environment.CurrentDirectory = cwd; }
        Reject(() => PortablePaths.WriteAtomic(Path.Combine(external,"forbidden.bin"),s => s.WriteByte(1)),"external save allowed");
        Check(!PortablePaths.IsInternal(Path.Combine(PortablePaths.Root,"..","outside.bin")),"traversal allowed");
        PortablePaths.AcquireInstance(); try { Reject(PortablePaths.AcquireInstance,"duplicate instance lock allowed"); }
        finally { PortablePaths.ReleaseInstance(); }
        Check(PortablePaths.ConfigFile("machines.config").Contains("Config"), "machine config did not move");
        Check(File.Exists(Path.Combine(PortablePaths.Root,"boot.zip")) && File.Exists(Path.Combine(PortablePaths.Root,"ROMS.PAK")),"root resources missing");
        Check(File.Exists(Path.Combine(PortablePaths.Root,"Docs","Help","index.html")),"local help missing");
    }
    static void Folders(string external, Query query)
    {
        var source = Path.Combine(external,"folder");Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source,"TEST.TRD"),"ORIGINAL DATA");File.WriteAllText(Path.Combine(source,"NV.EXT"),"OLD");
        var sd = new ZsdPentEvo();sd.MountHostFolder(source);
        try
        {
            GuestWrite(Folder(sd),"TEST.TRD",(byte)'S');
            File.WriteAllText(Path.Combine(source,"NV.EXT"),"PC NEW");File.WriteAllText(Path.Combine(source,"ADD.BIN"),"ADDED");
            Check(sd.RefreshMountedFolder() >= 0,"external save refresh failed");
            var saved = sd.ConfiguredSourcePath;
            Check(PortablePaths.IsInternal(saved) && saved != source,"external folder did not switch locally");
            Check(File.ReadAllText(Path.Combine(source,"TEST.TRD")) == "ORIGINAL DATA","external folder overwritten");
            Check(File.ReadAllText(Path.Combine(saved,"TEST.TRD")) == "SRIGINAL DATA","guest save missing");
            Check(File.ReadAllText(Path.Combine(saved,"NV.EXT")) == "PC NEW" && File.Exists(Path.Combine(saved,"ADD.BIN")),"independent PC edits missing");
            var xml=Node("<Device/>");sd.SaveConfigXml(xml);Check(!Path.IsPathRooted(xml.GetAttribute("sdPath")),"saved folder descriptor absolute");
            GuestWrite(Folder(sd),"TEST.TRD",(byte)'C');query.Result=DlgResult.Cancel;
            Check(sd.RefreshMountedFolder()<0 && sd.HasPendingChanges,"cancel lost local session");
            query.Result=DlgResult.No;Check(sd.RefreshMountedFolder()>=0 && File.ReadAllBytes(Path.Combine(saved,"TEST.TRD"))[0]=='S',"discard wrote data");
        }
        finally { query.Result=DlgResult.No;sd.EjectCard();sd.BusDisconnect(); }
        using (var folder = new HostFolderFat32Stream(source))
        {
            GuestWrite(folder,"TEST.TRD",(byte)'X'); File.WriteAllText(Path.Combine(source,"TEST.TRD"),"PC CONFLICT!!");
            Reject(() => folder.CommitChanges(),"same-file external conflict was overwritten");
            Check(folder.HasChanges && File.ReadAllText(Path.Combine(source,"TEST.TRD"))=="PC CONFLICT!!","conflict lost source or journal");
        }
        using (var folder = new HostFolderFat32Stream(source))
        {
            GuestWrite(folder,"TEST.TRD",(byte)'X');
            Reject(()=>folder.CommitChanges(ignored=>{throw new IOException("build failure");}),"failed remount accepted");
            Check(folder.SourceFolder==source && folder.HasChanges,"failed remount lost external source/session");
            Check(File.ReadAllText(Path.Combine(source,"TEST.TRD"))=="PC CONFLICT!!","rollback touched external source");
        }
        query.Result=DlgResult.Yes;
    }
    static void Images(string external)
    {
        var path=Path.Combine(external,"card.img"); File.WriteAllBytes(path,new byte[4096]);
        using(var card = new SdImageSessionStream(File.OpenRead(path),path))
        {
            card.Position=3;card.WriteByte(0x52);card.CommitChanges();
            Check(card.ImagePath!=path && PortablePaths.IsInternal(card.ImagePath),"SD image did not switch to local copy");
            Check(FileByte(path,3)==0 && FileByte(card.ImagePath,3)==0x52,"SD original overwritten or save lost");
            card.Position=4;card.WriteByte(0x53);card.CommitChanges();Check(FileByte(card.ImagePath,4)==0x53,"second local image save failed");
        }
        var info=new PhysicalDeviceInfo { filename=path };var ata=new AtaPasser();
        try { Check(ata.Open(info,false),"external ATA mount failed");Check(ata.ImagePath==path,"ATA copied on mount");
            Check(ata.Seek(0) && ata.WriteSector(Enumerable.Repeat((byte)0x66,512).ToArray(),0),"ATA write failed");ata.Flush();
            Check(PortablePaths.IsInternal(ata.ImagePath) && File.ReadAllBytes(path)[0]==0,"ATA original modified");
            Check(FileByte(ata.ImagePath,0)==0x66,"ATA local write missing"); }
        finally { ata.Dispose(); }
        var descriptor=new AtaDeviceInfo();descriptor.ConfigureImage(path,false);
        var local=Path.Combine(PortablePaths.DataDirectory("Temp"),"descriptor.vmide");descriptor.Save(local);
        var internalImage=Path.Combine(PortablePaths.DataDirectory("Temp"),"internal.img");File.WriteAllBytes(internalImage,new byte[1024]);
        descriptor.ConfigureImage(internalImage,false);descriptor.Save(local);Check(File.ReadAllText(local).Contains("Data\\Temp\\internal.img"),"IDE internal path not relative");
    }
    static void Missing()
    {
        var missing=Path.Combine(PortablePaths.Root,"missing-media","none.img");var sd=new ZsdPentEvo();
        sd.LoadConfigXml(Node("<Device sdSource='Image' sdPath='missing-media/none.img'/>"));
        typeof(ZsdPentEvo).GetMethod("RestoreConfiguredCard",Private).Invoke(sd,null);
        Check(!sd.IsMediaMounted && sd.RestoreWarning==null && sd.ConfiguredImageFileName==missing,"missing SD is not a quiet empty slot");sd.BusDisconnect();
        var fdd=new FddController();fdd.LoadConfigXml(Node("<Device><Drive index='0' inserted='true' readOnly='false' fileName='missing-media/none.trd'/></Device>"));
        Check(!fdd.IsDriveMounted(0),"missing floppy marked inserted");var xml=Node("<Device/>");fdd.SaveConfigXml(xml);
        Check(xml.SelectSingleNode("Drive[@index='0']")!=null,"missing floppy descriptor erased");
    }
    static void Settings()
    {
        var path=PortablePaths.ConfigFile("interface.xml");var original=File.Exists(path)?File.ReadAllBytes(path):null;
        try { var first=new SettingService();first.WindowWidth=1234;first.WindowHeight=567;first.RenderNoBorder=true;
            var second=new SettingService();Check(second.WindowWidth==1234 && second.WindowHeight==567 && second.RenderNoBorder,"portable interface settings lost"); }
        finally { if(original!=null)File.WriteAllBytes(path,original);else if(File.Exists(path))File.Delete(path); }
    }
    static void Models()
    {
        var templates=new MachinesConfig();templates.Load();
        var spectrum=new Spectrum();spectrum.Init();
        var path=Path.Combine(PortablePaths.DataDirectory("Temp\\Models"),"machine.vmz");
        typeof(BusManager).GetProperty("MachineFile",Private).SetValue(spectrum.BusManager,path,null);
        foreach(var name in templates.GetNames().Concat(new[]{"ZX-Evo BSconf","ZX Spectrum 48","ZX Spectrum 128","ZX-Evo BSconf"}))
        {
            spectrum.BusManager.LoadConfigXml(templates.GetConfig(name));
            spectrum.DebugReset();
            Check(spectrum.BusManager.FindDevice<IUlaDevice>()!=null,"ULA missing after model switch: "+name);
            var memory=spectrum.BusManager.FindDevice<IMemoryDevice>();
            Check(memory!=null && memory.RomPages!=null && memory.RomPages.Any(p=>p.Any(b=>b!=0 && b!=255)),"ROM missing: "+name);
            spectrum.ExecuteFrame();Check(spectrum.BusManager.SoundFrame.GetBuffer().Length>0,"audio frame missing: "+name);
            Console.WriteLine("MODEL PASS: "+name);
        }
        spectrum.BusManager.Disconnect();
    }
    static void NeoGs(Query query)
    {
        var board=new NeoGsDevice();var card=new SdCard();
        var source=Path.Combine(PortablePaths.DataDirectory("Temp"),"neo.img");var bytes=new byte[4096];bytes[510]=0x55;bytes[511]=0xAA;bytes[450]=0x0B;bytes[454]=1;bytes[458]=7;File.WriteAllBytes(source,bytes);card.Open(source);
        typeof(NeoGsDevice).GetField("m_sdCard",Private).SetValue(board,card);
        try
        {
            card.ImageSessionStream.WriteByte(0x77);query.Result=DlgResult.Cancel;
            Check(!board.PrepareForMediaChange() && board.HasPendingChanges,"NeoGS cancel lost session");
            query.Result=DlgResult.No;Check(board.PrepareForMediaChange() && !board.HasPendingChanges && FileByte(source,0)==0,"NeoGS discard saved changes");
            card.ImageSessionStream.Position=0;card.ImageSessionStream.WriteByte(0x78);query.Result=DlgResult.Yes;
            Check(board.PrepareForMediaChange() && FileByte(source,0)==0x78,"NeoGS save failed");
        }
        finally { card.Close(); }
    }
    static void Resets(Query query)
    {
        var names=new[]{"ZXMAK2.vmz","ZXMAK2.vmide","ZXMAK2.cmos","ZXMAK2.nvram"};
        var original=names.Select(n=>File.Exists(Path.Combine(PortablePaths.Root,n))?File.ReadAllBytes(Path.Combine(PortablePaths.Root,n)):null).ToArray();
        VirtualMachine vm=null;
        try
        {
            vm=new VirtualMachine(new Host(),null);vm.Init();vm.SaveConfigAs(Path.Combine(PortablePaths.Root,"ZXMAK2.vmz"));
            vm.Bus.LoadConfigXml(new MachinesConfigFixture().Evo());
            var model=(MainViewModel)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(MainViewModel));
            typeof(MainViewModel).GetField("m_vm",Private).SetValue(model,vm);
            var messages=new Messages();typeof(MainViewModel).GetField("m_userMessage",Private).SetValue(model,messages);
            vm.DoReset();Check(File.Exists(Path.Combine(PortablePaths.Root,"ZXMAK2.vmz")),"warm reset removed VMZ");
            model.ResetCmosState();Check(File.Exists(Path.Combine(PortablePaths.Root,"ZXMAK2.cmos")),"CMOS reset missing active file");
            Check(model.PrepareFactoryReset(),"factory reset failed");vm=null;
            Check(names.All(n=>!File.Exists(Path.Combine(PortablePaths.Root,n))),"factory reset missed root state files");
            Check(File.Exists(Path.Combine(PortablePaths.Root,"ROMS.PAK")) && File.Exists(Path.Combine(PortablePaths.Root,"boot.zip")),"factory reset deleted resources");
            Check(Directory.Exists(Path.Combine(PortablePaths.Root,"Data","Saves")),"factory reset deleted saves");
            Check(File.Exists(PortablePaths.ConfigFile("machines.config")),"factory reset deleted templates");
        }
        finally
        {
            if(vm!=null)vm.Dispose();
            for(int i=0;i<names.Length;i++) {var path=Path.Combine(PortablePaths.Root,names[i]);if(original[i]!=null)File.WriteAllBytes(path,original[i]);else if(File.Exists(path))File.Delete(path);}
        }
    }
    static void ImportedConfig(string external)
    {
        var source=Path.Combine(external,"profile.vmz");
        File.WriteAllText(source,"<VirtualMachine><Info/><Bus><Device type='ZXMAK2.Hardware.Spectrum.UlaSpectrum48'/><Device type='ZXMAK2.Hardware.Spectrum.MemorySpectrum48'/><Device type='ZXMAK2.Hardware.General.FddController'><Drive index='0' inserted='true' readOnly='false' fileName='missing.trd'/></Device></Bus></VirtualMachine>");
        var bytes=File.ReadAllBytes(source);
        File.WriteAllText(Path.ChangeExtension(source,".vmide"),"<IdeDiskDescriptor><Image fileName='relative.img' isReadOnly='false'/><Geometry cylinders='1' heads='1' sectors='2' lba='2'/></IdeDiskDescriptor>");
        var sidecar=File.ReadAllBytes(Path.ChangeExtension(source,".vmide"));
        using(var vm=new VirtualMachine(new Host(),null))
        {
            vm.Init();vm.OpenConfig(source);vm.SaveConfig();
            var saved=(string)typeof(VirtualMachine).GetField("m_configFileName",Private).GetValue(vm);
            Check(PortablePaths.IsInternal(saved) && saved!=source,"external VMZ not imported locally");
            Check(File.ReadAllText(Path.ChangeExtension(saved,".vmide")).Contains(Path.Combine(external,"relative.img")),"imported IDE relative reference rebased incorrectly");
            Check(bytes.SequenceEqual(File.ReadAllBytes(source)) && sidecar.SequenceEqual(File.ReadAllBytes(Path.ChangeExtension(source,".vmide"))),"external config/sidecar overwritten");
        }
    }
    static void Floppy(string external,Query query)
    {
        var source=Path.Combine(external,"disk.trd");File.WriteAllBytes(source,new byte[655360]);
        var disk=new DiskImage();disk.Init(3500000/5);var manager=new DiskLoadManager(disk);
        manager.OpenFileName(source,false);
        var sector=new byte[256];sector[0]=0x5A;disk.WriteLogicalSector(1,0,1,sector);
        var dirty=(ModifyFlag)1;disk.ModifyFlag=dirty;disk.Disconnect();
        Check(PortablePaths.IsInternal(disk.FileName) && FileByte(source,8192)==0,"external floppy overwritten or path not local");
        Check(FileByte(disk.FileName,8192)==0x5A,"external floppy local copy missing guest data");
        var oldPath=disk.FileName;disk.ModifyFlag=dirty;
        using(var lease=new FileStream(oldPath,FileMode.Open,FileAccess.Read,FileShare.None))
            Reject(()=>manager.SaveFileName(oldPath),"locked floppy save accepted");
        Check(disk.FileName==oldPath && disk.ModifyFlag==dirty,"failed atomic save lost dirty flag or path");
    }
    sealed class MachinesConfigFixture { public XmlNode Evo() {var config=new MachinesConfig();config.Load();return config.GetConfig("ZX-Evo BSconf");} }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            if(args.Length>0 && args[0]=="models") { Models();Console.WriteLine("PASS model template/frame/switch: {0} checks",checks);return 0; }
            if(args.Length>0 && args[0]=="relocated") { Paths(Directory.GetParent(PortablePaths.Root).FullName);Missing();Settings();
                Check(RomPack.GetRomData("~mapping.xml").Length>0,"relocated ROMS.PAK unreadable");
                var machines=new MachinesConfig();machines.Load();Check(machines.GetConfig("ZX-Evo BSconf")!=null,"relocated Evo template missing");
                Console.WriteLine("PASS relocated root/resources/settings/missing media: {0} checks",checks);return 0; }
            var external=Path.Combine(Directory.GetParent(PortablePaths.Root).FullName,"diagnostics","external-fixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(external);
            var query=new Query();Locator.Resolve<IResolver>().RegisterInstance<IUserQuery>(null,query);
            Paths(external);Folders(external,query);Images(external);Missing();Settings();NeoGs(query);Resets(query);ImportedConfig(external);Floppy(external,query);
            Console.WriteLine("PASS portable paths/local saves/PC merge/conflict/rollback/missing media/settings: {0} checks",checks);return 0;
        }
        catch(Exception error) { Console.WriteLine(error);return 1; }
    }
}
