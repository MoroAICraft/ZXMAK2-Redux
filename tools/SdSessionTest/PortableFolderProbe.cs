// v89 regression fixtures only. Never edit the user source or D: runtime.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using ZXMAK2;
using ZXMAK2.Dependency;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Hardware.Evo;
using ZXMAK2.Hardware.Circuits.SecureDigital;
using ZXMAK2.Host.Entities;
using ZXMAK2.Host.Interfaces;
using ZXMAK2.Host.Presentation;
using ZXMAK2.Host.WinForms.Views;
using ZXMAK2.Mvvm;

static class PortableFolderProbe
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    static int checks;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    sealed class Query:IUserQuery,IActionUserQuery
    {
        public DlgResult Next=DlgResult.Yes;public int Calls;
        public DlgResult Show(string m,string c,DlgButtonSet b,DlgIcon i){Calls++;return Next;}
        public DlgResult ShowActions(string m,string c,string a,string b,string d){Calls++;return Next;}
        public object ObjectSelector(object[] o,string c){return null;}
        public bool QueryText(string c,string t,ref string v){return false;}
        public bool QueryValue(string c,string t,string f,ref int v,int a,int b){return false;}
    }
    sealed class Host:IHostService
    {
        public IHostKeyboard Keyboard{get{return null;}}public IHostMouse Mouse{get{return null;}}public IHostJoystick Joystick{get{return null;}}
        public SyncSource SyncSource{get;set;}public int SampleRate{get{return 44100;}}public bool IsCaptured{get{return false;}}
        public IMediaRecorder MediaRecorder{get;set;}public bool CheckSyncSourceSupported(SyncSource s){return true;}
        public void PushFrame(IFrameInfo i,IFrameVideo v,IFrameSound s){Thread.Sleep(1);}public void CancelPush(){}public void Capture(){}public void Uncapture(){}public void Dispose(){}
    }
    sealed class Messages:IUserMessage
    {
        public int Errors;public void ErrorDetails(Exception e){Errors++;}public void Error(Exception e){Errors++;}public void Error(string f,params object[] a){Errors++;}
        public void Warning(Exception e){throw e;}public void Warning(string f,params object[] a){throw new Exception(f);}public void Info(string f,params object[] a){}
    }
    sealed class Success:CommandDelegate,ISuccessCommand
    {public Success(Func<bool> can):base(()=>{},can){}public event EventHandler ExecutedSuccessfully;}
    static SdCard Card(ZsdPentEvo sd){return (SdCard)typeof(ZsdPentEvo).GetField("card",F).GetValue(sd);}
    static byte[] Sector(Stream s,long n){s.Position=n*512;var b=new byte[512];Check(s.Read(b,0,512)==512,"sector read");return b;}
    static void Guest(Stream s,string file,byte value)
    {
        var b=Sector(s,0);long data=BitConverter.ToUInt16(b,14)+(long)b[16]*BitConverter.ToUInt32(b,36);
        var parts=file.Split('.');string shortName=parts[0].ToUpperInvariant().PadRight(8)+parts[1].ToUpperInvariant().PadRight(3);
        for(int sector=0;sector<b[13];sector++){var root=Sector(s,data+sector);
        for(int i=0;i<512;i+=32)if(System.Text.Encoding.ASCII.GetString(root,i,11)==shortName){uint c=(uint)(BitConverter.ToUInt16(root,i+20)<<16)|BitConverter.ToUInt16(root,i+26);long n=data+(c-2)*b[13];var bytes=Sector(s,n);bytes[0]=value;s.Position=n*512;s.Write(bytes,0,512);return;}}
        throw new Exception("fixture entry not found: "+file);
    }
    static byte First(string path){using(var f=File.OpenRead(path))return (byte)f.ReadByte();}
    static void RelativeAndMenu()
    {
        string path=Path.Combine(PortablePaths.DataDirectory("Temp"),"relative-folder");Directory.CreateDirectory(path);File.WriteAllText(Path.Combine(path,"TEST.TRD"),"ORIGINAL");
        var sd=new ZsdPentEvo();var node=new XmlDocument();node.LoadXml("<Device sdSource='HostFolder'/>");node.DocumentElement.SetAttribute("sdPath",PortablePaths.Store(path));sd.LoadConfigXml(node.DocumentElement);
        var can=typeof(ZsdPentEvo).GetMethod("RefreshFolderCommandUi_OnCanExecute",F);
        string cwd=Environment.CurrentDirectory;Environment.CurrentDirectory=Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        try {Check((bool)can.Invoke(sd,new object[]{null}),"relative Refresh follows CWD");}finally{Environment.CurrentDirectory=cwd;}
        sd.MountHostFolder(path);Check(sd.RefreshMountedFolder()>=0,"clean refresh");
        var view=(MainView)FormatterServices.GetUninitializedObject(typeof(MainView));
        typeof(MainView).GetField("_ejectSdMenuItems",F).SetValue(view,new ToolStripMenuItem[2]);
        typeof(MainView).GetField("_sdMountedStates",F).SetValue(view,new MediaState?[2]);
        typeof(MainView).GetField("_mediaButtons",F).SetValue(view,new Dictionary<MediaStatusKind,ToolStripDropDownButton>());
        bool available=false;var item=new ToolStripMenuItem("Refresh") {Enabled=false};
        typeof(MainView).GetField("_refreshSdMenuItem",F).SetValue(view,item);
        typeof(MainView).GetField("_refreshFolderSdCommand",F).SetValue(view,new Success(()=>available));
        var update=typeof(MainView).GetMethod("UpdateSecureDigitalMenuIndicators",F);
        update.Invoke(view,new object[]{null});Check(!item.Enabled,"unmounted menu enabled");available=true;update.Invoke(view,new object[]{null});Check(item.Enabled,"Refresh wrapper stayed disabled");available=false;update.Invoke(view,new object[]{null});Check(!item.Enabled,"eject menu stayed enabled");
        item.Dispose();sd.BusDisconnect();GC.SuppressFinalize(view);
        Console.WriteLine("PASS relative descriptor + foreign CWD + menu enable/disable refresh");
    }
    static void CloseConfigError(Query query)
    {
        using(var vm=new VirtualMachine(new Host(),null))
        {
            vm.Spectrum.BusManager.Init(vm.Spectrum,true);
            var model=(MainViewModel)FormatterServices.GetUninitializedObject(typeof(MainViewModel));typeof(MainViewModel).GetField("m_vm",F).SetValue(model,vm);
            var errors=new Messages();typeof(MainViewModel).GetField("m_userMessage",F).SetValue(model,errors);
            string config=Path.Combine(PortablePaths.DataDirectory("Temp"),"locked.vmz");vm.SaveConfigAs(config);
            using(var locked=File.Open(config,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
            {vm.DoRun();Check(!model.PrepareClose(),"config save failure accepted close");Check(vm.IsRunning&&errors.Errors==1,"config save failure left VM paused");}
            Check(model.PrepareClose()&&!vm.IsRunning,"unlocked close failed");
        }
        Console.WriteLine("PASS config-write failure resumes previously running VM; retry closes");
    }
    static void LongPaths()
    {
        string source=Path.Combine(Path.GetDirectoryName(PortablePaths.Root),"external-long-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source,"TEST.TRD"),"ORIGINAL");string deep=source;for(int i=0;i<7;i++)deep=Path.Combine(deep,new string((char)('a'+i),35));Directory.CreateDirectory(PortablePaths.IoPath(deep));File.WriteAllText(PortablePaths.IoPath(Path.Combine(deep,"tail.bin")),"DEEP");
        using(var stream=new HostFolderFat32Stream(source))
        {
            Check(stream.Warnings.Count==0,"deep source skipped during scan");
            Guest(stream,"TEST.TRD",(byte)'S');int reports=0;stream.CommitChanges(null,text=>{reports++;});string saved=stream.SourceFolder;
            Check(First(Path.Combine(source,"TEST.TRD"))=='O'&&First(Path.Combine(saved,"TEST.TRD"))=='S',"long save damaged source or lost guest");
            Check(File.ReadAllText(PortablePaths.IoPath(Path.Combine(saved,deep.Substring(source.Length+1),"tail.bin")))=="DEEP","deep file lost");Check(reports>0,"no save progress");
            using(var remount=new HostFolderFat32Stream(saved))Check(remount.Warnings.Count==0,"saved deep tree skipped during remount");
            Console.WriteLine("PASS long-path copy/save >260 chars, originals untouched, progress; {0}",saved);
        }
    }
    static void ProgressWorker()
    {
        var type=typeof(ZXMAK2.Host.WinForms.Services.UserQuery).GetNestedType("OperationDialog",BindingFlags.NonPublic);
        int caller=Thread.CurrentThread.ManagedThreadId,worker=caller;var watch=Stopwatch.StartNew();
        using(var form=(Form)Activator.CreateInstance(type,new object[]{"Unit progress",(Action<Action<string>>)(report=>{worker=Thread.CurrentThread.ManagedThreadId;report("WORKING");Thread.Sleep(150);})}))
        {
            typeof(Form).GetMethod("OnShown",F).Invoke(form,new object[]{EventArgs.Empty});
            var thread=(Thread)type.GetField("worker",F).GetValue(form);Check(thread.Join(5000),"worker hung");Check(worker!=caller,"operation used UI thread");
            Check(type.GetField("Failure",F|BindingFlags.Public).GetValue(form)==null,"worker failed");
            Check((string)type.GetField("message",F).GetValue(form)=="WORKING","progress report lost");
            Check(((WaitHandle)type.GetField("completed",F).GetValue(form)).WaitOne(0),"completion not signaled");
        }
        using(var form=(Form)Activator.CreateInstance(type,new object[]{"Unit failure",(Action<Action<string>>)(report=>{throw new IOException("injected");})}))
        {
            typeof(Form).GetMethod("OnShown",F).Invoke(form,new object[]{EventArgs.Empty});var thread=(Thread)type.GetField("worker",F).GetValue(form);Check(thread.Join(5000),"error worker hung");Check(type.GetField("Failure",F|BindingFlags.Public).GetValue(form) is IOException,"worker error lost");
        }
        Console.WriteLine("PASS progress worker/report/completion/error, no visible UI used");
    }
    static void RealFolder(string source)
    {
        source=Path.GetFullPath(source);string file=Path.Combine(source,"1099test.trd");byte original=First(file);byte guest=(byte)(original^0x80);
        int fileCount=Directory.GetFiles(source,"*",SearchOption.AllDirectories).Length;var watch=Stopwatch.StartNew();
        using(var stream=new HostFolderFat32Stream(source))
        {
            Check(stream.Warnings.Count==0,"real source scan warnings");
            Guest(stream,"1099test.trd",guest);string last="";stream.CommitChanges(null,text=>{if(text!=last){Console.WriteLine("{0:F1}s {1}",watch.Elapsed.TotalSeconds,text);last=text;}});
            string saved=stream.SourceFolder;Check(PortablePaths.IsInternal(saved)&&saved!=source,"real folder not switched");Check(First(file)==original,"real user file modified");Check(First(Path.Combine(saved,"1099test.trd"))==guest,"real session save lost");
            Check(Directory.GetFiles(PortablePaths.IoPath(saved),"*",SearchOption.AllDirectories).Length==fileCount,"real copy file count differs");
            var sourceFiles=Directory.GetFiles(source,"*",SearchOption.AllDirectories);var deepest=sourceFiles.OrderByDescending(p=>p.Length).First();string copied=Path.Combine(saved,deepest.Substring(source.Length+1));Check(copied.Length>260&&File.ReadAllBytes(deepest).SequenceEqual(File.ReadAllBytes(PortablePaths.IoPath(copied))),"real deep copy differs");
            string control=sourceFiles.First(p=>p!=file&&new FileInfo(p).Length>0&&new FileInfo(p).Length<1048576);
            Check(File.ReadAllBytes(control).SequenceEqual(File.ReadAllBytes(Path.Combine(saved,control.Substring(source.Length+1)))),"unrelated file copy differs");
            Console.WriteLine("PASS real tree {0} files; elapsed {1:F1}s; longest saved path {2}; copy retained at {3}",fileCount,watch.Elapsed.TotalSeconds,copied.Length,saved);
            var sd=new ZsdPentEvo();sd.MountHostFolder(saved);var node=new XmlDocument();node.LoadXml("<Device/>");sd.SaveConfigXml(node.DocumentElement);Check(!Path.IsPathRooted(node.DocumentElement.GetAttribute("sdPath")),"saved descriptor absolute");
            string cwd=Environment.CurrentDirectory;Environment.CurrentDirectory=Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            try {Check((bool)typeof(ZsdPentEvo).GetMethod("RefreshFolderCommandUi_OnCanExecute",F).Invoke(sd,new object[]{null}),"real internal Refresh gray");Check(sd.RefreshMountedFolder()>=0,"real internal refresh failed");}
            finally{Environment.CurrentDirectory=cwd;sd.BusDisconnect();}
        }
    }
    [STAThread]static int Main(string[] args)
    {
        try {var query=new Query();Locator.Resolve<IResolver>().RegisterInstance<IUserQuery>(null,query);
            if(args.Length>0&&args[0]=="real")RealFolder(args[1]);else {RelativeAndMenu();CloseConfigError(query);LongPaths();ProgressWorker();}
            Console.WriteLine("PASS {0} assertions",checks);return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
