using System;
using ZXMAK2.Engine.Cpu;

// Test-only partial for a PRIVATE COPY of the archived BC v0.63 harness.
// No modifications to Commander binaries or the user's working BC checkout.
partial class CommanderMediaProbe
{
    static byte nmiV94ExpectedEff7 = 0x84;
    static void PrimeNmiV94()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "--nmi-v94-prefix");
        if (index < 0) return;
        byte[] code;
        switch (args[index + 1])
        {
            case "ED": code = new byte[]{0xED,0}; break; // undefined ED NOP
            case "DD": code = new byte[]{0xDD,0}; break;
            case "FD": code = new byte[]{0xFD,0}; break;
            case "CB": code = new byte[]{0xCB,0xC1}; break; // SET 0,C; C is already odd
            case "DDCB": code = new byte[]{0xDD,0xCB,0,0xC6}; Write(cpu.regs.IX,(byte)(Read(cpu.regs.IX)|1)); break;
            case "FDCB": code = new byte[]{0xFD,0xCB,0,0xC6}; Write(cpu.regs.IY,(byte)(Read(cpu.regs.IY)|1)); break;
            default: throw new Exception("Unknown NMI fixture prefix");
        }
        int rate = Int32.Parse(args[Array.IndexOf(args,"--nmi-v94-rate")+1]);
        Check(rate == 1 || rate == 2 || rate == 4, "Invalid test clock");
        nmiV94ExpectedEff7 = (byte)(rate == 1 ? 0x94 : 0x84);
        Set(mt,"m_pEFF7",nmiV94ExpectedEff7); Set(mt,"m_pFF77",rate == 4 ? 11 : 3);
        timing.LatchClockAtRefresh();
        Check(timing.CpuClockMultiplier == rate, "Requested clock not applied");
        cpu.regs.PC = (ushort)(0xA100-code.Length); cpu.regs.R = 0xA1;
        cpu.FX = CpuModeIndex.None; cpu.XFX = CpuModeEx.None;
        cpu.HALTED = false; cpu.BINT = false;
        for(int i=0;i<code.Length;i++)Write((ushort)(cpu.regs.PC+i),code[i]);
        Step(); // Real prefix fetch, not a fabricated FX/XFX value.
        Check(cpu.FX != CpuModeIndex.None || cpu.XFX != CpuModeEx.None, "Prefix not reached");
        Console.WriteLine("INJECT NMI: PC="+cpu.regs.PC.ToString("X4")+" FX="+cpu.FX+" XFX="+cpu.XFX+" MHz="+(rate*3.5));
        Action ack = cpu.NMIACK_M1;
        bool firstAck = true;
        cpu.NMIACK_M1 = delegate {
            Console.WriteLine("ACK NMI: PC="+cpu.regs.PC.ToString("X4")+" FX="+cpu.FX+" XFX="+cpu.XFX);
            if(firstAck) {
                Check(cpu.regs.PC == 0xA100 && cpu.FX == CpuModeIndex.None && cpu.XFX == CpuModeEx.None,
                    "NMI accepted before entire instruction finished");
                firstAck = false;
            }
            ack();
        };
    }
}
