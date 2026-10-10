// Private v88 regression harness, not part of the distributed emulator.
// Port/renderer tests are synthetic; they do not replace real OmniPlayer/hardware listening.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using ZXMAK2;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Hardware;
using ZXMAK2.Hardware.Evo;
using ZXMAK2.Hardware.Spectrum;
using ZXMAK2.Hardware.Circuits.Ata;

static partial class SoundCardsProbe
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static int checks;
    static void Check(bool ok, string what) { checks++; if (!ok) throw new Exception(what); }
    sealed class Fixture : IDisposable
    {
        public readonly Spectrum Machine = new Spectrum();
        public readonly MemorySpectrum48 Memory = new MemorySpectrum48();
        public readonly BusDeviceBase Board;
        public readonly SoundDeviceBase Fm;
        public Fixture(int kind, int rate, bool legacy)
        {
            Machine.BusManager.Init(Machine, true);
            Machine.BusManager.Disconnect(); Machine.BusManager.Clear();
            Board = kind == 0 ? (BusDeviceBase)new TurboSoundFmPro() : kind == 1 ? new ZxMultiSoundDevice() : kind == 2 ? new ZxMultiSoundMaxDevice() : (BusDeviceBase)new ZxOmniSoundDevice();
            var multi = Board as ZxMultiSoundCore;
            if (multi != null) { multi.AutomaticConfiguration = false; multi.GeneralSoundEnabled = false; }
            var owner = kind == 0 ? typeof(TurboSoundFmPro) : typeof(ZxMultiSoundCore);
            Fm = (SoundDeviceBase)owner.GetField("m_fm", Private).GetValue(Board);
            if (legacy)
            {
                var old = (SoundDeviceBase)Activator.CreateInstance(typeof(TurboSoundFmPro).Assembly.GetType("ZXMAK2.Hardware.Evo.TsFmFmRenderer"), true);
                owner.GetField("m_fm", Private).SetValue(Board, old);
                ((ISoundRenderer[])owner.GetField("m_renderers", Private).GetValue(Board))[2] = old;
                Fm = old;
            }
            Machine.BusManager.SampleRate = rate;
            Machine.BusManager.Add(Memory); Machine.BusManager.Add(new UlaSpectrum48()); Machine.BusManager.Add(Board);
            Check(Machine.BusManager.Connect(), "connect " + kind);
            Fm.Volume = 100;
            Memory.WRMEM_DBG(0x4000, 0x18); Memory.WRMEM_DBG(0x4001, 0xFE);
            Machine.DebugReset(); Machine.CPU.regs.PC = 0x4000;
        }
        public void Select(int chip, bool read) { Port(0xFFFD, (byte)(0xF8 | chip | (read ? 2 : 0))); }
        public void Port(ushort port, byte data) { Machine.CPU.WRPORT(port, data); }
        public byte Read() { return Machine.CPU.RDPORT(0xFFFD); }
        public void Reg(int reg, int value) { Port(0xFFFD, (byte)reg); Port(0xBFFD, (byte)value); }
        public uint[] Frame() { Machine.ExecuteFrame(); return (uint[])Fm.AudioBuffer.Clone(); }
        public void Dispose() { Machine.BusManager.Disconnect(); Machine.Dispose(); }
    }
    static void Tone(Action<int,int> write, int alg, int feedback, int dt, int ks, int attack)
    {
        foreach (int slot in new[] { 0,4,8,12 })
        {
            write(0x30 + slot, (dt << 4) | 1); write(0x40 + slot, 0x28);
            write(0x50 + slot, (ks << 6) | attack); write(0x60 + slot, 0); write(0x70 + slot, 0); write(0x80 + slot, 0x0F);
        }
        write(0xA4, 0x22); write(0xA0, 0x69); write(0xB0, (feedback << 3) | alg); write(0x28, 0xF0);
    }
    static string Hash(uint[] data) { using(var sha=SHA256.Create()) { var bytes=new byte[data.Length*4]; Buffer.BlockCopy(data,0,bytes,0,bytes.Length);return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-",""); } }
    static double Frequency(List<short> data, int rate)
    {
        int first=-1,last=-1,count=0;
        for(int i=rate/5;i<data.Count;i++) if(data[i-1]<=0 && data[i]>0) { if(first<0)first=i;last=i;count++; }
        return count<2?0:(count-1)*rate/(double)(last-first);
    }
    static void BoardChecks()
    {
        for(int kind=0;kind<4;kind++) using(var f=new Fixture(kind,44100,false))
        {
            Check(f.Fm.GetType().Name=="Ym2203FmRenderer", "not shared ymfm");
            for(int chip=0;chip<2;chip++)
            {
                f.Select(chip,true); f.Reg(0,0x35+chip); Check(f.Read()==0x35+chip,"SSG read");
                f.Reg(0x1E,0x7B); Check(f.Read()==0,"FM aliased to I/O A");
                f.Reg(0x30,0x71); Check(f.Read()==0,"write-only FM cache exposed");
                f.Select(chip,false); f.Reg(0,0x35); Check((f.Read()&0x80)!=0,"SSG data cycle missed BUSY");
                f.Frame(); Check((f.Read()&0x80)==0,"BUSY never cleared");
                f.Reg(0x24,0xFF);f.Reg(0x25,3);f.Reg(0x27,5);f.Frame();
                byte timer=f.Read();Check((timer&1)!=0,"Timer A");
                f.Select(chip^1,false);Check((f.Read()&1)==0,"timer leaked to other chip");f.Select(chip,false);
                f.Reg(0x27,0x10);Check((f.Read()&1)==0,"Timer A reset");f.Frame();
                f.Reg(0x26,0xFF);f.Reg(0x27,0x0A);f.Frame();Check((f.Read()&2)!=0,"Timer B");f.Reg(0x27,0x20);Check((f.Read()&2)==0,"Timer B reset");
                f.Select(chip,true);Tone(f.Reg,7,0,0,0,31);f.Frame();Check(f.Fm.AudioBuffer.Any(v=>v!=0),"silent chip");
                f.Machine.DebugReset();f.Machine.CPU.regs.PC=0x4000;f.Select(chip,false);f.Frame();Check((f.Read()&3)==0,"reset timers");
            }
            var owner=kind==0?typeof(TurboSoundFmPro):typeof(ZxMultiSoundCore);
            var psg=(Array)owner.GetField("m_psg",Private).GetValue(f.Board);
            f.Select(0,true);f.Port(0xFFFD,0x2F);
            Check((int)psg.GetValue(0).GetType().GetProperty("ChipFrequency").GetValue(psg.GetValue(0),null)==5250000,"prescale 2F");
            Check((int)psg.GetValue(1).GetType().GetProperty("ChipFrequency").GetValue(psg.GetValue(1),null)==1750000,"prescale isolation");
            f.Port(0xFFFD,0x2D);f.Port(0xFFFD,0x2E);
            Check((int)psg.GetValue(0).GetType().GetProperty("ChipFrequency").GetValue(psg.GetValue(0),null)==3500000,"prescale 2E");
            f.Port(0xFFFD,0x2D);
            Console.WriteLine("Board {0}: both chips / BUSY / timers A,B / SSG / FM read / reset / prescale PASS",kind);
        }
        for(int alg=0;alg<8;alg++)
        {
            string reference=null;
            for(int kind=0;kind<4;kind++) using(var f=new Fixture(kind,44100,false))
            {
                f.Select(0,true);Tone(f.Reg,alg,7,0,0,31);f.Frame();string hash=Hash(f.Frame());
                if(reference==null)reference=hash;Check(hash==reference,"board PCM disagreement alg " + alg);
            }
            Console.WriteLine("Algorithm {0}: all four boards identical PCM {1}",alg,reference);
        }
    }
    [DllImport("omni_opn.dll",CallingConvention=CallingConvention.Cdecl)]static extern IntPtr omni_opn_create();
    [DllImport("omni_opn.dll",CallingConvention=CallingConvention.Cdecl)]static extern void omni_opn_destroy(IntPtr p);
    [DllImport("omni_opn.dll",CallingConvention=CallingConvention.Cdecl)]static extern void omni_opn_address(IntPtr p,byte b);
    [DllImport("omni_opn.dll",CallingConvention=CallingConvention.Cdecl)]static extern void omni_opn_data(IntPtr p,byte b);
    [DllImport("omni_opn.dll",CallingConvention=CallingConvention.Cdecl)]static extern void omni_opn_generate(IntPtr p,[Out]short[] v,uint count,uint rate);
    static short[] NativeTone(int alg,int fb,int dt,int ks,int ar)
    {
        var p=omni_opn_create();try {Tone((r,v)=>{omni_opn_address(p,(byte)r);omni_opn_data(p,(byte)v);},alg,fb,dt,ks,ar);
            // Distinct operators avoid algebraically equivalent waveforms
            // for different topologies when all four operators are equal.
            foreach(int slot in new[]{0,4,8,12}){omni_opn_address(p,(byte)(0x30+slot));omni_opn_data(p,(byte)((dt<<4)| (1+slot/4)));}
            var data=new short[16000];omni_opn_generate(p,data,(uint)data.Length,44100);return data; }finally{omni_opn_destroy(p);}
    }
    static void CoreChecks()
    {
        for(int alg=0;alg<8;alg++)
        {
            var a=NativeTone(alg,0,0,0,31);var b=NativeTone(alg,7,0,0,31);int diff=a.Zip(b,(x,y)=>Math.Abs(x-y)).Max();
            Check(diff>0,"feedback ignored alg "+alg);Console.WriteLine("Feedback alg {0}: max difference {1}",alg,diff);
        }
        Check(!NativeTone(2,0,0,0,31).SequenceEqual(NativeTone(3,0,0,0,31)),"alg2/3 identical");
        Check(!NativeTone(7,0,0,0,31).SequenceEqual(NativeTone(7,0,3,0,31)),"DT ignored");
        Check(!NativeTone(7,0,0,0,12).SequenceEqual(NativeTone(7,0,0,3,12)),"KS ignored");
        Console.WriteLine("Native algorithms 2/3, feedback 0-7, DT and unsaturated AR12/KS PASS");
        foreach(int rate in new[]{44100,48000,96000}) using(var f=new Fixture(0,rate,false))
        {
            f.Select(0,true);Tone(f.Reg,7,0,0,0,31);var data=new List<short>();
            for(int i=0;i<70;i++)foreach(uint sample in f.Frame())data.Add((short)sample);
            double freq=Frequency(data,rate);Check(Math.Abs(freq-228.8)<2,"YM pitch at "+rate);
            Console.WriteLine("YM2203 {0}: {1:F3} Hz",rate,freq);
        }
    }
    static void MaxChecks()
    {
        double reference=0;
        foreach(int rate in new[]{44100,48000,96000}) using(var f=new Fixture(2,rate,false))
        {
            var opl=((IAdditionalSoundRenderers)f.Board).SoundRenderers.Last();opl.Volume=100;
            Action<int,int> write=(r,v)=>{f.Port(0x34C4,(byte)r);f.Port(0x34C5,(byte)v);};
            foreach(int r in new[]{0x20,0x23})write(r,1);write(0x40,0x10);write(0x43,0);write(0x60,0xF0);write(0x63,0xF0);write(0x80,0x77);write(0x83,0x77);write(0xC0,0x30);write(0xA0,0x98);write(0xB0,0x31);
            var data=new List<short>();for(int i=0;i<70;i++){f.Frame();foreach(uint sample in opl.AudioBuffer)data.Add((short)sample);}
            double freq=Frequency(data,rate);if(reference==0)reference=freq;Check(reference>100 && Math.Abs(freq-reference)<2,"Max pitch "+rate);
            Console.WriteLine("Max OPL3 {0}: {1:F3} Hz",rate,freq);
        }
    }
    static void Paths()
    {
        var folder=PortablePaths.DataDirectory("Temp");
        foreach(var device in new BusDeviceBase[]{new ZXMAK2.Hardware.Atm.IdeAtm(),new ZXMAK2.Hardware.Profi.IdeProfi(),new ZXMAK2.Hardware.Sprinter.IdeSprinter(),new ZXMAK2.Hardware.General.IdeSmuc()})
        {
            string external=Path.Combine(Path.GetDirectoryName(PortablePaths.Root),Guid.NewGuid().ToString("N")+".img");
            // Sibling fixture is deliberately external to this disposable runtime.
            File.WriteAllBytes(external,new byte[4096]);string descriptor=Path.Combine(folder,device.GetType().Name+".vmide");
            var type=device.GetType();type.GetField("m_ideFileName",Private).SetValue(device,descriptor);
            var ata=(AtaPort)type.GetField("m_ata",Private).GetValue(device);ata.Devices[0].DeviceInfo.ConfigureImage(external,false);ata.Open();
            var passer=(AtaPasser)typeof(AtaDevice).GetField("ata_p",Private).GetValue(ata.Devices[0]);
            Check(passer.Seek(0)&&passer.WriteSector(Enumerable.Repeat((byte)0x66,512).ToArray(),0),"IDE write");string saved=passer.ImagePath;
            device.BusDisconnect();Check(File.ReadAllBytes(external)[0]==0,"external IDE modified");
            Check(PortablePaths.IsInternal(saved)&&File.ReadAllBytes(saved)[0]==0x66,"IDE save missing");
            var reloaded=new AtaDeviceInfo();reloaded.Load(descriptor);Check(PortablePaths.Resolve(reloaded.FileName)==saved,"descriptor lost internal copy");
            using(File.Open(saved,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){} // Close released handle.
            device.BusConnect();Check(ata.Devices[0].loaded(),"IDE reload");Check(PortablePaths.Resolve(ata.Devices[0].DeviceInfo.FileName)==saved,"reload old external path");device.BusDisconnect();
            Console.WriteLine("{0}: external read-only / internal copy / relative descriptor / close / reload PASS",type.Name);
        }
        var cmos=new CmosPentEvo();cmos.BusConnect();cmos.BusDisconnect();Check(true,"CMOS unassigned sidecar");
        Console.WriteLine("CMOS without assigned sidecar PASS");
    }
    static void Benchmark()
    {
        for(int old=1;old>=0;old--)using(var f=new Fixture(0,44100,old!=0))
        {
            for(int chip=0;chip<2;chip++){f.Select(chip,true);for(int channel=0;channel<3;channel++){
                int ch=channel;Tone((r,v)=>f.Reg(r==0x28?r:r+ch,r==0x28?(v|ch):v),0,7,0,0,31);}}
            for(int i=0;i<50;i++)f.Frame();var times=new List<double>();
            for(int run=0;run<3;run++){var watch=Stopwatch.StartNew();for(int i=0;i<300;i++)f.Frame();watch.Stop();times.Add(watch.Elapsed.TotalMilliseconds);}
            Console.WriteLine("TSFM six active FM channels, 300 complete CPU/video/audio frames, median: {0} {1:F2} ms ({2})",old!=0?"legacy":"ymfm",times.OrderBy(t=>t).ElementAt(1),String.Join(",",times.Select(t=>t.ToString("F2"))));
        }
    }
    static int Main(string[] args)
    {
        try {if(args.Length>0 && args[0]=="paths")Paths();else if(args.Length>0 && args[0]=="music")Music(args[1]);else {BoardChecks();CoreChecks();MaxChecks();Benchmark();}
            Console.WriteLine("PASS: {0} assertions",checks);return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
