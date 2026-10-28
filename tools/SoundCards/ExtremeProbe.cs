// Disposable runtime only. Deterministic bus/clock/UI regression, not a claim
// of real-hardware listening or a full NedoOS/OmniPlayer integration test.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows.Forms;
using System.Xml;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Hardware;
using ZXMAK2.Hardware.Evo;
using ZXMAK2.Hardware.Spectrum;
using ZXMAK2.Host.WinForms.Views.Configuration.Devices;

static class ExtremeProbe
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static int checks;
    static void Check(bool b, string message) { ++checks; if(!b) throw new Exception(message); }
    static object Field(object o,string name) {return o.GetType().GetField(name,Private).GetValue(o);}
    static object Call(object o,string name,params object[] args) {return o.GetType().GetMethod(name,Private|BindingFlags.Public).Invoke(o,args);}
    sealed class Fixture : IDisposable
    {
        public readonly Spectrum Machine=new Spectrum();
        public readonly IMemoryDevice Memory;
        public readonly ZxmSoundCardExtreme Board=new ZxmSoundCardExtreme();
        public readonly SoundDeviceBase Fm;
        public Fixture(bool evo=false, bool covox=false)
        {
            Machine.BusManager.Init(Machine,true);Machine.BusManager.Disconnect();Machine.BusManager.Clear();
            Memory=evo?(IMemoryDevice)new MemoryPentEvo():new MemorySpectrum48();
            Machine.BusManager.Add((BusDeviceBase)Memory);
            Machine.BusManager.Add(evo?(BusDeviceBase)new UlaPentEvo():new UlaSpectrum48());Machine.BusManager.Add(Board);
            if(covox)Machine.BusManager.Add(new ZXMAK2.Hardware.General.CovoxMono());
            Check(Machine.BusManager.Connect(),"Extreme connect");
            Fm=(SoundDeviceBase)Field(Board,"m_fm");
            Machine.DebugReset();
            if(evo)((MemoryPentEvo)Memory).ResetState();
            Memory.WRMEM_DBG(0x4000,0x18);Memory.WRMEM_DBG(0x4001,0xFE);
            Machine.CPU.regs.PC=0x4000;Machine.CPU.RDMEM(0x4000);
        }
        public void Port(int a,int v){Machine.CPU.WRPORT((ushort)a,(byte)v);}
        public byte Read(int a=0xFFFD){return Machine.CPU.RDPORT((ushort)a);}
        public void Reg(int r,int v){Port(0xFFFD,r);Port(0xBFFD,v);}
        public void Saa(int r,int v,int a=0x05FF){Port(a,r);Port(a-0x100,v);}
        public uint[] Frame(){Machine.ExecuteFrame();return (uint[])Fm.AudioBuffer.Clone();}
        public void Dispose(){Machine.BusManager.Disconnect();Machine.Dispose();}
    }
    static void Decode()
    {
        using(var f=new Fixture())
        {
            Check((byte)Field(f.Board,"m_configuration")==14,"reset config E, chip0/read/FM+SAA off");
            Check(f.Board.InputClock==3500000,"reset clock");
            f.Reg(0,73);Check(f.Read()==73,"reset selects YM0 register");
            f.Port(0xFFFD,0xFF);f.Reg(0,22);Check(f.Read()==22,"independent YM1");
            f.Port(0xFFFC,0x80);Check(f.Read()==73,"BLKAY2 forces YM0");
            f.Reg(0,91);f.Port(0xFFFC,0);Check(f.Read()==22,"YM1 retained after forced YM0");
            f.Port(0xFFFD,0xFE);Check(f.Read()==91,"forced write went to YM0");
            // YM decode aliases across A8..13; all other low bytes rejected.
            for(int p=0;p<65536;p++)
            {
                f.Reg(0,0x35);f.Port(p,0x56);
                if((p&0xC0FF)==0x80FD) Check(f.Read()==0x56,"YM data alias "+p);
                else if((p&0xC0FF)!=0xC0FD) Check(f.Read()==0x35,"YM non-alias "+p);
                f.Port(0xFFFC,0); // scanning intentionally includes clock register aliases.
            }
            f.Board.YmPortsDisabled=true;
            var read=new object[]{(ushort)0xFFFD,(byte)0xA5,false};Call(f.Board,"ReadYm",read);
            Check(!(bool)read[2]&&(byte)read[1]==0xA5,"JP1 releases read bus");
            f.Port(0xFFFD,0xF6);Check((byte)Field(f.Board,"m_configuration")==6,"JP1 still allows board config");
            f.Port(0xFFFC,3);Check(f.Board.InputClock==4000000,"JP1 still allows clock config");
            f.Board.YmPortsDisabled=false;f.Port(0xFFFC,0);
            f.Reg(7,0);f.Reg(14,15);Check(f.Read()==255,"unconnected PA input high");
            f.Reg(7,64);f.Reg(14,15);Check(f.Read()==15,"PA output latch");
            f.Reg(0x30,0x78);Check(f.Read()==0,"FM registers not fabricated readable");
        }
        Console.WriteLine("Full 16-bit YM port decode / registers / JP1 PASS");
    }
    static void Peripheral()
    {
        using(var f=new Fixture())
        {
            var saa=Field(f.Board,"m_saa");var regs=(byte[])Field(saa,"m_registers");
            // Every port compared with the published Boolean decode.
            foreach(bool legacy in new[]{false,true})
            {
                f.Board.LegacySaaPorts=legacy;
                for(int p=0;p<65536;p++)
                {
                    Call(saa,"SetReg",0,(byte)0);Call(saa,"SetReg",1,(byte)0);
                    saa.GetType().GetProperty("RegAddr").SetValue(saa,(byte)0,null);
                    f.Port(p,1);
                    bool hit=legacy?(p&0xC0FF)==0x00FF:(p&0xCEFF)==0x04FF;
                    int addr=(byte)saa.GetType().GetProperty("RegAddr").GetValue(saa,null);
                    Check(addr==(hit&&(p&256)!=0?1:0),"SAA address mask "+p);
                    Check(regs[0]==(hit&&(p&256)==0?1:0),"SAA data mask "+p);
                }
            }
            f.Board.LegacySaaPorts=false;
            f.Machine.CPU.RDMEM(0);f.Saa(0,0x45);Check(regs[0]!=0x45,"low ordinary memory read blocks SAA");
            f.Machine.CPU.WRMEM(0x4005,0);f.Saa(0,0x45);Check(regs[0]!=0x45,"write does not unblock");
            f.Machine.CPU.RDMEM(0x4000);f.Saa(0,0x45);Check(regs[0]==0x45,"high ordinary read unblocks");
            f.Machine.CPU.RDMEM_M1(0);f.Saa(0,0x54);Check(regs[0]==0x45,"low M1 blocks");
            f.Machine.CPU.RDMEM_M1(0x4000);f.Saa(0,0x54);Check(regs[0]==0x54,"high M1 unblocks");
            Check(!(bool)saa.GetType().GetProperty("ClockEnabled").GetValue(saa,null),"clock stopped but bus writable");
            f.Port(0xFFFD,0xF6);Check((bool)saa.GetType().GetProperty("ClockEnabled").GetValue(saa,null),"SAA clock enabled");
            f.Machine.DebugReset();Check(regs[0]==0x54,"SAA survives CPU reset");
            Check(!(bool)saa.GetType().GetProperty("ClockEnabled").GetValue(saa,null),"reset stops SAA clock");
            f.Machine.CPU.RDMEM(0x4000);
            var dac=(byte[])Field(f.Board,"m_dac");
            for(int p=0;p<65536;p++)
            {
                for(int i=0;i<4;i++)dac[i]=0;
                f.Port(p,93);
                bool hit=(p&0xAF)==15||(p&255)==0x3F||(p&255)==0xFB;
                int channel=((p>>5)&2)|((p>>4)&1);
                for(int i=0;i<4;i++)Check(dac[i]==(hit&&i==channel?93:0),"DAC mask "+p);
            }
            f.Port(0x0F,45);f.Port(0xB3,98);Check(dac[0]==45,"B3 removed, no alias");
            f.Machine.DebugReset();Check(dac[0]==45,"TLC7226 holds on reset");
            f.Board.PentEvoJumperClosed=false;
            f.Board.GetType().GetProperty("IoDosInputHigh",Private).SetValue(f.Board,false,null);
            f.Saa(0,0x65);Check(regs[0]!=0x65,"physical IODOS low blocks standard mode");
            f.Board.PentEvoJumperClosed=true;f.Machine.CPU.RDMEM(0x4000);
            f.Saa(0,0x65);Check(regs[0]==0x65,"PentEvo bypasses IODOS as CPLD specifies");
        }
        Console.WriteLine("Full SAA/DAC decode, memory-read latch, physical reset, IODOS PASS");
    }
    static double Frequency(List<short> data,int rate)
    {
        int first=-1,last=-1,count=0;
        for(int i=rate/5;i<data.Count;i++)if(data[i-1]<=0&&data[i]>0){if(first<0)first=i;last=i;count++;}
        return count<2?0:(count-1)*rate/(double)(last-first);
    }
    static void Tone(Fixture f)
    {
        foreach(int slot in new[]{0,4,8,12}) {f.Reg(0x30+slot,1);f.Reg(0x40+slot,40);f.Reg(0x50+slot,31);f.Reg(0x60+slot,0);f.Reg(0x70+slot,0);f.Reg(0x80+slot,15);}
        f.Reg(0xA4,0x22);f.Reg(0xA0,0x69);f.Reg(0xB0,7);f.Reg(0x28,0xF0);
    }
    static void Clocks()
    {
        var frequencies=new List<double>();var durations=new List<int>();
        foreach(int clock in new[]{0,1,3})using(var f=new Fixture())
        {
            f.Port(0xFFFC,clock);f.Port(0xFFFD,0xFA);Tone(f);
            var samples=new List<short>();for(int i=0;i<40;i++)samples.AddRange(f.Frame().Select(x=>(short)x));
            double hz=Frequency(samples,44100);frequencies.Add(hz);Check(hz>10,"FM tone exists");
            var psg=(Array)Field(f.Board,"m_psg");
            for(int chip=0;chip<2;chip++)
            {
                f.Port(0xFFFD,0xFA|chip);
                foreach(int prescale in new[]{6,3,2})
                {
                    f.Port(0xFFFD,prescale==6?0x2D:prescale==3?0x2E:0x2F);
                    int rate=(int)psg.GetValue(chip).GetType().GetProperty("ChipFrequency").GetValue(psg.GetValue(chip),null);
                    Check(rate==f.Board.InputClock*2/(prescale*2/3),"SSG oscillator/prescaler");
                }
            }
            int time;
            using(var timer=new Fixture())
            {
            timer.Machine.CPU.Tact=0;((EventManager)((IBusManager)timer.Machine.BusManager).Events).BeginFrame();
            timer.Port(0xFFFC,clock);timer.Port(0xFFFD,0xFC); // FM DAC disabled; timers still run.
            timer.Reg(0x26,0xF0);timer.Reg(0x27,0x2A);
            for(time=1;time<40000;time++){timer.Machine.CPU.Tact=time;if((timer.Read()&2)!=0)break;}
            Check(time<40000,"timer B with FM disabled");durations.Add(time);
            ((EventManager)((IBusManager)timer.Machine.BusManager).Events).EndFrame();
            }
            Console.WriteLine("Clock {0}: FM={1:F3} Hz; timer B={2} CPU tacts",f.Board.InputClock,hz,time);
        }
        Check(Math.Abs(frequencies[1]/frequencies[0]-2.0/3.5)<0.01,"FM CPC ratio");
        Check(Math.Abs(frequencies[2]/frequencies[0]-4.0/3.5)<0.01,"FM Atari ratio");
        Check(Math.Abs(durations[1]/(double)durations[0]-3.5/2.0)<0.025,"timer CPC ratio");
        Check(Math.Abs(durations[2]/(double)durations[0]-3.5/4.0)<0.025,"timer Atari ratio");
        Console.WriteLine("Oscillators, both YM prescalers, real FM frequency and timers PASS");
    }
    static void Select(ComboBox combo,string text){for(int i=0;i<combo.Items.Count;i++)if(combo.Items[i].ToString()==text){combo.SelectedIndex=i;return;}throw new Exception("Missing slot choice");}
    static void Configuration()
    {
        foreach(int slot in new[]{1,2})using(var f=new Fixture())using(var ui=new CtlSettingsPentEvoZxBus())
        {
            f.Machine.BusManager.Disconnect();f.Machine.BusManager.Clear();
            var ula=new UlaPentEvo();f.Machine.BusManager.Add(ula);
            ui.Initialize(f.Machine.BusManager,null,ula);
            ((CheckBox)Field(ui,"m_slot"+slot+"Enabled")).Checked=true;
            Select((ComboBox)Field(ui,"m_slot"+slot+"Device"),"ZXM Extreme Rev.03");
            Check(ui.IsExtremeBoardEnabled&&ui.IsAutomaticMultiSoundYmActive,"UI enables Extreme and music override");
            ui.Apply();var b=f.Machine.BusManager.FindDevice<ZxmSoundCardExtreme>();
            Check(b!=null&&b.PentEvoJumperClosed&&!b.LegacySaaPorts,"default jumpers");
            ((CheckBox)Field(ui,"m_extremeLegacySaa")).Checked=true;
            ((CheckBox)Field(ui,"m_extremeDisableYm")).Checked=true;ui.Apply();
            ((NumericUpDown)Field(ui,"m_extremeVolume")).Value=67;ui.Apply();
            Check(b.LegacySaaPorts&&b.YmPortsDisabled&&!ui.IsAutomaticMultiSoundYmActive,"UI jumpers apply");
            var doc=new XmlDocument();doc.LoadXml("<Device/>");b.SaveConfigXml(doc.DocumentElement);
            var loaded=new ZxmSoundCardExtreme();loaded.LoadConfigXml(doc.DocumentElement);
            Check(loaded.LegacySaaPorts&&loaded.YmPortsDisabled&&loaded.PentEvoJumperClosed,"config XML roundtrip");
            Check(loaded.Volume==67&&b.SoundRenderers.All(x=>x.Volume==67),"board volume and child renderers persist");
            int other=slot==1?2:1;
            ((CheckBox)Field(ui,"m_slot"+other+"Enabled")).Checked=true;
            Select((ComboBox)Field(ui,"m_slot"+other+"Device"),"ZX-MultiSound Max");
            bool rejected=false;try{ui.Apply();}catch(InvalidOperationException){rejected=true;}
            Check(rejected&&f.Machine.BusManager.FindDevice<ZxMultiSoundMaxDevice>()==null,"reject conflict before modifying bus");
            Select((ComboBox)Field(ui,"m_slot"+other+"Device"),"ZXM-MoonSound Rev.01");ui.Apply();
            Check(f.Machine.BusManager.FindDevice<ZxmMoonSoundDevice>()!=null,"MoonSound has disjoint ports");
            var ts=new TurboSoundFmPro {MultiSoundSaaPortCompatibility=true};
            f.Machine.BusManager.Add(ts);rejected=false;try{ui.Apply();}catch(InvalidOperationException){rejected=true;}
            Check(rejected,"reject internal TSFM legacy SAA overlap when JP1 closed");
            f.Machine.BusManager.Remove(ts);
            if(slot==1){ui.Size=new Size(540,440);ui.CreateControl();using(var image=new Bitmap(ui.Width,ui.Height)){ui.DrawToBitmap(image,ui.ClientRectangle);image.Save("extreme-settings.png");}}
        }
        Console.WriteLine("ZXBUS slots 1/2, settings persistence, conflict validation PASS");
    }
    static void Evo()
    {
        var hz=new List<double>();
        foreach(int speed in new[]{1,2,4})using(var f=new Fixture(true))
        {
            var m=(MemoryPentEvo)f.Memory;
            m.SHADOW=true;f.Port(0xFF77,speed==4?0xAB:0xA3);m.TURBO=speed==1;m.LatchClockAtRefresh();
            Check(m.CpuClockMultiplier==speed,"BaseConf CPU clock selected");
            f.Memory.WRMEM_DBG(0x4000,0x18);f.Memory.WRMEM_DBG(0x4001,0xFE);
            f.Port(0xFFFD,0xFA);Tone(f);
            var samples=new List<short>();for(int i=0;i<40;i++)samples.AddRange(f.Frame().Select(x=>(short)x));
            hz.Add(Frequency(samples,44100));
            Console.WriteLine("ZX Evolution CPU {0:F1} MHz: FM {1:F3} Hz",3.5*speed,hz.Last());
            Check(hz.Last()>200&&hz.Last()<240,"BaseConf independent oscillator");
            // DOSEN does not gate the YM or PentEvo-jumper peripherals.
            m.CPM=true;Check(m.DOSEN,"CPM enters DOSEN");f.Machine.CPU.RDMEM(0x4000);
            f.Port(0xFFFD,0xFE);f.Reg(0,81);Check(f.Read()==81,"YM works with DOSEN");
            f.Saa(0,0x52);var regs=(byte[])Field(Field(f.Board,"m_saa"),"m_registers");
            Check(regs[0]==0x52,"SAA PentEvo works with DOSEN");
            // Standard mode follows actual ZXBUS /DOS by mapped bank, not DOSEN.
            f.Board.PentEvoJumperClosed=false;
            foreach(int port in new[]{0x04FF,0x14FF,0x24FF,0x34FF})
            {
                bool available=(bool)Call(m,"GetZxBusDosN",(ushort)port);
                Call(Field(f.Board,"m_saa"),"SetReg",0,(byte)0);
                f.Port(port+256,0);f.Port(port,0x37);
                Check(regs[0]==(available?0x37:0),"standard mode uses physical DOS signal");
            }
        }
        Check(hz.Max()-hz.Min()<0.2,"CPU turbo does not detune sound");
        using(var f=new Fixture(false,true))
        {
            f.Port(0xFB,255);f.Frame();
            var covox=f.Machine.BusManager.FindDevice<ZXMAK2.Hardware.General.CovoxMono>();
            Check(covox.AudioBuffer.All(x=>x==0),"standalone Covox not doubled on Extreme FB");
            Check(((byte[])Field(f.Board,"m_dac"))[3]==255,"Extreme receives FB");
        }
        Console.WriteLine("BaseConf 3.5/7/14 MHz, physical DOS gating and Covox ownership PASS");
    }
    static int Peak(uint[] wave,bool right)
    { return wave.Max(x=>Math.Abs((int)(short)(right?x>>16:x))); }
    static int Swing(uint[] wave,bool right)
    { return wave.Max(x=>(int)(short)(right?x>>16:x))-wave.Min(x=>(int)(short)(right?x>>16:x)); }
    static void Outputs()
    {
        using(var f=new Fixture())
        {
            var psg=(Array)Field(f.Board,"m_psg");
            foreach(int chip in new[]{0,1})
            {
                f.Port(0xFFFD,0xFE|chip);f.Reg(7,0x3E);f.Reg(0,100);f.Reg(1,0);f.Reg(8,15);f.Frame();
                var wave=((SoundDeviceBase)psg.GetValue(chip)).AudioBuffer;
                Check(Swing(wave,false)>1000&&Swing(wave,true)==0,"both PSG A channels left only");
                f.Reg(8,0);f.Reg(7,0x3B);f.Reg(4,100);f.Reg(5,0);f.Reg(10,15);f.Frame();f.Frame();
                wave=((SoundDeviceBase)psg.GetValue(chip)).AudioBuffer;
                Check(Swing(wave,true)>1000&&Swing(wave,false)==0,"both PSG C channels right only");
                f.Reg(10,0);
            }
            f.Port(0xFFFD,0xF6);f.Saa(0,0xFF);f.Saa(8,0x90);f.Saa(0x10,4);f.Saa(0x14,1);f.Saa(0x1C,1);
            var saa=(SoundDeviceBase)Field(f.Board,"m_saa");f.Frame();f.Frame();
            Check(Peak(saa.AudioBuffer,false)>100&&Peak(saa.AudioBuffer,true)>100,"SAA stereo audio");
            f.Port(0xFFFD,0xFE);f.Frame();f.Frame();
            Check(saa.AudioBuffer.All(x=>x==0),"SAA clock-off mutes renderer");
            f.Port(0xFFFD,0xF6);f.Frame();f.Frame();
            Check(Peak(saa.AudioBuffer,false)>100,"SAA resumes retained voice");
            f.Port(0xFFFD,0xFA);Tone(f);f.Frame();
            Check(Peak(f.Fm.AudioBuffer,false)>0&&f.Fm.AudioBuffer.All(x=>(short)x==(short)(x>>16)),"FM routes to both outputs");
            f.Port(0xFFFD,0xFE);f.Frame();f.Frame();Check(f.Fm.AudioBuffer.All(x=>x==0),"FM DAC off");
            f.Port(0xFFFD,0xFA);f.Frame();Check(Peak(f.Fm.AudioBuffer,false)>0,"FM resume without reprogramming");
            f.Port(0xFFFC,3);f.Machine.DebugReset();
            Check(f.Board.InputClock==3500000&&(int)psg.GetValue(0).GetType().GetProperty("ChipFrequency").GetValue(psg.GetValue(0),null)==1750000,"reset restores both oscillator paths");
        }
        Console.WriteLine("PSG resistor stereo, SAA/FM audio, mute/resume and reset PASS");
    }
    static void Guest(string folder)
    {
        using(var f=new Fixture(true))
        {
            byte[] code=File.ReadAllBytes(Path.Combine(folder,"AmMusic.bin"));
            byte[] music=File.ReadAllBytes(Path.Combine(folder,"Music","Module str","buggos party.str"));
            for(int i=0;i<code.Length;i++)f.Memory.WRMEM_DBG((ushort)(0x6000+i),code[i]);
            for(int i=0;i<music.Length;i++)f.Memory.WRMEM_DBG((ushort)(0xC000+i),music[i]);
            // Unchanged guest player routine; skip its LD HL,0 placeholder.
            byte[] init={0xF3,0x01,0xFC,0xFF,0x3E,0x01,0xED,0x79,0x21,0x00,0xC0,0xCD,0xFC,0x62,0x18,0xFE};
            for(int i=0;i<init.Length;i++)f.Memory.WRMEM_DBG((ushort)(0x5000+i),init[i]);
            f.Machine.CPU.regs.SP=0xBFFF;f.Machine.CPU.regs.PC=0x5000;f.Frame();
            Check(f.Machine.CPU.regs.PC==0x500E,"Mick guest init returns");
            byte[] play={0xCD,0xFF,0x62,0x18,0xFE};
            for(int i=0;i<play.Length;i++)f.Memory.WRMEM_DBG((ushort)(0x5000+i),play[i]);
            int active=0;var wave=new List<uint>();var watch=Stopwatch.StartNew();
            for(int i=0;i<150;i++)
            {
                f.Machine.CPU.regs.PC=0x5000;f.Frame();
                Check(f.Machine.CPU.regs.PC==0x5003,"Mick guest play returns");
                var psg=((SoundDeviceBase)((Array)Field(f.Board,"m_psg")).GetValue(0)).AudioBuffer;
                if(Swing(psg,false)>100||Swing(psg,true)>100)active++;wave.AddRange(psg);
            }
            watch.Stop();Check(active>140,"real guest module produces audio");
            Check(f.Board.InputClock==2000000,"guest OUT selects CPC clock");
            byte[] bytes=new byte[wave.Count*4];Buffer.BlockCopy(wave.ToArray(),0,bytes,0,bytes.Length);
            using(var sha=SHA256.Create())Console.WriteLine("Mick AmMusic/STPlayer, buggos party, 150 frames: active={0}, {1:F1} ms, PCM SHA256={2}",active,watch.Elapsed.TotalMilliseconds,BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-",""));
        }
    }
    static void Profile(string path)
    {
        using(var machine=new Spectrum())
        {
            machine.BusManager.Init(machine,true);machine.BusManager.Disconnect();machine.BusManager.Clear();
            var doc=new XmlDocument();doc.Load(path);machine.BusManager.LoadConfigXml(doc.DocumentElement.SelectSingleNode("Bus"));
            var board=machine.BusManager.FindDevice<ZxmSoundCardExtreme>();
            Check(board!=null,"packaged profile contains Extreme");
            Check(machine.BusManager.FindDevice<MemoryPentEvo>()!=null,"packaged profile is ZX Evolution");
            machine.DebugReset();for(int i=0;i<200;i++)machine.ExecuteFrame();
            var saved=new XmlDocument();saved.LoadXml("<Bus/>");machine.BusManager.SaveConfigXml(saved.DocumentElement);
            machine.BusManager.Disconnect();machine.BusManager.Clear();machine.BusManager.LoadConfigXml(saved.DocumentElement);
            board=machine.BusManager.FindDevice<ZxmSoundCardExtreme>();
            Check(board!=null&&board.PentEvoJumperClosed&&!board.LegacySaaPorts,"full bus roundtrip");
            for(int i=0;i<10;i++)machine.ExecuteFrame();
            Console.WriteLine("Packaged BaseConf ROM boot, 200 frames, bus XML reconnect: PASS; PC={0:X4}",machine.CPU.regs.PC);
        }
    }
    [STAThread] static int Main(string[] args)
    {
        PortableBootstrap.Initialize();
        try{if(args.Length==2&&args[0]=="--profile")Profile(args[1]);else{Decode();Peripheral();Clocks();Configuration();Evo();Outputs();if(args.Length>0)Guest(args[0]);}Console.WriteLine("PASS: {0} assertions",checks);return 0;}
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
