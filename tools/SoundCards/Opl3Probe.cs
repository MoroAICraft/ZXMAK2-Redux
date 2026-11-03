// Run only in a disposable portable runtime. No external disks or host audio.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Forms;
using System.Xml;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Hardware;
using ZXMAK2.Hardware.Evo;
using ZXMAK2.Host.WinForms.Tools;
using ZXMAK2.Host.WinForms.Views.Configuration.Devices;

static class Opl3Probe
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    const string Label="ZX OPL3 (UR4QBP)";
    static int checks;
    static void Check(bool ok,string msg){checks++;if(!ok)throw new Exception(msg);}
    static object Field(object o,string n){return o.GetType().GetField(n,Private).GetValue(o);}
    static object Call(object o,string n,params object[] args){return o.GetType().GetMethod(n,Private|BindingFlags.Public).Invoke(o,args);}
    static string Hash(short[] samples){byte[] bytes=new byte[samples.Length*2];Buffer.BlockCopy(samples,0,bytes,0,bytes.Length);using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}
    sealed class Chip : IDisposable
    {
        public readonly IntPtr P=Create();
        public Chip(){Check(P!=IntPtr.Zero,"native create");}
        public void Reg(int r,int v){Write(P,(uint)((r>>8)*2),(byte)r);Write(P,(uint)((r>>8)*2+1),(byte)v);}
        public short[] Samples(int count){var data=new short[2*count];Generate(P,data,(uint)count);return data;}
        public void Dispose(){Destroy(P);}
    }
    [DllImport("ymfm_opl3.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="ymfm_opl3_create")]static extern IntPtr Create();
    [DllImport("ymfm_opl3.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="ymfm_opl3_destroy")]static extern void Destroy(IntPtr p);
    [DllImport("ymfm_opl3.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="ymfm_opl3_reset")]static extern void Reset(IntPtr p);
    [DllImport("ymfm_opl3.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="ymfm_opl3_advance")]static extern void Advance(IntPtr p,ulong clocks);
    [DllImport("ymfm_opl3.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="ymfm_opl3_read")]static extern byte Read(IntPtr p,uint offset);
    [DllImport("ymfm_opl3.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="ymfm_opl3_write")]static extern void Write(IntPtr p,uint offset,byte v);
    [DllImport("ymfm_opl3.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="ymfm_opl3_generate")]static extern void Generate(IntPtr p,[Out]short[] samples,uint count);

    sealed class Fixture:IDisposable
    {
        public readonly Spectrum Machine=new Spectrum();
        public readonly MemoryPentEvo Memory=new MemoryPentEvo();
        public readonly UlaPentEvo Ula=new UlaPentEvo();
        public readonly ZxOpl3Ur4qbpDevice Board=new ZxOpl3Ur4qbpDevice();
        public Fixture(int rate=44100,bool multi=false,bool opl3=true)
        {
            var bus=Machine.BusManager;bus.Init(Machine,true);bus.Disconnect();bus.Clear();
            bus.SampleRate=rate;bus.Add(Memory);bus.Add(Ula);
            if(opl3)bus.Add(Board);if(multi)bus.Add(new ZxMultiSoundDevice());
            Check(bus.Connect(),"bus connect");Machine.DebugReset();Memory.ResetState();Loop();
        }
        public void Loop(){Memory.WRMEM_DBG(0x4000,0x18);Memory.WRMEM_DBG(0x4001,0xFE);Machine.CPU.regs.PC=0x4000;Machine.CPU.RDMEM(0x4000);}
        public void Port(int p,int v){Machine.CPU.WRPORT((ushort)p,(byte)v);}
        public byte In(int p=0xC4){return Machine.CPU.RDPORT((ushort)p);}
        public void Reg(int r,int v){Port(0xC4+(r>>8)*2,r);Port(0xC5+(r>>8)*2,v);}
        public uint[] Frame(){Machine.ExecuteFrame();return (uint[])Board.AudioBuffer.Clone();}
        public void Speed(int speed){Memory.SHADOW=true;Port(0xFF77,speed==4?0xAB:0xA3);Memory.TURBO=speed==1;Memory.LatchClockAtRefresh();Check(Memory.CpuClockMultiplier==speed,"speed selected");Loop();}
        public void Dispose(){Machine.BusManager.Disconnect();Machine.Dispose();}
    }
    static void Tone(Action<int,int> reg,int channel=0,int pan=0x30)
    {
        int bank=channel/9*256,ch=channel%9,op=ch/3*8+ch%3;
        foreach(int slot in new[]{op,op+3}){reg(bank+0x20+slot,0x21);reg(bank+0x40+slot,slot==op?63:0);reg(bank+0x60+slot,0xF0);reg(bank+0x80+slot,0x0F);}
        reg(bank+0xC0+ch,pan);reg(bank+0xA0+ch,0x44);reg(bank+0xB0+ch,0x32); // FNUM 580, block4: ~440 Hz.
    }
    static int Swing(short[] data,int channel){var a=data.Where((x,i)=>(i&1)==channel).ToArray();return a.Length==0?0:a.Max()-a.Min();}
    static double Frequency(List<short> data,int rate)
    {int first=-1,last=0,count=0;for(int i=rate/5;i<data.Count;i++)if(data[i-1]<=0&&data[i]>0){if(first<0)first=i;last=i;count++;}return count<2?0:(count-1)*rate/(double)(last-first);}
    static void NativeTests()
    {
        using(var c=new Chip())
        {
            Check(Read(c.P,0)==0,"YMF262 initial status 00, no OPL4 ID");
            for(uint p=1;p<4;p++)Check(Read(c.P,p)==255,"unsupported read follows ymfm FF");
            c.Reg(2,255);c.Reg(4,1);Advance(c.P,1151);Check(Read(c.P,0)==0,"timer1 before 1152");
            Advance(c.P,1);Check(Read(c.P,0)==0xC0,"timer1 exact 1152 clocks");
            c.Reg(4,0x80);Check(Read(c.P,0)==0,"IRQ reset clears status");
            Advance(c.P,1152);Check(Read(c.P,0)==0xC0,"timer repeats without audio generation");
            c.Reg(4,0x40);c.Reg(4,0x80);c.Reg(4,0x41);Advance(c.P,10000);Check(Read(c.P,0)==0,"timer1 mask");
            Reset(c.P);c.Reg(3,255);c.Reg(4,2);Advance(c.P,4607);Check(Read(c.P,0)==0,"timer2 before 4608");
            Advance(c.P,1);Check(Read(c.P,0)==0xA0,"timer2 exact 4608 clocks");
            Reset(c.P);Check(Read(c.P,0)==0,"reset clears timer status");Advance(c.P,100000);Check(Read(c.P,0)==0,"reset cancels timers");
            Tone(c.Reg);var opl2=c.Samples(5000);Check(Swing(opl2,0)>1000&&Swing(opl2,1)>1000,"OPL2 compatibility audible on A/B");
            Reset(c.P);Check(c.Samples(1000).All(x=>x==0),"reset clears voices");
        }
        for(int channel=0;channel<18;channel++)using(var c=new Chip())
        {c.Reg(0x105,1);Tone(c.Reg,channel);Check(Swing(c.Samples(5000),0)>1000,"OPL3 channel "+channel);}
        foreach(int pan in new[]{0x10,0x20,0x40,0x80})using(var c=new Chip())
        {
            c.Reg(0x105,1);Tone(c.Reg,9,pan);var s=c.Samples(5000);
            Check((Swing(s,0)>100)==(pan==0x10),"A output pan "+pan);
            Check((Swing(s,1)>100)==(pan==0x20),"B output pan "+pan);
        }
        using(var a=new Chip())using(var b=new Chip())
        {
            a.Reg(0x105,1);b.Reg(0x105,1);Tone(a.Reg);Tone(b.Reg);
            var whole=a.Samples(6000);var pieces=new List<short>();foreach(int n in new[]{1,17,983,4999})pieces.AddRange(b.Samples(n));
            Check(whole.SequenceEqual(pieces),"native output independent of chunk size");
            Console.WriteLine("YMF262 18 channels / A/B only / native chunk SHA256: "+Hash(whole));
            a.Reg(0x104,1);Tone(a.Reg,3);a.Reg(0xC0,0x31);a.Reg(0xC3,0x31);
            Check(Swing(a.Samples(5000),0)>100,"4-op connection produces output");
            Reset(a.P);Tone(a.Reg,6);a.Reg(0xBD,0x30);Check(Swing(a.Samples(5000),0)>100,"rhythm bass drum output");
        }
        Console.WriteLine("Native status/timer boundaries/reset/masks/OPL2/OPL3/4-op/rhythm PASS");
    }
    static void BusTests()
    {
        using(var f=new Fixture())
        {
            var ev=(EventManager)((IBusManager)f.Machine.BusManager).Events;
            foreach(string name in new[]{"m_mapReadPort","m_mapWritePort"})
            {
                var map=(Array)Field(ev,name);
                for(int p=0;p<65536;p++)
                {
                    var d=map.GetValue(p) as Delegate;
                    bool hit=d!=null&&d.GetInvocationList().Any(x=>Object.ReferenceEquals(x.Target,f.Board));
                    Check(hit==((p&0xFC)==0xC4),"exact decoder "+name+" "+p);
                }
            }
            var ack=(Delegate)Field(ev,"m_intAck");Check(ack==null||!ack.GetInvocationList().Any(x=>Object.ReferenceEquals(x.Target,f.Board)),"/M1 interrupt acknowledge excluded");
            for(int high=0;high<256;high++)for(int lo=0;lo<4;lo++)Check(f.In((high<<8)|0xC4|lo)==(lo==0?0:255),"read upper alias");
            foreach(bool dos in new[]{false,true})
            {
                f.Memory.CPM=dos;f.Memory.DOSEN=dos;Check(f.Memory.DOSEN==dos,"DOS state set");
                f.Reg(4,0x60);f.Reg(4,0x80);f.Reg(2,255);f.Reg(4,1);
                f.Machine.CPU.Tact+=3000;Check((f.In()&0xE0)==0xC0,"DOSEN does not gate OPL3");
            }
            object[] read={(ushort)0xC4,(byte)0x55,true};Call(f.Board,"ReadPort",read);Check((byte)read[1]==0x55,"handled bus preserved");
            f.Board.ResetState();Check(f.In()==0,"physical reset");
            // A guest Z80 timer detector: real OUT/IN instructions and bounded polling.
            // No ROM/OS file patched. Results are written into this disposable RAM.
            foreach(int speed in new[]{1,2,4})
            {
                f.Speed(speed);var code=new List<byte>();
                Action<int,int> wr=(r,v)=>{code.AddRange(new byte[]{0x01,0xC4,0x00,0x3E,(byte)r,0xED,0x79,0x0C,0x3E,(byte)v,0xED,0x79});};
                code.Add(0xF3);wr(4,0x60);wr(4,0x80);
                code.AddRange(new byte[]{0x0D,0xED,0x78,0x32,0x00,0x60});wr(2,255);wr(4,1);
                code.AddRange(new byte[]{0x0D,0x16,0xFF});int loop=code.Count;
                code.AddRange(new byte[]{0xED,0x78,0xE6,0xC0,0xFE,0xC0,0x28,0x03,0x15,0x20,0x00});
                code[code.Count-1]=(byte)(loop-code.Count);
                code.AddRange(new byte[]{0x32,0x01,0x60});int stop=0x5000+code.Count;code.AddRange(new byte[]{0x18,0xFE});
                for(int i=0;i<code.Count;i++)f.Memory.WRMEM_DBG((ushort)(0x5000+i),code[i]);
                f.Machine.CPU.regs.PC=0x5000;int cycles=0;
                while(f.Machine.CPU.regs.PC!=stop&&cycles++<5000)f.Machine.BusManager.ExecCycle();
                Check(cycles<5000,"guest detector terminates");Check(f.Memory.RDMEM_DBG(0x6000)==0&&f.Memory.RDMEM_DBG(0x6001)==0xC0,"Z80 detects OPL3 at speed "+speed);
                Console.WriteLine("Guest Z80 detector {0:F1} MHz: initial={1:X2}, timer={2:X2}, instructions={3}",speed*3.5,f.Memory.RDMEM_DBG(0x6000),f.Memory.RDMEM_DBG(0x6001),cycles);
                f.Board.ResetState();
            }
        }
        Console.WriteLine("All 65536 read/write addresses, DOSEN, handled, /M1 and guest detector PASS");
    }
    static void AudioTests()
    {
        // Status polling must not change resampling or advance extra samples.
        foreach(int rate in new[]{22050,44100,48000,96000})using(var a=new Fixture(rate))using(var b=new Fixture(rate))
        {
            a.Reg(0x105,1);b.Reg(0x105,1);Tone(a.Reg);Tone(b.Reg);
            var ea=(EventManager)((IBusManager)a.Machine.BusManager).Events;
            var eb=(EventManager)((IBusManager)b.Machine.BusManager).Events;
            long start=a.Machine.CPU.Tact,span=a.Ula.FrameTactCount;
            for(int i=1;i<2048;i++){b.Machine.CPU.Tact=start+span*i/2048;b.In();}
            a.Machine.CPU.Tact=b.Machine.CPU.Tact=start+span;ea.EndFrame();eb.EndFrame();
            Check(a.Board.AudioBuffer.SequenceEqual(b.Board.AudioBuffer),"polling chunk PCM invariant "+rate);
        }
        foreach(int rate in new[]{22050,44100,48000,96000})foreach(int speed in new[]{1,2,4})using(var f=new Fixture(rate))
        {
            f.Speed(speed);f.Reg(0x105,1);Tone(f.Reg,9);var data=new List<short>();
            for(int i=0;i<35;i++)data.AddRange(f.Frame().Select(x=>(short)x));
            double hz=Frequency(data,rate);Check(hz>438&&hz<442,"pitch independent of host sample/CPU rate: "+hz);
            f.Board.Volume=0;f.Frame();Check(f.Frame().All(x=>x==0),"mute outputs zero after settling");
            f.Reg(2,255);f.Reg(4,1);f.Machine.CPU.Tact+=3000;Check((f.In()&0xC0)==0xC0,"muted timers keep running");
            f.Board.Volume=100;Check(f.Frame().Any(x=>x!=0),"unmute resumes voice");
            f.Machine.DebugReset();f.Frame();Check(f.Frame().All(x=>x==0),"guest reset silences board");
            Console.WriteLine("Host {0} Hz / CPU {1:F1} MHz: tone {2:F3} Hz; mute/reset PASS",rate,speed*3.5,hz);
        }
        using(var a=new Fixture())using(var b=new Fixture(44100,true))
        {
            a.Reg(0x105,1);b.Reg(0x105,1);Tone(a.Reg);Tone(b.Reg);
            // Ordinary MultiSound YM writes do not touch the OPL3 register latch.
            b.Port(0xFFFD,0xFE);b.Port(0xFFFD,0);b.Port(0xBFFD,0x57);
            Check(b.In(0xFFFD)==0x57,"MultiSound YM remains readable beside OPL3");
            for(int i=0;i<20;i++)Check(a.Frame().SequenceEqual(b.Frame()),"MultiSound does not alter OPL3 PCM");
        }
    }
    static void Select(ComboBox c,string text){for(int i=0;i<c.Items.Count;i++)if(c.Items[i].ToString()==text){c.SelectedIndex=i;return;}throw new Exception("No slot item "+text);}
    static void UiTests()
    {
        foreach(int slot in new[]{1,2})using(var f=new Fixture())using(var ui=new CtlSettingsPentEvoZxBus())
        {
            f.Machine.BusManager.Disconnect();f.Machine.BusManager.Remove(f.Board);
            ui.Initialize(f.Machine.BusManager,null,f.Ula);
            ((CheckBox)Field(ui,"m_slot"+slot+"Enabled")).Checked=true;Select((ComboBox)Field(ui,"m_slot"+slot+"Device"),Label);ui.Apply();
            Check(ui.IsOpl3BoardEnabled,"slot selected");Check(!ui.IsAutomaticMultiSoundYmActive,"OPL3 alone does not disable internal AY/TSFM");
            ((NumericUpDown)Field(ui,"m_opl3Volume")).Value=67;ui.Apply();
            var b=f.Machine.BusManager.FindDevice<ZxOpl3Ur4qbpDevice>();Check(b!=null&&b.Volume==67,"volume apply");
            int other=3-slot;((CheckBox)Field(ui,"m_slot"+other+"Enabled")).Checked=true;
            foreach(string name in new[]{"ZXM-MoonSound Rev.01","ZX-MultiSound Max","ZX OmniSound"})
            {Select((ComboBox)Field(ui,"m_slot"+other+"Device"),name);string before=MachineSettingsPolicy.Snapshot(f.Machine.BusManager).OuterXml;bool rejected=false;try{ui.Apply();}catch(InvalidOperationException){rejected=true;}Check(rejected&&before==MachineSettingsPolicy.Snapshot(f.Machine.BusManager).OuterXml,"atomic conflict rejection "+name);}
            Select((ComboBox)Field(ui,"m_slot"+other+"Device"),"ZX-MultiSound Rev.A2");ui.Apply();
            Check(f.Machine.BusManager.FindDevice<ZxMultiSoundDevice>()!=null,"ordinary MultiSound both slot orders");
            var snapshot=MachineSettingsPolicy.Snapshot(f.Machine.BusManager);f.Machine.BusManager.Clear();f.Machine.BusManager.LoadConfigXml(snapshot);
            Check(f.Machine.BusManager.FindDevice<ZxOpl3Ur4qbpDevice>().Volume==67,"full bus XML reload volume");
            f.Machine.DebugReset();for(int i=0;i<3;i++)f.Machine.ExecuteFrame();
            f.Machine.BusManager.Disconnect();
            if(slot==1){ui.Size=new Size(600,650);EnsureHandles(ui);using(var image=new Bitmap(ui.Width,ui.Height)){ui.DrawToBitmap(image,ui.ClientRectangle);image.Save("opl3-settings.png");}}
        }
        using(var a=new Fixture())using(var b=new Fixture())
        {
            Check(!MachineSettingsPolicy.SoundHardwareChanged(a.Machine.BusManager,b.Machine.BusManager),"same hardware no reset");
            b.Board.Volume=35;Check(!MachineSettingsPolicy.SoundHardwareChanged(a.Machine.BusManager,b.Machine.BusManager),"volume only no reset");
            Check(MachineSettingsPolicy.TryApplyVolumes(a.Machine.BusManager,b.Machine.BusManager)&&a.Board.Volume==35,"live volume path");
            b.Machine.BusManager.Disconnect();b.Machine.BusManager.Remove(b.Board);
            Check(MachineSettingsPolicy.SoundHardwareChanged(a.Machine.BusManager,b.Machine.BusManager),"remove card triggers existing cold hardware apply");
        }
        foreach(BusDeviceBase conflicting in new BusDeviceBase[]{new ZxmMoonSoundDevice(),new ZxMultiSoundMaxDevice(),new ZxOmniSoundDevice()})using(var f=new Fixture())
        {f.Machine.BusManager.Disconnect();f.Machine.BusManager.Add(conflicting);Check(!f.Machine.BusManager.Connect(),"hand edited conflicting profile rejected");}
        Console.WriteLine("ZXBUS slots, conflict protection, XML persistence, volume/cold-apply policy PASS");
    }
    static void EnsureHandles(Control c){var handle=c.Handle;foreach(Control child in c.Controls)EnsureHandles(child);}
    static void Performance()
    {
        foreach(bool multi in new[]{false,true})foreach(bool opl3 in new[]{false,true})using(var f=new Fixture(44100,multi,opl3))
        {
            if(opl3){f.Reg(0x105,1);for(int ch=0;ch<18;ch++)Tone(f.Reg,ch);}
            for(int i=0;i<20;i++)f.Machine.ExecuteFrame();var sw=Stopwatch.StartNew();
            for(int i=0;i<250;i++)f.Machine.ExecuteFrame();sw.Stop();
            Console.WriteLine("Performance 250 frames MultiSound={0} OPL3={1}: {2:F2} ms/frame",multi,opl3,sw.Elapsed.TotalMilliseconds/250);
        }
    }
    static void Profile(string path)
    {
        using(var f=new Fixture())
        {
            f.Machine.BusManager.Disconnect();f.Machine.BusManager.Clear();var doc=new XmlDocument();doc.Load(path);
            f.Machine.BusManager.LoadConfigXml(doc.DocumentElement.SelectSingleNode("Bus"));
            Check(f.Machine.BusManager.FindDevice<ZxOpl3Ur4qbpDevice>()!=null,"test profile OPL3 present");
            f.Machine.DebugReset();for(int i=0;i<150;i++)f.Machine.ExecuteFrame();
            Console.WriteLine("Packaged BaseConf boot 150 frames, PC={0:X4}",f.Machine.CPU.regs.PC);
        }
    }
    [STAThread] static int Main(string[] args)
    {
        PortableBootstrap.Initialize();
        try{if(args.Length==2&&args[0]=="--profile")Profile(args[1]);else if(args.Length==1&&args[0]=="--native")NativeTests();else{NativeTests();BusTests();AudioTests();UiTests();Performance();}Console.WriteLine("PASS: {0} assertions",checks);return 0;}
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
