// v90 direct-write policy. Only disposable, purpose-created external fixtures.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Diagnostics;
using System.Runtime.Serialization;
using ZXMAK2;
using ZXMAK2.Hardware.Evo;
using ZXMAK2.Hardware.Circuits.SecureDigital;
using ZXMAK2.Host.Entities;
using ZXMAK2.Engine;
using ZXMAK2.Host.Presentation;

static partial class PortableFolderProbe
{
    static void LargeDirectFixture(Query query)
    {
        string root=Path.Combine(Path.GetDirectoryName(PortablePaths.Root),"large-direct-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root,"TEST.TRD"),"ORIGINAL");string huge=Path.Combine(root,"HUGE.BIN");
        using(var file=File.Create(huge))file.SetLength(3L*1024*1024*1024);
        for(int directory=0;directory<10;directory++){string sub=Path.Combine(root,"dir"+directory);Directory.CreateDirectory(sub);for(int f=0;f<1000;f++)using(File.Create(Path.Combine(sub,"F"+f.ToString("D6")+".BIN"))) {}}
        int copies=Copies();var watch=Stopwatch.StartNew();
        using(var vm=new VirtualMachine(new Host(),null))
        {
            vm.Spectrum.BusManager.Init(vm.Spectrum,true);var sd=vm.Bus.FindDevice<ZsdPentEvo>();Check(sd!=null,"large fixture missing SD");sd.MountHostFolder(root);
            File.WriteAllText(Path.Combine(root,"PC.CFG"),"PC LATEST");Guest(Card(sd).HostFolderStream,"TEST.TRD",(byte)'S');
            vm.SaveConfigAs(Path.Combine(PortablePaths.DataDirectory("Temp"),"large-direct.vmz"));
            var model=(MainViewModel)FormatterServices.GetUninitializedObject(typeof(MainViewModel));typeof(MainViewModel).GetField("m_vm",F).SetValue(model,vm);var errors=new Messages();typeof(MainViewModel).GetField("m_userMessage",F).SetValue(model,errors);
            query.Next=DlgResult.Yes;vm.DoRun();
            // An unchanged 3-GiB payload cannot be read/copied while locked.
            // Direct selective Save must still succeed using only FAT metadata.
            using(var lease=File.Open(huge,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
                Check(model.PrepareClose()&&!vm.IsRunning&&errors.Errors==0,"large direct close/save tried to copy/read unchanged payload or failed");
            Check(sd.ConfiguredSourcePath==root&&First(Path.Combine(root,"TEST.TRD"))=='S',"large direct save switched source/lost guest");
            Check(File.ReadAllText(Path.Combine(root,"PC.CFG"))=="PC LATEST"&&new FileInfo(huge).Length==3L*1024*1024*1024,"large source/PC addition changed");
            Check(Copies()==copies,"large save cloned tree");
            Console.WriteLine("PASS 10002-file / 3-GiB disposable folder: real PrepareClose/background direct Save, unchanged huge file locked, no clone; {0:F3}s",watch.Elapsed.TotalSeconds);
        }
    }
    static int Copies()
    {
        string p=Path.Combine(PortablePaths.Root,"Data","Saves","SD-Folders");
        return Directory.Exists(p)?Directory.GetDirectories(p).Length:0;
    }
    static XmlElement FolderConfig(string p){var d=new XmlDocument();d.LoadXml("<Device sdSource='HostFolder'/>");d.DocumentElement.SetAttribute("sdPath",p);return d.DocumentElement;}
    static void GuestNamed(HostFolderFat32Stream s,string name,byte value)
    {
        var root=typeof(HostFolderFat32Stream).GetField("m_root",F).GetValue(s);
        foreach(var node in (IEnumerable)root.GetType().GetField("Children").GetValue(root))
            if((string)node.GetType().GetField("Name").GetValue(node)==name){uint c=(uint)node.GetType().GetField("StartCluster").GetValue(node);long data=(long)typeof(HostFolderFat32Stream).GetField("m_dataStartSector",F).GetValue(s);var b=Sector(s,0);long n=data+(c-2)*b[13];var bytes=Sector(s,n);bytes[0]=value;s.Position=n*512;s.Write(bytes,0,512);return;}
        throw new Exception("named fixture missing");
    }
    static void DirectCases(Query query)
    {
        string root=Path.Combine(Path.GetDirectoryName(PortablePaths.Root),"direct-fixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        string test=Path.Combine(root,"TEST.TRD"),pc=Path.Combine(root,"PC.CFG");File.WriteAllText(test,"ORIGINAL");File.WriteAllText(pc,"OLD");
        int copies=Copies();var sd=new ZsdPentEvo();sd.MountHostFolder(root);int prompts=query.Calls;
        File.WriteAllText(pc,"PC NEW");Check(sd.RefreshMountedFolder()>=0&&query.Calls==prompts,"PC-only Refresh prompted");
        Guest(Card(sd).HostFolderStream,"TEST.TRD",(byte)'S');File.WriteAllText(pc,"PC NEWER");File.WriteAllText(Path.Combine(root,"ADDED.BIN"),"PC ADD");
        query.Next=DlgResult.Yes;Check(sd.RefreshMountedFolder()>=0,"Save Refresh failed");
        Check(sd.ConfiguredSourcePath==root&&First(test)=='S',"save switched folder or did not write selected source");Check(File.ReadAllText(pc)=="PC NEWER"&&File.Exists(Path.Combine(root,"ADDED.BIN")),"independent PC changes overwritten");Check(Copies()==copies,"full SD folder clone appeared");
        Guest(Card(sd).HostFolderStream,"TEST.TRD",(byte)'T');var old=Card(sd);query.Next=DlgResult.Cancel;Check(sd.RefreshMountedFolder()<0&&Object.ReferenceEquals(old,Card(sd))&&sd.HasPendingChanges,"Cancel lost session");
        query.Next=DlgResult.No;Check(sd.RefreshMountedFolder()>=0&&First(test)=='S',"discard wrote guest changes");
        Guest(Card(sd).HostFolderStream,"TEST.TRD",(byte)'X');File.WriteAllText(test,"PC CHANGED");query.Next=DlgResult.Yes;bool conflict=false;try{sd.RefreshMountedFolder();}catch(IOException){conflict=true;}
        Check(conflict&&sd.HasPendingChanges&&File.ReadAllText(test)=="PC CHANGED","same-file PC version silently overwritten");query.Next=DlgResult.No;Check(sd.RefreshMountedFolder()>=0,"conflict discard failed");
        Guest(Card(sd).HostFolderStream,"TEST.TRD",(byte)'E');query.Next=DlgResult.Yes;Check(sd.PrepareForMediaChange()&&First(test)=='E'&&sd.ConfiguredSourcePath==root,"Exit/Eject Save changed source path");sd.BusDisconnect();
        var restored=new ZsdPentEvo();restored.LoadConfigXml(FolderConfig(root));typeof(ZsdPentEvo).GetMethod("RestoreConfiguredCard",F).Invoke(restored,null);Check(restored.IsMediaMounted&&First(test)=='E',"restart failed to reread source");restored.BusDisconnect();
        Directory.Move(root,root+"-moved");prompts=query.Calls;var missing=new ZsdPentEvo();missing.LoadConfigXml(FolderConfig(root));typeof(ZsdPentEvo).GetMethod("RestoreConfiguredCard",F).Invoke(missing,null);Check(!missing.IsMediaMounted&&missing.RestoreWarning==null&&query.Calls==prompts&&!Directory.Exists(root),"missing startup folder not quiet/empty");missing.BusDisconnect();

        root=root+"-moved";string longName=new string('z',240)+".CFG";string longFile=Path.Combine(root,longName);File.WriteAllText(PortablePaths.IoPath(longFile),"LONG NAME");
        using(var stream=new HostFolderFat32Stream(root)){GuestNamed(stream,longName,(byte)'Q');stream.CommitChanges();Check(File.ReadAllText(PortablePaths.IoPath(longFile))=="QONG NAME","long file name commit failed");Check(stream.SourceFolder==root,"long name save switched source");}
        sd=new ZsdPentEvo();sd.MountHostFolder(root);var info=Sector(Card(sd).HostFolderStream,1);info[488]^=1;Card(sd).HostFolderStream.Position=512;Card(sd).HostFolderStream.Write(info,0,512);prompts=query.Calls;
        Check(sd.PrepareForMediaChange()&&query.Calls==prompts,"FSInfo-only close prompted");sd.BusDisconnect();Check(Copies()==copies,"unexpected clone after lifecycle");
        string atomic=Path.Combine(PortablePaths.DataDirectory("Temp"),new string('a',251)+".bin");
        PortablePaths.WriteAtomic(atomic,s=>s.WriteByte(1));PortablePaths.WriteAtomic(atomic,s=>s.WriteByte(2));Check(First(PortablePaths.IoPath(atomic))==2,"255-character atomic save/backup failed");
        Console.WriteLine("PASS direct selected-folder Save/Refresh/PC edits/conflict/Cancel/discard/Exit/restart/missing/244-char filename/no clone");
    }
}
