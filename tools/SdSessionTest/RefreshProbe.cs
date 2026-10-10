using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Windows.Forms;
using ZXMAK2.Dependency;
using ZXMAK2.Engine;
using ZXMAK2.Hardware.Evo;
using ZXMAK2.Hardware.Circuits.SecureDigital;
using ZXMAK2.Host.Entities;
using ZXMAK2.Host.Interfaces;
using ZXMAK2.Host.Presentation;
using ZXMAK2.Mvvm;

internal static class RefreshProbe
{
    private const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
    private sealed class Query : IUserQuery, IActionUserQuery
    {
        internal DlgResult Next = DlgResult.Cancel;
        internal int Calls, ActionCalls;
        internal Action BeforeShow;
        public DlgResult ShowActions(string m,string c,string a,string d,string x) {
            Check(c=="Refresh SD folder?" && a=="Refresh (Save changes)" &&
                  d=="Refresh (Without saving)" && x=="Cancel","wrong action labels");
            Check(m.Contains("restart"),"restart is not explained");
            Calls++;ActionCalls++;if(BeforeShow!=null)BeforeShow();return Next;
        }
        public DlgResult Show(string m,string c,DlgButtonSet b,DlgIcon i){Calls++;if(BeforeShow!=null)BeforeShow();return Next;}
        public object ObjectSelector(object[] a,string c){return null;}
        public bool QueryText(string c,string t,ref string v){return false;}
        public bool QueryValue(string c,string t,string f,ref int v,int min,int max){return false;}
    }
    private sealed class Messages : IUserMessage {
        internal int Errors;
        public void ErrorDetails(Exception e){Errors++;}
        public void Error(Exception e){Errors++;}
        public void Error(string f,params object[] a){Errors++;}
        public void Warning(Exception e){throw e;}
        public void Warning(string f,params object[] a){throw new Exception(f);}
        public void Info(string f,params object[] a){}
    }
    private sealed class Host : IHostService {
        public IHostKeyboard Keyboard{get{return null;}} public IHostMouse Mouse{get{return null;}}
        public IHostJoystick Joystick{get{return null;}} public SyncSource SyncSource{get;set;}
        public int SampleRate{get{return 44100;}} public bool IsCaptured{get{return false;}}
        public IMediaRecorder MediaRecorder{get;set;}
        public bool CheckSyncSourceSupported(SyncSource s){return true;}
        public void PushFrame(IFrameInfo i,IFrameVideo v,IFrameSound s){Thread.Sleep(1);}
        public void CancelPush(){} public void Capture(){} public void Uncapture(){} public void Dispose(){}
    }
    private static SdCard Card(ZsdPentEvo sd){return(SdCard)typeof(ZsdPentEvo).GetField("card",F).GetValue(sd);}
    private static byte[] Read(Stream s,long sector){s.Position=sector*512;var b=new byte[512];Check(s.Read(b,0,512)==512,"sector read");return b;}
    private static void Write(Stream s,long sector,byte[] b){s.Position=sector*512;s.Write(b,0,512);}
    private static long Data(Stream s){var b=Read(s,0);return BitConverter.ToUInt16(b,14)+(long)b[16]*BitConverter.ToUInt32(b,36);}
    private static uint Cluster(byte[] b,int o){return(uint)((BitConverter.ToUInt16(b,o+20)<<16)|BitConverter.ToUInt16(b,o+26));}
    private static int Entry(byte[] b,string name){string wanted=name.ToUpperInvariant().Replace("."," ");for(int i=0;i<512;i+=32){if(b[i]==0||b[i]==0xE5||b[i+11]==15)continue;string n=System.Text.Encoding.ASCII.GetString(b,i,8).TrimEnd()+" "+System.Text.Encoding.ASCII.GetString(b,i+8,3).TrimEnd();if(n.TrimEnd()==wanted.TrimEnd())return i;}throw new Exception("Missing "+name);}
    private static void Guest(ZsdPentEvo sd,string name,byte value){var s=Card(sd).HostFolderStream;var boot=Read(s,0);long start=Data(s);var root=Read(s,start);uint c=Cluster(root,Entry(root,name));long sector=start+(c-2)*boot[13];var b=Read(s,sector);b[0]=value;Write(s,sector,b);}
    private static void Put32(byte[] b,int o,uint v){Buffer.BlockCopy(BitConverter.GetBytes(v),0,b,o,4);}
    private static void Put16(byte[] b,int o,ushort v){Buffer.BlockCopy(BitConverter.GetBytes(v),0,b,o,2);}
    private static uint Free(Stream s){var boot=Read(s,0);long fat=BitConverter.ToUInt16(boot,14);for(uint c=2;c<1024;c++){if((BitConverter.ToUInt32(Read(s,fat+c/128),(int)(c%128)*4)&0x0FFFFFFF)==0)return c;}throw new Exception("No free cluster");}
    private static void Fat(Stream s,uint c){var boot=Read(s,0);for(int copy=0;copy<boot[16];copy++){long pos=BitConverter.ToUInt16(boot,14)+(long)copy*BitConverter.ToUInt32(boot,36)+c/128;var b=Read(s,pos);Put32(b,(int)(c%128)*4,0x0FFFFFFF);Write(s,pos,b);}}
    private static void Name(byte[] b,int o,string name){var parts=name.ToUpperInvariant().Split('.');var n=System.Text.Encoding.ASCII.GetBytes(parts[0].PadRight(8)+(parts.Length>1?parts[1]:"").PadRight(3));Buffer.BlockCopy(n,0,b,o,11);b[o+12]=0;}
    private static void Add(Stream s,string name,bool directory){long start=Data(s);var root=Read(s,start);int o=0;while(root[o]!=0&&root[o]!=0xE5)o+=32;uint c=Free(s);Fat(s,c);Name(root,o,name);root[o+11]=(byte)(directory?16:32);Put16(root,o+20,(ushort)(c>>16));Put16(root,o+26,(ushort)c);Put32(root,o+28,(uint)(directory?0:4));Write(s,start,root);var b=new byte[512];if(!directory)Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes("NEW!"),0,b,0,4);Write(s,start+(c-2)*Read(s,0)[13],b);}
    private static void Remove(Stream s,string name){long start=Data(s);var b=Read(s,start);b[Entry(b,name)]=0xE5;Write(s,start,b);}
    private static void Rename(Stream s,string oldName,string newName){long start=Data(s);var b=Read(s,start);Name(b,Entry(b,oldName),newName);Write(s,start,b);}
    private static string Fresh(string scratch,string test){var path=Path.Combine(scratch,test);Directory.CreateDirectory(path);File.WriteAllText(Path.Combine(path,"TEST.TRD"),"GAME DATA AND UNCHANGED TAIL");File.WriteAllText(Path.Combine(path,"NV.EXT"),"PC OLD");return path;}
    private static void Cases(string scratch,Query q)
    {
        var sd=new ZsdPentEvo();
        try {
            var path=Fresh(scratch,"pc-only");sd.MountHostFolder(path);var before=q.Calls;
            File.WriteAllText(Path.Combine(path,"NV.EXT"),"PC REPLACEMENT");File.WriteAllText(Path.Combine(path,"ADD.BIN"),"ADDED");
            Check(sd.RefreshMountedFolder()>=0 && q.Calls==before,"PC-only refresh prompted");
            Check(!sd.HasPendingChanges,"clean refresh dirty");
            Guest(sd,"TEST.TRD",(byte)'S');q.Next=DlgResult.Yes;
            File.WriteAllText(Path.Combine(path,"NV.EXT"),"PC NEW AGAIN");File.WriteAllText(Path.Combine(path,"MORE.BIN"),"MORE");
            Check(sd.RefreshMountedFolder()>=0,"disjoint save refresh failed");
            Check(File.ReadAllText(Path.Combine(path,"TEST.TRD"))=="SAME DATA AND UNCHANGED TAIL","partial TRD write/tail lost");
            Check(File.ReadAllText(Path.Combine(path,"NV.EXT"))=="PC NEW AGAIN"&&File.Exists(Path.Combine(path,"MORE.BIN")),"PC edits overwritten");
            Check(!Directory.GetDirectories(path,".zxmak2-sd-backup-*").Any(),"backup leaked in source");
            Check(!sd.HasPendingChanges,"save refresh retained changes");
            Guest(sd,"TEST.TRD",(byte)'X');File.WriteAllText(Path.Combine(path,"NV.EXT"),"NO SAVE PC");q.Next=DlgResult.No;
            Check(sd.RefreshMountedFolder()>=0 && File.ReadAllBytes(Path.Combine(path,"TEST.TRD"))[0]=='S',"discard saved guest");
            Check(File.ReadAllText(Path.Combine(path,"NV.EXT"))=="NO SAVE PC"&&!sd.HasPendingChanges,"discard lost PC/dirty");
            Guest(sd,"TEST.TRD",(byte)'C');var old=Card(sd);q.Next=DlgResult.Cancel;
            Check(sd.RefreshMountedFolder()<0&&Object.ReferenceEquals(old,Card(sd))&&sd.HasPendingChanges,"cancel lost session");
            q.Next=DlgResult.None;Check(sd.RefreshMountedFolder()<0&&Object.ReferenceEquals(old,Card(sd)),"closed question accepted");
            q.Next=DlgResult.Yes;var stamp=File.GetLastWriteTime(Path.Combine(path,"TEST.TRD"));
            File.WriteAllText(Path.Combine(path,"TEST.TRD"),"PCME DATA AND UNCHANGED TAIL");File.SetLastWriteTime(Path.Combine(path,"TEST.TRD"),stamp);
            bool rejected=false;try{sd.RefreshMountedFolder();}catch(IOException){rejected=true;}
            Check(rejected&&sd.HasPendingChanges&&Object.ReferenceEquals(old,Card(sd)),"same-file timestamp-preserved collision missed");
            Check(File.ReadAllText(Path.Combine(path,"TEST.TRD")).StartsWith("PCME"),"collision overwritten");
            // A touched original has a private snapshot: export still contains
            // guest data, not the conflicting newer PC payload.
            var exported=Path.Combine(scratch,"conflict.img");Card(sd).HostFolderStream.ExportImage(exported);
            using(var image=File.OpenRead(exported)) {
                var root=Read(image,Data(image));uint c=Cluster(root,Entry(root,"TEST.TRD"));
                Check(Read(image,Data(image)+(c-2)*Read(image,0)[13])[0]=='C',"export lost frozen guest original");
            }
            q.Next=DlgResult.No;Check(sd.RefreshMountedFolder()>=0&&!sd.HasPendingChanges,"discard after conflict failed");
            // Metadata/FSInfo writes and reverted data writes need no save choice.
            var s=Card(sd).HostFolderStream;var info=Read(s,1);info[488]^=1;Write(s,1,info);before=q.Calls;
            Check(sd.RefreshMountedFolder()>=0&&q.Calls==before,"FSInfo-only prompted");
            Guest(sd,"TEST.TRD",(byte)'Z');Guest(sd,"TEST.TRD",(byte)'P');before=q.Calls;
            Check(sd.RefreshMountedFolder()>=0&&q.Calls==before,"reverted data prompted");
            // Create/rename/delete in the guest; unrelated PC deletion/additions survive.
            path=Fresh(scratch,"tree");sd.MountHostFolder(path);s=Card(sd).HostFolderStream;
            Rename(s,"TEST.TRD","RENAMED.TRD");Add(s,"NEW.BIN",false);Add(s,"DIR",true);
            File.Delete(Path.Combine(path,"NV.EXT"));File.WriteAllText(Path.Combine(path,"HOST.BIN"),"PC");q.Next=DlgResult.Yes;
            Check(sd.RefreshMountedFolder()>=0,"guest tree save failed");
            Check(!File.Exists(Path.Combine(path,"TEST.TRD"))&&File.Exists(Path.Combine(path,"RENAMED.TRD"))&&Directory.Exists(Path.Combine(path,"DIR")),"rename/create failed");
            Check(File.ReadAllText(Path.Combine(path,"NEW.BIN"))=="NEW!"&&!File.Exists(Path.Combine(path,"NV.EXT"))&&File.Exists(Path.Combine(path,"HOST.BIN")),"tree merge lost PC/content");
            s=Card(sd).HostFolderStream;Remove(s,"NEW.BIN");q.Next=DlgResult.Yes;Check(sd.RefreshMountedFolder()>=0&&!File.Exists(Path.Combine(path,"NEW.BIN")),"guest delete failed");
            // A PC child added to a directory being deleted must be protected.
            s=Card(sd).HostFolderStream;Remove(s,"DIR");File.WriteAllText(Path.Combine(path,"DIR","PC.BIN"),"SAFE");old=Card(sd);rejected=false;
            try{sd.RefreshMountedFolder();}catch(IOException){rejected=true;}
            Check(rejected&&Object.ReferenceEquals(old,Card(sd))&&File.Exists(Path.Combine(path,"DIR","PC.BIN")),"directory conflict lost PC data");
            q.Next=DlgResult.No;sd.RefreshMountedFolder();
            // Build failure after host writes rolls back files and retains journal.
            Guest(sd,"RENAMED.TRD",(byte)'R');s=Card(sd).HostFolderStream;
            var original=File.ReadAllBytes(Path.Combine(path,"RENAMED.TRD"));rejected=false;
            try{s.CommitChanges(delegate(string backup){
                bool blocked=false;try{File.WriteAllText(Path.Combine(path,"RENAMED.TRD"),"PC DURING SAVE");}catch(IOException){blocked=true;}
                Check(blocked,"installed guest file was writable during transaction");
                throw new IOException("Injected snapshot build failure");
            });}catch(IOException){rejected=true;}
            Check(rejected&&s.HasChanges&&original.SequenceEqual(File.ReadAllBytes(Path.Combine(path,"RENAMED.TRD"))),"build rollback lost file/session");
            q.Next=DlgResult.Yes;Check(sd.RefreshMountedFolder()>=0&&File.ReadAllBytes(Path.Combine(path,"RENAMED.TRD"))[0]=='R',"retry after rollback failed");
            // Failed rebuild on the discard path must not discard first.
            Guest(sd,"RENAMED.TRD",(byte)'F');old=Card(sd);q.Next=DlgResult.No;
            var sourceField=typeof(HostFolderFat32Stream).GetField("m_sourceFolder",F);
            sourceField.SetValue(Card(sd).HostFolderStream,path+"-missing");rejected=false;
            try{sd.RefreshMountedFolder();}catch(IOException){rejected=true;}
            finally{sourceField.SetValue(Card(sd).HostFolderStream,path);}
            Check(rejected&&sd.HasPendingChanges&&Object.ReferenceEquals(old,Card(sd)),"failed discard refresh lost session");
            Check(sd.RefreshMountedFolder()>=0&&!sd.HasPendingChanges,"discard retry failed");
            // Malformed filesystem still offers explicit discard rather than blocking all refreshes.
            s=Card(sd).HostFolderStream;var boot=Read(s,0);boot[3]^=1;Write(s,0,boot);q.Next=DlgResult.No;
            Check(sd.RefreshMountedFolder()>=0&&!sd.HasPendingChanges,"damaged-session discard blocked");
            File.WriteAllText(Path.Combine(path,"RENAMED.TRD"),"CHANGED PC SIZE");
            rejected=false;try{Card(sd).HostFolderStream.ExportImage(Path.Combine(scratch,"unsafe.img"));}catch(IOException){rejected=true;}
            Check(rejected&&!File.Exists(Path.Combine(scratch,"unsafe.img")),"export silently mixed unreadable PC bytes");
        } finally {sd.BusDisconnect();}
    }
    private static void PowerCycle(string scratch,Query q)
    {
        using(var vm=new VirtualMachine(new Host(),null)) {
            vm.Spectrum.BusManager.Init(vm.Spectrum,true);
            var sd=vm.Spectrum.BusManager.FindDevice<ZsdPentEvo>();
            if(sd==null){vm.Spectrum.BusManager.Disconnect();sd=new ZsdPentEvo();vm.Spectrum.BusManager.Add(sd);Check(vm.Spectrum.BusManager.Connect(),"connect");}
            var path=Fresh(scratch,"vm");sd.MountHostFolder(path);
            var model=(MainViewModel)FormatterServices.GetUninitializedObject(typeof(MainViewModel));
            typeof(MainViewModel).GetField("m_vm",F).SetValue(model,vm);
            var command=(ISuccessCommand)typeof(ZsdPentEvo).GetField("refreshFolderCommand",F).GetValue(sd);
            var mem=vm.Spectrum.BusManager.FindDevice<ZXMAK2.Engine.Interfaces.IMemoryDevice>();
            int page=mem.RamPages.Length-1;mem.RamPages[page][1000]=0x5A;
            Guest(sd,"TEST.TRD",(byte)'C');q.Next=DlgResult.Cancel;
            q.BeforeShow=delegate{Check(!vm.IsRunning,"guest running during refresh prompt");};
            model.ExecuteMediaChange(command,null);
            Check(mem.RamPages[page][1000]==0x5A&&sd.HasPendingChanges,"cancel cold-started/lost changes");
            File.WriteAllText(Path.Combine(path,"TEST.TRD"),"PC CONFLICT");q.Next=DlgResult.Yes;
            model.ExecuteMediaChange(command,null);Check(mem.RamPages[page][1000]==0x5A&&sd.HasPendingChanges,"error cold-started/lost changes");
            q.Next=DlgResult.No;model.ExecuteMediaChange(command,null);
            Check(mem.RamPages[page][1000]==0&&!sd.HasPendingChanges&&sd.IsMediaMounted,"success did not power cycle/keep SD");
            Check(!vm.IsRunning,"paused VM unexpectedly resumed");
            Guest(sd,"TEST.TRD",(byte)'U');q.Next=DlgResult.Cancel;
            vm.DoRun();model.ExecuteMediaChange(command,null);
            Check(vm.IsRunning&&sd.HasPendingChanges,"running VM did not resume after cancel");
            vm.DoStop();q.Next=DlgResult.No;
            vm.DoRun();model.ExecuteMediaChange(command,null);
            Check(vm.IsRunning&&!sd.HasPendingChanges&&sd.IsMediaMounted,"running VM did not resume after successful refresh");
            vm.DoStop();
            q.BeforeShow=null;
        }
    }
    private static void Dialog(string output)
    {
        var type=typeof(ZXMAK2.Host.WinForms.Services.UserQuery).GetNestedType("ActionChoiceDialog",BindingFlags.NonPublic);
        using(var form=(Form)Activator.CreateInstance(type,new object[]{"The SD session has unsaved changes.\n\nThe emulated machine will restart after a successful refresh.","Refresh SD folder?","Refresh (Save changes)","Refresh (Without saving)","Cancel"})) {
            form.CreateControl();form.PerformLayout();
            var buttons=Find(form).OfType<Button>().ToArray();
            Check(buttons.Length==3&&buttons.Select(x=>x.Text).SequenceEqual(new[]{"Refresh (Save changes)","Refresh (Without saving)","Cancel"}),"dialog labels/count");
            Check(((Button)form.AcceptButton).DialogResult==DialogResult.Yes&&((Button)form.CancelButton).DialogResult==DialogResult.Cancel,"Enter/Escape wrong");
            foreach(var button in buttons)Check(button.Right<=button.Parent.ClientSize.Width&&button.Width>=TextRenderer.MeasureText(button.Text,button.Font).Width,"button clipped");
            // Render the dialog content without showing a desktop window.
            using(var surface=new UserControl()) {
                var content=form.Controls[0];form.Controls.Remove(content);content.Dock=DockStyle.Top;
                surface.Controls.Add(content);surface.Size=content.GetPreferredSize(System.Drawing.Size.Empty);
                surface.CreateControl();content.PerformLayout();surface.PerformLayout();
                using(var bmp=new System.Drawing.Bitmap(surface.Width,surface.Height)){surface.DrawToBitmap(bmp,surface.ClientRectangle);bmp.Save(Path.Combine(output,"refresh-dialog.png"));}
            }
        }
    }
    private static System.Collections.Generic.IEnumerable<Control> Find(Control root){foreach(Control child in root.Controls){yield return child;foreach(var nested in Find(child))yield return nested;}}
    [STAThread] private static int Main(string[] args)
    {
        string app=ZXMAK2.PortablePaths.Root,scratch=Path.Combine(app,"refresh-test-"+Guid.NewGuid().ToString("N"));
        var names=new[]{"ZXMAK2.vmz","ZXMAK2.vmide","ZXMAK2.cmos"};var saved=names.Select(n=>File.Exists(Path.Combine(app,n))?File.ReadAllBytes(Path.Combine(app,n)):null).ToArray();
        try {
            Directory.CreateDirectory(scratch);var q=new Query();var messages=new Messages();
            var resolver=Locator.Resolve<IResolver>();resolver.RegisterInstance<IUserQuery>(null,q);resolver.RegisterInstance<IUserMessage>(null,messages);
            Cases(scratch,q);PowerCycle(scratch,q);Dialog(args[0]);
            Check(q.ActionCalls>0,"action query unused");Check(messages.Errors==1,"unexpected UI error count");
            Console.WriteLine("PASS refresh save/discard/cancel/PC edits/TRD/tree/collisions/rollback/invalid FAT/cold-start/dialog: {0} checks",checks);return 0;
        }catch(Exception e){Console.WriteLine(e);return 1;}
        finally{for(int i=0;i<names.Length;i++){var path=Path.Combine(app,names[i]);if(saved[i]!=null)File.WriteAllBytes(path,saved[i]);else if(File.Exists(path))File.Delete(path);}if(Directory.Exists(scratch))Directory.Delete(scratch,true);}
    }
}
