// Entry shim for the existing portable regression suite: exercise the same
// bootstrap/native lookup as the distributed EXE from a foreign working dir.
using System;
using System.Reflection;
using System.Linq;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Host.Entities;
static class PortableV98Entry
{
    [STAThread] static int Main(string[] args)
    {
        PortableBootstrap.Initialize();
        if(args.Length==1&&args[0]=="models-long")return ModelsLong();
        return (int)typeof(PortableProbe).GetMethod("Main",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{args});
    }
    static int ModelsLong()
    {
        var templates=new MachinesConfig();templates.Load();
        using(var machine=new Spectrum()){
            machine.Init();
            foreach(string name in new[]{"ZX Spectrum 48","ZX Spectrum 128","PENTAGON 128K"}){
                machine.BusManager.LoadConfigXml(templates.GetConfig(name));machine.DebugReset();
                for(int i=0;i<200;i++)machine.ExecuteFrame();
                ushort bootPc=machine.CPU.regs.PC;
                byte[] code={0x3E,0,0xEE,0x10,0xD3,0xFE,0x06,0,0x10,0xFE,0x18,0xF6};
                var memory=machine.BusManager.FindDevice<IMemoryDevice>();
                for(int i=0;i<code.Length;i++)memory.WRMEM_DBG((ushort)(0x8000+i),code[i]);
                machine.CPU.regs.PC=0x8000;machine.CPU.IFF1=machine.CPU.IFF2=false;
                int low=0,high=0;
                for(int i=0;i<20;i++){
                    machine.ExecuteFrame();var sound=machine.BusManager.SoundFrame.GetBuffer();
                    if(sound.Length!=882)throw new Exception("Frame size "+name);
                    low=Math.Min(low,sound.Min(x=>(int)(short)x));high=Math.Max(high,sound.Max(x=>(int)(short)x));
                }
                if(high-low<100)throw new Exception("Beeper output silent "+name);
                Console.WriteLine("PASS {0}: 200 ROM frames PC={1:X4}; 20 guest beeper frames swing={2}; sound frame882",name,bootPc,high-low);
            }
            machine.BusManager.Disconnect();
        }
        return 0;
    }
}
