using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Hardware.Circuits.Sound;
using ZXMAK2.Hardware.General;
using ZXMAK2.Hardware.Evo;
using ZXMAK2.Hardware.Spectrum;

// Actual CPU.WRPORT decode -> board PSG -> audio buffer, disposable sandbox.
static class PsgNoiseRoutesProbe {
    static readonly BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
    [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern bool SetDllDirectory(string path);
    static object Field(object obj,string name) {
        for(Type t=obj.GetType();t!=null;t=t.BaseType) {
            var f=t.GetField(name,F);if(f!=null)return f.GetValue(obj);
        }
        throw new Exception(name);
    }
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
    static string Run(int kind,int chip,int period) {
        using(var m=new Spectrum()) {
            m.BusManager.Init(m,true);m.BusManager.Disconnect();m.BusManager.Clear();
            var mem=new MemorySpectrum48();
            BusDeviceBase board;
            switch(kind) {
                case 0:board=new AY8910();break; case 1:board=new AYCHRV();break;
                case 2:board=new TurboSoundFmPro();break;case 3:board=new ZxMultiSoundDevice();break;
                case 4:board=new ZxMultiSoundMaxDevice();break;case 5:board=new ZxOmniSoundDevice();break;
                default:board=new ZxmSoundCardExtreme();break;
            }
            var multi=board as ZxMultiSoundCore;
            if(multi!=null){multi.AutomaticConfiguration=false;multi.GeneralSoundEnabled=false;}
            m.BusManager.Add(mem);m.BusManager.Add(new UlaSpectrum48());m.BusManager.Add(board);
            Check(m.BusManager.Connect(),"connect "+kind);
            m.DebugReset();mem.WRMEM_DBG(0x4000,0x18);mem.WRMEM_DBG(0x4001,0xFE);
            m.CPU.regs.PC=0x4000;m.CPU.RDMEM(0x4000);
            object renderer=kind<2?(object)board:((Array)Field(board,"m_psg")).GetValue(chip);
            var p=(PsgChip)Field(renderer,"m_chip");
            if(kind>=2)m.CPU.WRPORT(0xFFFD,(byte)(0xFE|chip));
            Action<int,int> write=(r,v)=>{m.CPU.WRPORT(0xFFFD,(byte)r);m.CPU.WRPORT(0xBFFD,(byte)v);};
            write(6,period);write(7,7);write(8,15);write(9,11);write(10,6);
            Check(p.GetReg(6)==(period&31),"port -> R6 readback");
            Check((int)Field(p,"m_divN")==Math.Max(1,period&31)*2,"port -> normalized divider");
            var pcm=new MemoryStream();var w=new BinaryWriter(pcm);
            for(int i=0;i<12;i++) {
                m.ExecuteFrame();foreach(uint s in ((ISoundRenderer)renderer).AudioBuffer)w.Write(s);
                w.Write((int)Field(p,"m_seedN"));w.Write((int)Field(p,"m_cntN"));
            }
            Check(((ISoundRenderer)renderer).AudioBuffer.Distinct().Count()>2,"audible varying noise");
            string result;using(var sha=SHA256.Create())result=BitConverter.ToString(sha.ComputeHash(pcm.ToArray())).Replace("-","");
            m.BusManager.Disconnect();return result;
        }
    }
    public static void Main() {
        SetDllDirectory(Path.GetFullPath("Lib/Native"));
        string[] names={"AY8910","AYCHRV internal Evo","TurboSound FM Pro","MultiSound","MultiSound Max","OmniSound","Extreme Rev03"};
        for(int kind=0;kind<7;kind++)for(int chip=0;chip<(kind<2?1:2);chip++) {
            string a=Run(kind,chip,0),b=Run(kind,chip,1),c=Run(kind,chip,2),d=Run(kind,chip,32);
            Check(a==b && a==d && a!=c,"route PCM/sequence equivalence "+names[kind]);
            Console.WriteLine("PASS {0} PSG {1}: R6=0 == 1 == 0x20, != 2; PCM+sequence {2}",names[kind],chip,a);
        }
    }
}
