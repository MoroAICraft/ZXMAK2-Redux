using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using ZXMAK2;
using ZXMAK2.Dependency;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Hardware;
using ZXMAK2.Hardware.Evo;
using ZXMAK2.Hardware.General;
using ZXMAK2.Hardware.Circuits.Ata;
using ZXMAK2.Hardware.Circuits.SecureDigital;
using ZXMAK2.Host.Entities;
using ZXMAK2.Host.Interfaces;
using ZXMAK2.Host.WinForms.Services;

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
    [STAThread] static int Main(string[] args)
    {
        try
        {
            if(args.Length>0 && args[0]=="relocated") { Paths(PortablePaths.DataDirectory("Temp"));Missing();Settings();
                Check(RomPack.GetRomData("~mapping.xml").Length>0,"relocated ROMS.PAK unreadable");
                var machines=new MachinesConfig();machines.Load();Check(machines.GetConfig("ZX-Evo BSconf")!=null,"relocated Evo template missing");
                Console.WriteLine("PASS relocated root/resources/settings/missing media: {0} checks",checks);return 0; }
            var external=Path.Combine(Directory.GetParent(PortablePaths.Root).FullName,"diagnostics","external-fixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(external);
            var query=new Query();Locator.Resolve<IResolver>().RegisterInstance<IUserQuery>(null,query);
            Paths(external);Folders(external,query);Images(external);Missing();Settings();
            Console.WriteLine("PASS portable paths/local saves/PC merge/conflict/rollback/missing media/settings: {0} checks",checks);return 0;
        }
        catch(Exception error) { Console.WriteLine(error);return 1; }
    }
}
