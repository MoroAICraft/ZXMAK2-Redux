// Run only inside a private disposable runtime, never in the user's install.
using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Drawing;
using System.Collections;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using ZXMAK2;
using ZXMAK2.Dependency;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Hardware;
using ZXMAK2.Hardware.Evo;
using ZXMAK2.Hardware.General;
using ZXMAK2.Hardware.Spectrum;
using ZXMAK2.Host.Entities;
using ZXMAK2.Host.Interfaces;
using ZXMAK2.Host.WinForms.Tools;
using ZXMAK2.Host.WinForms.Controls;
using ZXMAK2.Host.WinForms.Views;
using ZXMAK2.Model.Disk;
using ZXMAK2.Serializers;

static class PrefinalProbe
{
    static int checks;
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool ok, string text) { checks++; if (!ok) throw new Exception(text); }
    static object Field(object target, string name) { return target.GetType().GetField(name, Hidden).GetValue(target); }
    sealed class Query : IUserQuery, IActionUserQuery
    {
        public DlgResult Result = DlgResult.No; public int Choice = 0, Questions, Selections;
        public DlgResult Show(string m,string c,DlgButtonSet b,DlgIcon i) { Questions++; return Result; }
        public DlgResult ShowActions(string m,string c,string a,string b,string x) { Questions++; return Result; }
        public object ObjectSelector(object[] items,string caption) { Selections++; return Choice < 0 ? null : items[Choice]; }
        public bool QueryText(string c,string t,ref string s) { return false; }
        public bool QueryValue(string c,string t,string f,ref int n,int min,int max) { return false; }
    }
    sealed class Messages : IUserMessage
    {
        public int Errors; public string Last;
        public void Error(Exception e) { Errors++;Console.WriteLine(e); }
        public void ErrorDetails(Exception e) { Error(e); }
        public void Error(string s,params object[] a) { Errors++;Console.WriteLine(s,a); }
        public void Warning(Exception e) { Error(e); }
        public void Warning(string s,params object[] a) { Console.WriteLine(s,a); }
        public void Info(string s,params object[] a) { Last=string.Format(s,a); Console.WriteLine(Last); }
    }
    sealed class Vm : IVirtualMachine
    {
        public readonly Spectrum S = new Spectrum(); public int Cold, Warm, Saves; public bool FailSave;
        public Vm(BusDeviceBase board, bool sandbox = true)
        {
            S.BusManager.Init(S,sandbox);S.BusManager.Disconnect();S.BusManager.Clear();
            S.BusManager.Add(new MemorySpectrum128()); S.BusManager.Add(new UlaSpectrum128());
            S.BusManager.Add(board); Check(S.BusManager.Connect(),"VM connect"); S.DebugReset();
        }
        public IBus Bus { get { return S.BusManager; } }
        public bool IsRunning {get;private set;}
        public Size FrameSize {get{return Size.Empty;}}
        public event EventHandler FrameSizeChanged {add{} remove{}}
        public void DoRun(){IsRunning=true;} public void DoStop(){IsRunning=false;}
        public void DoPowerCycle(){Cold++; foreach(var p in S.BusManager.FindDevice<IMemoryDevice>().RamPages)Array.Clear(p,0,p.Length);S.DebugReset();}
        public void DoReset(){Warm++;S.DebugReset();} public void DoNmi(){}
        public void SaveConfig(){Saves++;if(FailSave){FailSave=false;throw new IOException("Injected save failure");}} public void Dispose(){S.BusManager.Disconnect();S.Dispose();}
    }
    static void Reject(string path,byte[] bytes)
    {
        bool threw=false;try{BaseConfMedia.Validate(path,bytes);}catch(InvalidDataException){threw=true;}
        Check(threw,"invalid accepted: "+path);
    }
    static void Validation()
    {
        foreach(var ext in new[]{"trd","SCL","tap","sna"})Check(BaseConfMedia.Supports("x."+ext),ext);
        foreach(var ext in new[]{"tzx","z80","spg","bmp","zip","img","exe"})Check(!BaseConfMedia.Supports("x."+ext),ext);
        Reject("x.tap",new byte[]{2,0,255});Reject("x.tap",new byte[]{0,0});Reject("x.trd",new byte[17]);
        Reject("x.scl",new byte[9]);Reject("x.sna",new byte[10]);
        var sna=new byte[49179];Reject("x.sna",sna);sna[24]=0x80;BaseConfMedia.Validate("x.sna",sna);
        var sn128=new byte[131103];BaseConfMedia.Validate("x.sna",sn128);sn128[49181]=5;Reject("x.sna",sn128);
        var manyBlocks=new byte[20000];for(int p=0;p<manyBlocks.Length;p+=4)manyBlocks[p]=2;
        Reject("pulse-explosion.tap",manyBlocks);
        Console.WriteLine("Media format/content validation PASS");
    }
    static void NoFlick()
    {
        using(var view=new MainView(null))
        {
            var router=(HostKeyboardRouter)Field(view,"_keyboardRouter");
            var on=(ToolStripMenuItem)Field(view,"menuViewVideoFilterNoFlick");
            var off=(ToolStripMenuItem)Field(view,"menuViewVideoFilterNone");
            on.Enabled=off.Enabled=true;int calls=0;
            on.Click+=delegate{on.Checked=true;off.Checked=false;calls++;};
            off.Click+=delegate{on.Checked=false;off.Checked=true;calls++;};
            on.Checked=false;
            Check(router.ExecuteShortcut(Keys.Control|Keys.F,false)&&on.Checked&&calls==1,"Ctrl+F on");
            Check(router.ExecuteShortcut(Keys.Control|Keys.F,false)&&!on.Checked&&calls==2,"Ctrl+F off");
            Check(on.ShortcutKeyDisplayString=="Ctrl+F","NoFlick shortcut label");
        }
        Console.WriteLine("Actual main menu Ctrl+F toggle twice PASS");
    }
    static void Snapshots()
    {
        var config=new XmlDocument();config.Load(PortablePaths.ConfigFile("machines.config"));
        using(var machine=new Spectrum())
        {
            machine.BusManager.Init(machine,true);
            machine.BusManager.LoadConfigXmlChecked(config.SelectSingleNode("/Machines/Bus[@name='ZX-Evo BSconf']"));
            foreach(int size in new[]{49179,131103})
            {
                var data=new byte[size];data[24]=0x80;data[25]=1;data[26]=3;data[27]=0x79;
                if(size==49179){data[27+0x4000]=0x34;data[27+0x4000+1]=0x92;}
                else {data[49179]=0x34;data[49180]=0x92;data[49181]=3;data[27+0x8000]=0x55;}
                BaseConfMedia.Validate("fixture.sna",data);
                using(var stream=new MemoryStream(data))machine.BusManager.LoadManager.GetSerializer(".sna").Deserialize(stream);
                Check(machine.CPU.regs.PC==0x9234,"SNA PC "+size);
                Check(machine.CPU.regs.SP==(size==49179?0x8002:0x8000),"SNA SP "+size);
                Check(machine.BusManager.FindDevice<IMemoryDevice>().RamPages[5][0]==0x79,"SNA RAM "+size);
                if(size>49179)Check(machine.BusManager.FindDevice<IMemoryDevice>().RamPages[3][0]==0x55,"SNA bank3");
            }
            machine.BusManager.Disconnect();
        }
        Console.WriteLine("BaseConf 48K/128K SNA PC/SP/RAM restoration PASS");
    }
    static void Disks(Query q,Messages msg)
    {
        using(var vm=new Vm(new FddPentEvo()))
        {
            var c=vm.S.BusManager.FindDevice<IBetaDiskDevice>(); var data=new byte[655360];
            string source=Path.Combine(PortablePaths.DataDirectory("Temp"),"test.trd");File.WriteAllBytes(source,data);
            Check(BaseConfMedia.OpenDisk(vm.S,source,data,false,q,msg),"mount A");
            Check(c.FDD[0].Present && q.Selections==0,"empty A automatic");
            c.FDD[0].ModifyFlag=(ModifyFlag)1;q.Result=DlgResult.Cancel;
            Check(!BaseConfMedia.OpenDisk(vm.S,source,data,false,q,msg)&&c.FDD[0].ModifyFlag!=(ModifyFlag)0,"cancel preserves dirty");
            q.Result=DlgResult.Yes;
            Check(BaseConfMedia.OpenDisk(vm.S,source,data,true,q,msg),"save+replace");
            Check(c.FDD[0].IsWP && Directory.GetFiles(PortablePaths.DataDirectory("Saves\\Floppy"),"*.udi",SearchOption.AllDirectories).Length>0,"UDI saved");
            q.Choice=1;Check(BaseConfMedia.OpenDisk(vm.S,source,data,false,q,msg)&&c.FDD[1].Present,"occupied A choose B");
            q.Choice=-1;Check(!BaseConfMedia.OpenDisk(vm.S,source,data,false,q,msg),"drive cancel");q.Choice=0;
            var scl=new byte[9+14+256];System.Text.Encoding.ASCII.GetBytes("SINCLAIR").CopyTo(scl,0);scl[8]=1;scl[9]=(byte)'X';scl[9+8]=(byte)'C';scl[22]=1;scl[23]=0x77;
            BaseConfMedia.Validate("new.scl",scl);int before=q.Questions;
            Check(BaseConfMedia.OpenDisk(vm.S,Path.ChangeExtension(source,"scl"),scl,false,q,msg),"SCL new disk");
            Check(q.Questions==before,"SCL append prompt leaked");
            var sector=new byte[256];c.FDD[0].ReadLogicalSector(1,0,1,sector); // data starts at track 1 (physical cyl0 side1)
            c.FDD[0].ReadLogicalSector(0,1,1,sector);Check(sector[0]==0x77,"SCL data");
            vm.S.CPU.WRPORT(0x13BD,1);Check(!((IHostFloppyVisibility)c).IsHostDriveVisible(0),"virtual A visibility");
            Check(BaseConfMedia.OpenDisk(vm.S,source,data,false,q,msg)&&c.FDD[1].Present,"virtual A redirected choice");
            Check(msg.Errors==0,"disk loader errors");
        }
        Console.WriteLine("Drive choice, virtual drive, Cancel, Save/UDI, separate SCL PASS");
    }
    static void Apply(Messages msg)
    {
        using(var vm=new Vm(new ZxmSoundCardExtreme()))
        {
            var board=vm.Bus.FindDevice<ZxmSoundCardExtreme>();vm.S.CPU.WRPORT(0xFFFD,0xF6);
            byte old=(byte)Field(board,"m_configuration");vm.DoRun();
            using(var form=new FormMachineSettings())
            {
                form.Init(null,vm);var staged=(BusManager)Field(form,"m_workBus");
                ((IList)Field(form,"m_ctlList")).Clear();staged.FindDevice<ZxmSoundCardExtreme>().Volume=73;
                typeof(FormMachineSettings).GetMethod("btnApply_Click",Hidden).Invoke(form,new object[]{null,EventArgs.Empty});
            }
            Check(vm.Cold==0 && vm.Warm==0 && board.Volume==73 && vm.IsRunning,"volume Apply restarted or paused");
            Check((byte)Field(board,"m_configuration")==old,"volume reset SAA configuration");
            foreach(bool legacy in new[]{true,false}) using(var form=new FormMachineSettings())
            {
                form.Init(null,vm);var staged=(BusManager)Field(form,"m_workBus");((IList)Field(form,"m_ctlList")).Clear();
                staged.FindDevice<ZxmSoundCardExtreme>().LegacySaaPorts=legacy;
                typeof(FormMachineSettings).GetMethod("btnApply_Click",Hidden).Invoke(form,new object[]{null,EventArgs.Empty});
            }
            Check(vm.Cold==2 && vm.IsRunning,"hardware Apply must power cycle and resume");
            foreach(var next in new BusDeviceBase[]{new TurboSoundFmPro(),new ZxmMoonSoundDevice(),new ZxMultiSoundDevice(),new ZxMultiSoundMaxDevice(),new ZxOmniSoundDevice(),new ZxmSoundCardExtreme()})
            {
                var multi=next as ZxMultiSoundCore;if(multi!=null){multi.AutomaticConfiguration=false;multi.GeneralSoundEnabled=false;}
                using(var form=new FormMachineSettings())
                {
                    form.Init(null,vm);var staged=(BusManager)Field(form,"m_workBus");((IList)Field(form,"m_ctlList")).Clear();
                    foreach(var d in staged.FindDevices<BusDeviceBase>().Where(d=>d.Category==BusDeviceCategory.Music).ToArray())staged.Remove(d);
                    staged.Add(next);int before=vm.Cold;
                    typeof(FormMachineSettings).GetMethod("btnApply_Click",Hidden).Invoke(form,new object[]{null,EventArgs.Empty});
                    Check(vm.Cold==before+1 && vm.IsRunning,"card swap "+next.GetType().Name);
                    Check(vm.Bus.FindDevices<BusDeviceBase>().Count(d=>d.Category==BusDeviceCategory.Music)==1,"stale card survived");
                }
                Console.WriteLine("Apply power cycle PASS: "+next.GetType().Name);
            }
            Check(msg.Errors==0,"Apply error");
            using(var form=new FormMachineSettings())
            {
                form.Init(null,vm);var staged=(BusManager)Field(form,"m_workBus");((IList)Field(form,"m_ctlList")).Clear();
                var live=vm.Bus.FindDevice<ZxmSoundCardExtreme>();bool oldLegacy=live.LegacySaaPorts;
                staged.FindDevice<ZxmSoundCardExtreme>().LegacySaaPorts=!oldLegacy;vm.FailSave=true;
                typeof(FormMachineSettings).GetMethod("btnApply_Click",Hidden).Invoke(form,new object[]{null,EventArgs.Empty});
                Check(vm.IsRunning && vm.Bus.FindDevice<ZxmSoundCardExtreme>().LegacySaaPorts==oldLegacy,"rollback or resume failed");
                Check(msg.Errors==1,"failure not reported");msg.Errors=0;
            }
        }
        Console.WriteLine("Actual settings Apply: volume/no reset, board changes/cold boot PASS");
    }
    static void Integration()
    {
        var keyPath=@"Software\ZXMAK2-PrefinalProbe-"+Guid.NewGuid().ToString("N");
        try
        {
            using(var root=Registry.CurrentUser.CreateSubKey(keyPath))
            {
                using(var trd=root.CreateSubKey(".trd"))trd.SetValue("","Unrelated.Default");
                var exe=Path.Combine(PortablePaths.Root,"ZXMAK2.exe");
                Check(PortableLaunch.RegisterAt(root,exe),"first registration");
                Check(!PortableLaunch.RegisterAt(root,exe),"registration not idempotent");
                using(var trd=root.OpenSubKey(".trd"))Check((string)trd.GetValue("")=="Unrelated.Default","default hijacked");
                using(var c=root.OpenSubKey("ZXMAK2.Portable."+PortableLaunch.Identity(exe)+@"\shell\open\command"))
                    Check((string)c.GetValue("")=="\""+exe+"\" \"%1\"","quote command");
            }
        }
        finally {Registry.CurrentUser.DeleteSubKeyTree(keyPath);}
        string path=Path.Combine(PortablePaths.DataDirectory("Temp"),"IPC тест.tap");File.WriteAllBytes(path,new byte[]{2,0,255,255});
        PortableLaunch.StartReceiver();
        try
        {
            PortableLaunch.Send(path);string received;Check(PortableLaunch.TryDequeue(out received)&&received==path,"IPC Unicode path");
            PortableLaunch.Send(path);
            using(var view=new MainView(null))
            {
                int opens=0;view.CommandOpenUri=new ZXMAK2.Mvvm.CommandDelegate(arg=>{Check(((Uri)arg).LocalPath==path,"queued URI");opens++;});
                typeof(MainView).GetField("_isFormShown",Hidden).SetValue(view,true);
                var open=typeof(MainView).GetMethod("OpenQueuedFile",Hidden);
                view.Enabled=false;open.Invoke(view,new object[]{null,EventArgs.Empty});
                Check(opens==0,"queue interrupted disabled/modal owner");view.Enabled=true;
                var filter=Field(view,"_keyboardMessages");var menu=Field(view,"menuFile");
                filter.GetType().GetMethod("OnOpened",Hidden).Invoke(filter,new object[]{menu,EventArgs.Empty});
                open.Invoke(view,new object[]{null,EventArgs.Empty});Check(opens==0,"queue interrupted open menu");
                filter.GetType().GetMethod("OnClosed",Hidden).Invoke(filter,new object[]{menu,EventArgs.Empty});
                open.Invoke(view,new object[]{null,EventArgs.Empty});Check(opens==1,"queue did not resume after menu");
            }
        }
        finally {PortableLaunch.StopReceiver();}
        Console.WriteLine("Open With isolated registry, defaults untouched, quoted command, IPC PASS");
    }
    static void RetainedMedia(Query q)
    {
        // Exercise real disk connect/disconnect, not the settings sandbox which
        // deliberately skips disk IO and could conceal a broken retention flag.
        using(var vm=new Vm(new ZxmSoundCardExtreme(), false))
        {
            vm.S.BusManager.Disconnect();var fdd=new FddPentEvo();vm.S.BusManager.Add(fdd);Check(vm.S.BusManager.Connect(),"FDD attach");
            var source=Path.Combine(PortablePaths.DataDirectory("Temp"),"retain.trd");File.WriteAllBytes(source,new byte[655360]);
            fdd.LoadManagers[0].OpenFileName(source,false);var sector=new byte[256];sector[0]=0x79;fdd.FDD[0].WriteLogicalSector(1,0,1,sector);fdd.FDD[0].ModifyFlag=(ModifyFlag)1;
            var state=MachineSettingsPolicy.Snapshot(vm.Bus);
            Check(MachineSettingsPolicy.SameFloppies(state,state.CloneNode(true),fdd.GetType().FullName),"qualified FDD type not matched");
            vm.DoRun();int prompts=q.Questions;
            using(var form=new FormMachineSettings())
            {
                form.Init(null,vm);var staged=(BusManager)Field(form,"m_workBus");((IList)Field(form,"m_ctlList")).Clear();
                staged.FindDevice<ZxmSoundCardExtreme>().LegacySaaPorts=true;
                typeof(FormMachineSettings).GetMethod("btnApply_Click",Hidden).Invoke(form,new object[]{null,EventArgs.Empty});
            }
            fdd=vm.Bus.FindDevice<FddPentEvo>();fdd.FDD[0].ReadLogicalSector(1,0,1,sector);
            Check(sector[0]==0x79 && fdd.FDD[0].ModifyFlag!=(ModifyFlag)0 && q.Questions==prompts,"unchanged floppy lost data/prompted");
            Check(!fdd.PreserveMediaOnReconnect,"retention scope leaked");fdd.FDD[0].ModifyFlag=ModifyFlag.None;
            var tape=new TapeDevice();Check(tape.Position==0,"empty tape position");
        }
        var pending=new XmlDocument();pending.LoadXml("<Bus><Device type='ZXMAK2.Hardware.Evo.ZsdPentEvo, ZXMAK2.Hardware' sdPath='old' sdImage='old' sdSource='Image'/></Bus>");
        var saved=new XmlDocument();saved.LoadXml("<Bus><Device type='ZXMAK2.Hardware.Evo.ZsdPentEvo, ZXMAK2.Hardware' sdPath='Data/new.img' sdImage='Data/new.img' sdSource='Image'/></Bus>");
        MachineSettingsPolicy.RefreshSavedMediaPaths(pending.DocumentElement,saved.DocumentElement,false,false);
        Check(pending.DocumentElement.FirstChild.Attributes["sdPath"].Value=="Data/new.img","save-as path stale");
        Console.WriteLine("Unchanged dirty floppy retention, save-as SD path, empty tape state PASS");
    }
    [STAThread] static int Main()
    {
        try
        {
            PortableBootstrap.Initialize();var q=new Query();var msg=new Messages();
            Locator.Resolve<IResolver>().RegisterInstance<IUserQuery>(null,q);
            Locator.Resolve<IResolver>().RegisterInstance<IUserMessage>(null,msg);
            Validation();NoFlick();Snapshots();Disks(q,msg);Apply(msg);RetainedMedia(q);Integration();SoundCardsProbe.MeasureLevels();
            Console.WriteLine("PASS Prefinal: "+checks+" checks");return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}

static partial class SoundCardsProbe
{
    public static void MeasureLevels()
    {
        double expected=0;
        for(int kind=0;kind<4;kind++)using(var f=new Fixture(kind,44100,false))
        {
            f.Select(0,true);Tone(f.Reg,7,0,0,0,31);double squares=0;long count=0;int peak=0;
            for(int frame=0;frame<120;frame++)
            {
                f.Frame();var pcm=f.Machine.BusManager.SoundFrame.GetBuffer();if(frame<20)continue;
                foreach(uint sample in pcm){int left=(short)sample,right=(short)(sample>>16);squares+=(double)left*left+(double)right*right;count+=2;peak=Math.Max(peak,Math.Max(Math.Abs(left),Math.Abs(right)));}
            }
            double rms=Math.Sqrt(squares/count);if(kind==0)expected=rms;
            Console.WriteLine("LEVEL board={0} final mixer RMS={1:F3} peak={2}",kind,rms,peak);
            Check(Math.Abs(rms-expected*(kind==3?1.4:1.0))<1.0,"unexpected final mixer level "+kind);
        }
    }
}
