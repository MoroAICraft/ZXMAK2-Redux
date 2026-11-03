// v95 reproduction, or --fixed regression assertions; only newly-created fixtures.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Diagnostics;
using ZXMAK2;
using ZXMAK2.Dependency;
using ZXMAK2.Engine;
using ZXMAK2.Hardware.Evo;
using ZXMAK2.Hardware.Circuits.SecureDigital;
using ZXMAK2.Host.Entities;
using ZXMAK2.Host.Interfaces;
using ZXMAK2.Host.Presentation;

static class StorageFaultProbe
{
    static bool fixedVersion;
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
    static int checks, findings;
    static string fixtures;
    static void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
    static void Finding(string name) { findings++; Console.WriteLine("FINDING: " + name); }
    sealed class Query : IUserQuery {
        public DlgResult Next = DlgResult.Yes;
        public DlgButtonSet LastButtons;
        public DlgResult Show(string m,string c,DlgButtonSet b,DlgIcon i){LastButtons=b;return Next;}
        public object ObjectSelector(object[] a,string c){throw new NotSupportedException();}
        public bool QueryText(string c,string t,ref string v){return false;}
        public bool QueryValue(string c,string t,string f,ref int v,int min,int max){return false;}
    }
    sealed class Messages : IUserMessage {
        public int Errors;
        public void ErrorDetails(Exception e){Errors++;} public void Error(Exception e){Errors++;}
        public void Error(string f,params object[] a){Errors++; Console.WriteLine("UI error: "+string.Format(f,a));}
        public void Warning(Exception e){} public void Warning(string f,params object[] a){} public void Info(string f,params object[] a){}
    }
    sealed class Host : IHostService {
        public IHostKeyboard Keyboard{get{return null;}} public IHostMouse Mouse{get{return null;}}
        public IHostJoystick Joystick{get{return null;}} public SyncSource SyncSource{get;set;}
        public int SampleRate{get{return 44100;}} public bool IsCaptured{get{return false;}}
        public IMediaRecorder MediaRecorder{get;set;} public bool CheckSyncSourceSupported(SyncSource s){return true;}
        public void PushFrame(IFrameInfo i,IFrameVideo v,IFrameSound s){Thread.Sleep(1);}
        public void CancelPush(){} public void Capture(){} public void Uncapture(){} public void Dispose(){}
    }
    static SdCard Card(ZsdPentEvo d){return (SdCard)typeof(ZsdPentEvo).GetField("card",F).GetValue(d);}
    static byte[] Sector(Stream s,long n){s.Position=n*512;var b=new byte[512];Check(s.Read(b,0,512)==512,"short sector");return b;}
    static long FileSector(Stream s,string name) {
        var b=Sector(s,0);long start=BitConverter.ToUInt16(b,14)+(long)b[16]*BitConverter.ToUInt32(b,36);
        var root=Sector(s,start);
        for(int i=0;i<512;i+=32) if(System.Text.Encoding.ASCII.GetString(root,i,11)==name) {
            uint cluster=(uint)(BitConverter.ToUInt16(root,i+20)<<16)|BitConverter.ToUInt16(root,i+26);
            return start+(cluster-2)*b[13];
        }
        throw new Exception("fixture missing "+name);
    }
    static void Guest(HostFolderFat32Stream s,string name,byte value) {long n=FileSector(s,name);var b=Sector(s,n);b[0]=value;s.Position=n*512;s.Write(b,0,512);}
    static string Fresh(string name) {
        var p=Path.Combine(fixtures,name+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(p);
        File.WriteAllText(Path.Combine(p,"TEST.TRD"),"ORIGINAL");File.WriteAllText(Path.Combine(p,"SECOND.BIN"),"SECOND!!");return p;
    }
    static Exception Error(Action action) {try{action();return null;}catch(Exception e){Console.WriteLine("Expected/observed exception: "+e.GetType().Name+": "+e.Message);return e;}}
    static void Original(string root){Check(File.ReadAllText(Path.Combine(root,"TEST.TRD"))=="ORIGINAL","original changed");}
    static void CloseCached(HostFolderFat32Stream s){typeof(HostFolderFat32Stream).GetMethod("CloseCachedFile",F).Invoke(s,null);}
    static void Atomic() {
        string p=Path.Combine(fixtures,"atomic.cfg");File.WriteAllText(p,"ORIGINAL");
        var ex=Error(()=>PortablePaths.WriteAtomic(p,s=>{s.WriteByte(65);throw new IOException("Injected ERROR_DISK_FULL",unchecked((int)0x80070070));}));
        Check(ex is IOException&&File.ReadAllText(p)=="ORIGINAL","partial writer destroyed original");
        Check(Directory.GetFiles(fixtures,".zxmak2-write-*").Length==0,"partial atomic temp leaked");
        using(var lease=File.Open(p,FileMode.Open,FileAccess.Read,FileShare.None)) Check(Error(()=>PortablePaths.WriteAtomic(p,s=>s.WriteByte(66))) is IOException,"locked config accepted");
        Check(File.ReadAllText(p)=="ORIGINAL","locked config damaged");
        PortablePaths.WriteAtomic(p,s=>s.WriteByte(67));Check(File.ReadAllBytes(p).SequenceEqual(new byte[]{67}),"atomic retry failed");
        Console.WriteLine("PASS atomic partial-write simulated disk-full / locked target / retry");
    }
    static void SaveFaults() {
        foreach(string mode in new[]{"locked-second","denied-folder","missing-folder","callback-diskfull","callback-rebuild","readonly-original"}) {
            string root=Fresh(mode),file=Path.Combine(root,"TEST.TRD"),second=Path.Combine(root,"SECOND.BIN");
            using(var s=new HostFolderFat32Stream(root)) {
                Guest(s,"TEST    TRD",(byte)'G');Guest(s,"SECOND  BIN",(byte)'H');CloseCached(s);
                FileStream lease=null;DirectorySecurity acl=null;bool moved=false;
                Exception ex=null;int callback=0;
                try {
                    if(mode=="locked-second") lease=File.Open(second,FileMode.Open,FileAccess.Read,FileShare.Read);
                    if(mode=="denied-folder") {
                        acl=Directory.GetAccessControl(root,AccessControlSections.Access);var denied=new DirectorySecurity();denied.SetSecurityDescriptorBinaryForm(acl.GetSecurityDescriptorBinaryForm(),AccessControlSections.Access);
                        denied.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User,FileSystemRights.Write,AccessControlType.Deny));
                        Directory.SetAccessControl(root,denied);
                        Check(Error(()=>{using(File.Create(Path.Combine(root,"denied-check"))) {}}) is UnauthorizedAccessException,"ACL not effective");
                    }
                    if(mode=="missing-folder") {Directory.Move(root,root+"-away");moved=true;}
                    if(mode=="readonly-original") File.SetAttributes(file,FileAttributes.ReadOnly);
                    ex=Error(()=>s.CommitChanges(backup=>{
                        callback++;
                        if(mode=="callback-diskfull") throw new IOException("Injected post-write ERROR_DISK_FULL",unchecked((int)0x80070070));
                        if(mode=="callback-rebuild") throw new IOException("Injected refresh rebuild failure");
                    }));
                } finally {
                    if(lease!=null)lease.Dispose();
                    if(acl!=null){var restored=new DirectorySecurity();restored.SetSecurityDescriptorBinaryForm(acl.GetSecurityDescriptorBinaryForm(),AccessControlSections.Access);Directory.SetAccessControl(root,restored);}
                    if(moved)Directory.Move(root+"-away",root);
                    if(mode=="readonly-original") foreach(string f in Directory.GetFiles(root,"*",SearchOption.AllDirectories)) File.SetAttributes(f,FileAttributes.Normal);
                }
                if(!fixedVersion&&mode=="readonly-original"&&ex!=null&&!s.HasChanges&&File.ReadAllBytes(file)[0]=='G') {
                    Finding("read-only original: Commit throws after saving data, HasChanges=false; backup directories="+Directory.GetDirectories(root,".zxmak2-sd-backup-*").Length);
                } else {
                    Check(ex!=null,"fault not reached: "+mode);Original(root);
                    Check(File.ReadAllText(second)=="SECOND!!"&&s.HasChanges,"rollback lost originals/session: "+mode);
                    if(mode.StartsWith("callback-"))Check(callback==1,"post-write injection missed");
                    s.CommitChanges();Check(File.ReadAllBytes(file)[0]=='G'&&File.ReadAllBytes(second)[0]=='H'&&!s.HasChanges,"retry lost guest: "+mode);
                    Console.WriteLine("PASS "+mode+" preserves source + dirty session; retry succeeds");
                }
            }
        }
    }
    static void RefreshMissing(Query q) {
        string root=Fresh("refresh-missing");var d=new ZsdPentEvo();d.MountHostFolder(root);
        try {
            var old=Card(d);Guest(old.HostFolderStream,"TEST    TRD",(byte)'R');CloseCached(old.HostFolderStream);
            Directory.Move(root,root+"-away");
            try {foreach(var answer in new[]{DlgResult.Yes,DlgResult.No}) {q.Next=answer;Check(Error(()=>d.RefreshMountedFolder())!=null,"missing refresh accepted");Check(Object.ReferenceEquals(old,Card(d))&&d.HasPendingChanges,"failed refresh lost session");}}
            finally {Directory.Move(root+"-away",root);}
            q.Next=DlgResult.Yes;Check(d.RefreshMountedFolder()>=0&&!d.HasPendingChanges&&File.ReadAllBytes(Path.Combine(root,"TEST.TRD"))[0]=='R',"refresh retry failed");
        } finally {q.Next=DlgResult.No;d.BusDisconnect();}
        Console.WriteLine("PASS missing Refresh Save/Discard keeps mounted session; retry succeeds");
    }
    static void CloseFailure(Query q) {
        foreach(bool readOnly in fixedVersion?new[]{false,true}:new[]{false})
        foreach(bool running in new[]{false,true}) using(var vm=new VirtualMachine(new Host(),null)) {
            vm.Spectrum.BusManager.Init(vm.Spectrum,true);var sd=vm.Bus.FindDevice<ZsdPentEvo>();Check(sd!=null,"SD missing");
            string root=Fresh("close-"+running);sd.MountHostFolder(root);Guest(Card(sd).HostFolderStream,"TEST    TRD",(byte)'C');CloseCached(Card(sd).HostFolderStream);
            vm.SaveConfigAs(Path.Combine(fixtures,"close-"+running+".vmz"));
            var model=(MainViewModel)FormatterServices.GetUninitializedObject(typeof(MainViewModel));typeof(MainViewModel).GetField("m_vm",F).SetValue(model,vm);
            var errors=new Messages();typeof(MainViewModel).GetField("m_userMessage",F).SetValue(model,errors);q.Next=DlgResult.Yes;if(running)vm.DoRun();
            string target=Path.Combine(root,"TEST.TRD");
            using(var lease=readOnly?null:File.Open(target,FileMode.Open,FileAccess.Read,FileShare.Read)) {
                if(readOnly)File.SetAttributes(target,FileAttributes.ReadOnly);
                try {
                Check(!model.PrepareClose()&&vm.IsRunning==running&&sd.HasPendingChanges&&errors.Errors==1,"failed close state not preserved");
                Original(root);
                } finally {if(readOnly)File.SetAttributes(target,FileAttributes.Normal);}
            }
            Check(model.PrepareClose()&&!vm.IsRunning&&!sd.HasPendingChanges,"close retry failed");
        }
        Console.WriteLine("PASS failed Save/Exit resumes running VM, keeps paused VM paused; retry closes");
    }
    static void CrashChild(string root) {
        using(var s=new HostFolderFat32Stream(root)) {Guest(s,"TEST    TRD",(byte)'X');Console.WriteLine("READY");Console.Out.Flush();Console.ReadLine();}
    }
    sealed class FailSecondWriteStream : Stream {
        FileStream inner; readonly string path; int writes;
        public FailSecondWriteStream(string p){path=p;inner=File.Open(p,FileMode.Open,FileAccess.ReadWrite,FileShare.Read);}
        public override bool CanRead{get{return true;}} public override bool CanWrite{get{return true;}} public override bool CanSeek{get{return true;}}
        public override long Length{get{return inner.Length;}} public override long Position{get{return inner.Position;}set{inner.Position=value;}}
        public override void Flush(){inner.Flush(true);} public override int Read(byte[] b,int o,int n){return inner.Read(b,o,n);}
        public override long Seek(long o,SeekOrigin w){return inner.Seek(o,w);} public override void SetLength(long n){inner.SetLength(n);}
        public override void Write(byte[] b,int o,int n){
            if(++writes==2){long p=inner.Position;inner.Flush(true);inner.Dispose();inner=File.Open(path,FileMode.Open,FileAccess.ReadWrite,FileShare.Read);inner.Position=p;throw new IOException("Injected transient sector-write failure",unchecked((int)0x8007045D));}
            inner.Write(b,o,n);
        }
        protected override void Dispose(bool d){if(d)inner.Dispose();base.Dispose(d);}
    }
    static void ImagePartialSave() {
        foreach(bool discard in new[]{false,true}) {
            string path=Path.Combine(fixtures,"internal-partial-"+discard+".img");File.WriteAllBytes(path,new byte[4096]);
            using(var s=new SdImageSessionStream(new FailSecondWriteStream(path),path)) {
                s.Position=0;s.WriteByte(65);s.Position=512;s.WriteByte(66);
                Check(Error(()=>s.CommitChanges()) is IOException&&s.HasChanges,"sector fault not reached/session lost");
                byte[] partial=ReadMedia(path);Check(partial[0]==65&&partial[512]==0,"partial apply not reached");
                if(!discard) {
                    var retry=Error(()=>s.CommitChanges());
                    if(!fixedVersion&&retry!=null&&retry.Message.Contains("changed outside")) Finding("internal IMG partial save: retry rejected as external change after our own first-sector write");
                    else Check(retry==null,"unexpected image retry failure");
                } else {
                    if(fixedVersion) {
                        Check(Error(()=>s.DiscardChanges()) is IOException&&s.HasChanges,"partial save discard lost recovery");
                        s.Position=1024;Check(Error(()=>s.WriteByte(67)) is IOException,"guest write allowed during incomplete save");
                    } else { s.DiscardChanges();Check(!s.HasChanges,"image discard dirty"); }
                    partial=ReadMedia(path);
                    if(!fixedVersion&&partial[0]==65&&partial[512]==0) Finding("internal IMG partial save then Discard removes recovery journal but leaves a partially updated image");
                }
            }
            using(var restored=new SdImageSessionStream(File.Open(path,FileMode.Open,FileAccess.ReadWrite,FileShare.Read),path)) {
                byte[] actual=ReadMedia(path);
                if(!discard||fixedVersion)Check(actual[0]==65&&actual[512]==66&&!restored.HasChanges,"interrupted IMG remount did not finish transaction");
                else Check(actual[0]==65&&actual[512]==0&&!restored.HasChanges,"discard reproduction changed unexpectedly");
            }
        }
        Console.WriteLine("PASS internal IMG fault injection exercised; retained applying journal recovers on reopen");
    }
    static byte[] ReadMedia(string path){using(var f=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))using(var m=new MemoryStream()){f.CopyTo(m);return m.ToArray();}}
    static void ImageGuards() {
        string p=Path.Combine(fixtures,"image-guards.img");File.WriteAllBytes(p,new byte[4096]);
        using(var s=new SdImageSessionStream(File.Open(p,FileMode.Open,FileAccess.ReadWrite,FileShare.Read),p)) {
            s.WriteByte(77);s.DiscardChanges();Check(!s.HasChanges&&ReadMedia(p)[0]==0,"ordinary discard altered image");
            s.Position=0;s.WriteByte(78);s.CommitChanges();Check(!s.HasChanges&&ReadMedia(p)[0]==78,"ordinary save failed");
            s.Position=0;s.WriteByte(79);s.DiscardChanges();Check(ReadMedia(p)[0]==78,"new session discard failed");
        }
        // Close the handle before modifying metadata; ordinary unfinished
        // journals must still reject an external edit on the next mount.
        using(var s=new SdImageSessionStream(File.Open(p,FileMode.Open,FileAccess.ReadWrite,FileShare.Read),p)) {s.Position=0;s.WriteByte(80);}
        File.SetLastWriteTimeUtc(p,DateTime.UtcNow.AddMinutes(2));
        using(var raw=File.Open(p,FileMode.Open,FileAccess.ReadWrite,FileShare.Read))
            Check(Error(()=>new SdImageSessionStream(raw,p)) is InvalidDataException,"external edit guard bypassed");
        Console.WriteLine("PASS normal IMG save/discard/reuse and external-change guard");
    }
    static void ImageCleanup() {
        string p=Path.Combine(fixtures,"image-cleanup.img");File.WriteAllBytes(p,new byte[4096]);
        using(var s=new SdImageSessionStream(File.Open(p,FileMode.Open,FileAccess.ReadWrite,FileShare.Read),p)) {
            s.WriteByte(88);string journal=(string)typeof(SdImageSessionStream).GetField("m_journalPath",F).GetValue(s);
            File.SetAttributes(journal,FileAttributes.ReadOnly);
            try {Check(Error(()=>s.CommitChanges()) is UnauthorizedAccessException&&s.HasChanges&&s.IsSaveIncomplete,"journal cleanup did not preserve retry");}
            finally {File.SetAttributes(journal,FileAttributes.Normal);}
            s.CommitChanges();Check(!s.HasChanges&&!s.IsSaveIncomplete&&ReadMedia(p)[0]==88,"journal cleanup retry failed");
            s.Position=512;s.WriteByte(89);string marker=(string)typeof(SdImageSessionStream).GetField("m_applyingPath",F).GetValue(s);
            // Inject marker cleanup denial only AFTER writes, before ClearJournal.
            var raw=(Stream)typeof(SdImageSessionStream).GetField("m_source",F).GetValue(s);
            typeof(SdImageSessionStream).GetField("m_source",F).SetValue(s,new AfterFlushStream(raw,()=>File.SetAttributes(marker,FileAttributes.ReadOnly)));
            try {
                s.CommitChanges();Check(!s.HasChanges&&!s.IsSaveIncomplete&&ReadMedia(p)[512]==89,"marker cleanup masked committed data");
                s.Position=1024;Check(Error(()=>s.WriteByte(90)) is UnauthorizedAccessException&&!s.HasChanges&&!File.Exists(journal),"stale marker attached to new journal");
            } finally {File.SetAttributes(marker,FileAttributes.Normal);}
            s.Position=1024;s.WriteByte(90);s.DiscardChanges();Check(ReadMedia(p)[1024]==0,"ordinary discard after leftover marker failed");
        }
        Console.WriteLine("PASS journal deletion failure/retry; harmless marker cleanup; safe new session");
    }
    sealed class AfterFlushStream:Stream {
        readonly Stream inner;Action once;public AfterFlushStream(Stream s,Action a){inner=s;once=a;}
        public override bool CanRead{get{return inner.CanRead;}}public override bool CanWrite{get{return inner.CanWrite;}}public override bool CanSeek{get{return inner.CanSeek;}}
        public override long Length{get{return inner.Length;}}public override long Position{get{return inner.Position;}set{inner.Position=value;}}
        public override void Flush(){inner.Flush();var a=once;once=null;if(a!=null)a();}
        public override int Read(byte[] b,int o,int n){return inner.Read(b,o,n);}public override void Write(byte[] b,int o,int n){inner.Write(b,o,n);}
        public override long Seek(long o,SeekOrigin s){return inner.Seek(o,s);}public override void SetLength(long n){inner.SetLength(n);}
        protected override void Dispose(bool d){if(d)inner.Dispose();base.Dispose(d);}
    }
    static void ImageClose(Query q) {
        foreach(bool running in new[]{false,true})using(var vm=new VirtualMachine(new Host(),null)) {
            vm.Spectrum.BusManager.Init(vm.Spectrum,true);var sd=vm.Bus.FindDevice<ZsdPentEvo>();
            string path=Path.Combine(fixtures,"image-close-"+running+".img");File.WriteAllBytes(path,new byte[4096]);
            var session=new SdImageSessionStream(new FailSecondWriteStream(path),path);session.WriteByte(65);session.Position=512;session.WriteByte(66);
            var card=new SdCard();typeof(SdCard).GetField("fstream",F).SetValue(card,session);typeof(ZsdPentEvo).GetField("card",F).SetValue(sd,card);
            vm.SaveConfigAs(Path.Combine(fixtures,"image-close-"+running+".vmz"));
            var model=(MainViewModel)FormatterServices.GetUninitializedObject(typeof(MainViewModel));typeof(MainViewModel).GetField("m_vm",F).SetValue(model,vm);
            var errors=new Messages();typeof(MainViewModel).GetField("m_userMessage",F).SetValue(model,errors);if(running)vm.DoRun();q.Next=DlgResult.Yes;
            Check(!model.PrepareClose()&&vm.IsRunning==running&&sd.HasPendingChanges&&session.IsSaveIncomplete,"partial IMG close lost session/run state");
            foreach(var choice in new[]{DlgResult.Cancel,DlgResult.No}) {
                q.Next=choice;Check(!model.PrepareClose()&&q.LastButtons==DlgButtonSet.RetryCancel&&session.IsSaveIncomplete,"partial IMG prompt permitted discard/exit");
            }
            q.Next=DlgResult.Retry;Check(model.PrepareClose()&&!vm.IsRunning&&!sd.HasPendingChanges,"partial IMG close retry failed");
            var data=ReadMedia(path);Check(data[0]==65&&data[512]==66,"retry exit saved incorrect sectors");
        }
        q.Next=DlgResult.Yes;Console.WriteLine("PASS partial IMG Save/Exit, Retry/Cancel prompt, running/paused state preserved");
    }
    static void ExternalImageCleanup() {
        string p=Path.Combine(Path.GetDirectoryName(PortablePaths.Root.TrimEnd(Path.DirectorySeparatorChar)),"external-cleanup-"+Guid.NewGuid().ToString("N")+".img");
        File.WriteAllBytes(p,new byte[4096]);Check(!PortablePaths.IsInternal(p),"external fixture is not external");
        using(var s=new SdImageSessionStream(File.Open(p,FileMode.Open,FileAccess.Read,FileShare.Read),p)) {
            s.WriteByte(90);string journal=(string)typeof(SdImageSessionStream).GetField("m_journalPath",F).GetValue(s);
            File.SetAttributes(journal,FileAttributes.ReadOnly);
            try {
                Check(Error(()=>s.CommitChanges()) is UnauthorizedAccessException&&s.HasChanges&&s.ImagePath==p,"failed external save switched path/lost journal");
                s.Position=0;Check(s.ReadByte()==90&&ReadMedia(p)[0]==0,"external image/session damaged");
            } finally {File.SetAttributes(journal,FileAttributes.Normal);}
            s.CommitChanges();Check(!s.HasChanges&&PortablePaths.IsInternal(s.ImagePath)&&ReadMedia(s.ImagePath)[0]==90&&ReadMedia(p)[0]==0,"external save retry failed");
        }
        Console.WriteLine("PASS external IMG journal cleanup failure preserves stream/path, then local-copy retry; original unchanged");
    }
    static void FolderCleanupAndReadOnly(Query q) {
        string root=Fresh("cleanup"),file=Path.Combine(root,"TEST.TRD"),savedBackup=null;
        using(var s=new HostFolderFat32Stream(root)) {
            Guest(s,"TEST    TRD",(byte)'K');
            try {
                s.CommitChanges(backup=>{savedBackup=backup;File.SetAttributes(Path.Combine(backup,"TEST.TRD"),FileAttributes.ReadOnly);});
                Check(!s.HasChanges&&File.ReadAllBytes(file)[0]=='K',"backup cleanup masked successful save");
                Check(Directory.Exists(savedBackup),"injected cleanup failure not reached");
            } finally {if(savedBackup!=null&&Directory.Exists(savedBackup))foreach(string f in Directory.GetFiles(savedBackup,"*",SearchOption.AllDirectories))File.SetAttributes(f,FileAttributes.Normal);}
        }
        root=Fresh("readonly-refresh");file=Path.Combine(root,"TEST.TRD");var d=new ZsdPentEvo();d.MountHostFolder(root);
        try {
            var old=Card(d);Guest(old.HostFolderStream,"TEST    TRD",(byte)'R');CloseCached(old.HostFolderStream);
            File.SetAttributes(file,FileAttributes.ReadOnly);q.Next=DlgResult.Yes;
            Check(Error(()=>d.RefreshMountedFolder()) is UnauthorizedAccessException&&Object.ReferenceEquals(old,Card(d))&&d.HasPendingChanges,"readonly refresh lost mounted session");Original(root);
            File.SetAttributes(file,FileAttributes.Normal);
            Check(d.RefreshMountedFolder()>=0&&!d.HasPendingChanges&&File.ReadAllBytes(file)[0]=='R',"readonly refresh retry failed");
        } finally {File.SetAttributes(file,FileAttributes.Normal);q.Next=DlgResult.No;d.BusDisconnect();}
        Console.WriteLine("PASS successful commit survives backup cleanup denial; readonly Save & Refresh preserves session and retries");
    }
    static void Crash() {
        string root=Fresh("crash"),exe=Assembly.GetExecutingAssembly().Location;
        var start=new ProcessStartInfo(exe,"crash-child \""+root+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,RedirectStandardInput=true};
        using(var child=Process.Start(start)) {
            string line=null;var reader=new Thread(()=>line=child.StandardOutput.ReadLine()){IsBackground=true};reader.Start();
            if(!reader.Join(15000)){child.Kill();throw new Exception("child not ready");}
            Check(line=="READY","crash fixture child failed: "+line);child.Kill();Check(child.WaitForExit(5000),"child kill timeout");
        }
        Original(root);File.WriteAllText(Path.Combine(root,"TEST.TRD"),"PCLATEST");
        using(var s=new HostFolderFat32Stream(root)) {Check(!s.WasRecovered&&!s.HasChanges,"stale crash overlay recovered");Check(Sector(s,FileSector(s,"TEST    TRD"))[0]=='P',"crash remount ignored current PC data");}
        Console.WriteLine("PASS killed only probe child; remount uses PC current state, not crashed journal");
    }
    [STAThread] static int Main(string[] args) {
        try {
            Console.OutputEncoding=System.Text.Encoding.UTF8;
            if(args.Length==2&&args[0]=="crash-child"){CrashChild(args[1]);return 0;}
            fixedVersion=args.Contains("--fixed");
            fixtures=Path.Combine(PortablePaths.Root,"fault-fixtures-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(fixtures);
            var q=new Query();Locator.Resolve<IResolver>().RegisterInstance<IUserQuery>(null,q);
            Atomic();SaveFaults();RefreshMissing(q);CloseFailure(q);Crash();ImagePartialSave();
            if(fixedVersion){ImageGuards();ImageCleanup();ImageClose(q);ExternalImageCleanup();FolderCleanupAndReadOnly(q);Check(findings==0,"regression findings remain");}
            Console.WriteLine("AUDIT COMPLETE: checks={0}, findings={1}; fixtures={2}",checks,findings,fixtures);return 0;
        } catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
