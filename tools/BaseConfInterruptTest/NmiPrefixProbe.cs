using System;
using System.Linq;
using System.Reflection;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Cpu;

// Bounded, headless regression. No ROM/media/config files are opened or written.
class NmiPrefixProbe
{
    static int checks, cases, failures;
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static void Run(string name, Action action)
    {
        cases++;
        try { action(); Console.WriteLine("PASS: " + name); }
        catch (Exception ex) { failures++; Console.WriteLine("FAIL: " + name + ": " + ex.Message); }
    }
    sealed class Rig
    {
        public CpuUnit Cpu = new CpuUnit();
        public byte[] Mem = new byte[65536];
        public int Acks, Irqs;
        public Rig(CpuType type, byte[] code)
        {
            Cpu.Type = type;
            Cpu.regs.PC = 0x8000; Cpu.regs.SP = 0xF000;
            Cpu.regs.AF = 0x2401; Cpu.regs.BC = 2;
            Cpu.regs.HL = 0x9000; Cpu.regs.DE = 0x9100;
            Cpu.regs.IX = 0x9200; Cpu.regs.IY = 0x9300;
            Cpu.regs.IR = 0x8080; Cpu.IFF1 = Cpu.IFF2 = true; Cpu.IM = 1;
            Mem[0x9000] = 0xA5; Mem[0x9001] = 0x5A;
            Mem[0x9202] = 0x81; Mem[0x92FE] = 0x42;
            Array.Copy(code, 0, Mem, 0x8000, code.Length);
            // The handler MUST be fetched as ordinary opcodes, never as the
            // interrupted ED/CB/index continuation. INC A; RETN.
            Mem[0x66] = 0x3C; Mem[0x67] = 0xED; Mem[0x68] = 0x45;
            Cpu.RDMEM_M1 = Cpu.RDMEM = a => Mem[a];
            Cpu.WRMEM = (a, v) => Mem[a] = v;
            Cpu.RDPORT = a => 0x5A; Cpu.WRPORT = (a, v) => {};
            Cpu.RDNOMREQ = Cpu.WRNOMREQ = a => {};
            Cpu.SCANSIG = delegate {};
            Cpu.RESET = delegate { Cpu.NMI = false; };
            Cpu.NMIACK_M1 = delegate { Acks++; Cpu.NMI = false; };
            Cpu.INTACK_M1 = delegate { Irqs++; Cpu.INT = false; };
        }
        public void Step() { Cpu.ExecCycle(); }
        public bool Boundary { get { return Cpu.FX == CpuModeIndex.None && Cpu.XFX == CpuModeEx.None; } }
        public ushort StackPc { get { return (ushort)(Mem[Cpu.regs.SP] | Mem[(ushort)(Cpu.regs.SP + 1)] << 8); } }
    }
    static readonly byte[][] Instructions = {
        new byte[]{0xED,0x44},                  // NEG
        new byte[]{0xCB,0x00},                  // RLC B
        new byte[]{0xDD,0x21,0x34,0x12},        // LD IX,nn
        new byte[]{0xFD,0x21,0x78,0x56},        // LD IY,nn
        new byte[]{0xDD,0xCB,0x02,0x06},        // RLC (IX+2)
        new byte[]{0xFD,0xCB,0xFE,0x46},        // BIT 0,(IY-2)
        new byte[]{0xDD,0xDD,0xFD,0x21,0x78,0x56},
        new byte[]{0xFD,0xFD,0xDD,0xCB,0x02,0x06},
        new byte[]{0xDD,0xFD,0xED,0x44},        // ignored index prefixes
        new byte[]{0xED,0xED},                  // undefined ED completes, not another prefix
        new byte[]{0xCB,0xCB},                  // SET 1,E, not another prefix
        new byte[]{0xDD,0x00},                  // ignored index before NOP
        new byte[]{0xDD,0x76},                  // prefixed HALT
        new byte[]{0xFD,0xFB},                  // prefixed EI (BINT remains true)
        new byte[]{0xED,0xB0},                  // LDIR: interruptible between iterations
        new byte[]{0x3E,0x42},                  // non-prefixed multi-byte instruction
        new byte[]{0xDD,0xFD,0xDD,0xFD,0xDD,0xFD,0xDD,0xFD,0x00}
    };
    static void EqualState(Rig a, Rig b)
    {
        foreach (FieldInfo f in typeof(CpuRegs).GetFields(BindingFlags.Public | BindingFlags.Instance))
            Check(Object.Equals(f.GetValue(a.Cpu.regs), f.GetValue(b.Cpu.regs)), "register " + f.Name);
        Check(a.Mem.SequenceEqual(b.Mem), "instruction memory effects differ");
        Check(a.Cpu.Tact == b.Cpu.Tact && a.Cpu.RzxCounter == b.Cpu.RzxCounter, "instruction timing/R differs");
        Check(a.Cpu.BINT == b.Cpu.BINT && a.Cpu.IFF1 == b.Cpu.IFF1 &&
            a.Cpu.IFF2 == b.Cpu.IFF2 && a.Cpu.HALTED == b.Cpu.HALTED, "instruction flags differ");
    }
    static void Prefix(CpuType type, byte[] code, int inject, bool pulse)
    {
        var expected = new Rig(type, code); var test = new Rig(type, code);
        int steps = 0;
        do { expected.Step(); steps++; } while (!expected.Boundary && steps < 40);
        for (int i = 0; i < steps; i++)
        {
            test.Cpu.NMI = i == inject || (!pulse && i > inject);
            test.Step();
            Check(test.Acks == 0, String.Format("early NMI PC={0:X4} FX={1} XFX={2} stacked={3:X4}",
                test.Cpu.regs.PC, test.Cpu.FX, test.Cpu.XFX, test.StackPc));
        }
        Check(test.Boundary, "prefix not completed"); EqualState(expected, test);
        ushort resume = (ushort)(expected.Cpu.regs.PC + (expected.Cpu.HALTED ? 1 : 0));
        long tact = test.Cpu.Tact; byte refresh = test.Cpu.regs.R;
        test.Cpu.NMI = !pulse; test.Step();
        Check(test.Acks == 1 && test.Cpu.regs.PC == 0x66, "deferred one-cycle NMI was lost");
        Check(test.StackPc == resume && test.Cpu.regs.SP == 0xEFFE, "wrong return PC/SP");
        Check(test.Cpu.Tact == tact + 11 && test.Cpu.regs.R == ((refresh & 128) | ((refresh + 1) & 127)), "NMI not 11T/one refresh");
        Check(!test.Cpu.IFF1 && test.Cpu.IFF2 == expected.Cpu.IFF1 && !test.Cpu.HALTED, "NMI flags");
        byte a = test.Cpu.regs.A; test.Step();
        Check(test.Cpu.regs.PC == 0x67 && test.Cpu.regs.A == (byte)(a + 1), "handler inherits prefix");
        test.Step(); test.Step();
        Check(test.Cpu.regs.PC == resume && test.Cpu.regs.SP == 0xF000 && test.Acks == 1, "RETN/duplicate NMI");
        Check(test.Cpu.IFF1 == expected.Cpu.IFF1, "RETN IFF restoration");
        Console.WriteLine("TRACE: " + BitConverter.ToString(code) + " inject=" + inject + " pulse=" + pulse +
            " instructionT=" + tact + " resume=" + resume.ToString("X4") + " NMI=0066,+11T,ack=1");
    }
    static void Signals(CpuType type)
    {
        var plain = new Rig(type, new byte[]{0}); plain.Cpu.NMI = true; plain.Cpu.INT = true; plain.Step();
        Check(plain.Acks == 1 && plain.Irqs == 0 && plain.StackPc == 0x8000, "ordinary NMI priority");
        var halt = new Rig(type, new byte[]{0x76}); halt.Step(); halt.Cpu.NMI = true; halt.Step();
        Check(!halt.Cpu.HALTED && halt.StackPc == 0x8001 && halt.Cpu.Tact == 15, "HALT exit");
        var ei = new Rig(type, new byte[]{0xFB,0}); ei.Step(); ei.Cpu.NMI = true; ei.Step();
        Check(ei.Acks == 1 && ei.StackPc == 0x8001, "EI must not mask NMI with BINT");
        var irq = new Rig(type, new byte[]{0xFB,0}); irq.Step(); irq.Cpu.INT = true; irq.Step();
        Check(irq.Irqs == 0 && irq.Cpu.regs.PC == 0x8002, "EI must defer INT"); irq.Step();
        Check(irq.Irqs == 1 && irq.Cpu.regs.PC == 0x38, "INT after EI delay");
        var reset = new Rig(type, new byte[]{0xDD,0xFD,0x21,0x34,0x12});
        reset.Step(); reset.Cpu.NMI = true; reset.Step(); reset.Cpu.NMI = false;
        Check(reset.Acks == 0, "NMI before reset was not deferred");
        reset.Cpu.RST = true; reset.Cpu.INT = true; reset.Step(); reset.Cpu.RST = false;
        Check(reset.Cpu.regs.PC == 0 && reset.Boundary && reset.Acks == 0 && reset.Irqs == 0, "RESET priority/prefix clear");
        reset.Step(); Check(reset.Acks == 0 && reset.Cpu.regs.PC == 1, "RESET leaked pending NMI");
        reset.Cpu.NMI = true; reset.Step(); Check(reset.Acks == 1 && reset.StackPc == 1, "fresh NMI after RESET");
        var operand = new Rig(type, new byte[]{0x3E,0x42});
        operand.Cpu.RDMEM = a => { operand.Cpu.NMI = true; return operand.Mem[a]; };
        operand.Step(); Check(operand.Acks == 0 && operand.Cpu.regs.A == 0x42, "NMI interrupted operand read");
        operand.Step(); Check(operand.Acks == 1 && operand.StackPc == 0x8002, "NMI after full instruction");
    }
    static void BusPulse()
    {
        var r = new Rig(CpuType.NEC_NMOS, new byte[]{0xDD,0xFD,0x21,0x78,0x56});
        var events = new EventManager(r.Cpu, null);
        events.SubscribeRdMemM1(0, 0, delegate(ushort a, ref byte v) { v = r.Mem[a]; });
        events.SubscribeRdMem(0, 0, delegate(ushort a, ref byte v) { v = r.Mem[a]; });
        events.SubscribeWrMem(0, 0, (a, v) => r.Mem[a] = v);
        events.SubscribeNmiAck(delegate { r.Acks++; });
        events.PreCycle(); r.Step(); events.RequestNmi(1000);
        events.PreCycle(); Check(r.Cpu.NMI, "host request not delivered"); r.Step();
        Check(r.Acks == 0, "host NMI accepted inside prefix");
        events.PreCycle(); Check(!r.Cpu.NMI, "expected one-cycle host request"); r.Step();
        events.PreCycle(); r.Step();
        Check(r.Acks == 1 && r.StackPc == 0x8005 && r.Cpu.regs.IY == 0x5678, "host pulse lost or wrong PC");
        events.PreCycle(); r.Step(); Check(r.Acks == 1 && r.Cpu.regs.PC == 0x67, "host NMI duplicated");
    }
    static int Main()
    {
        foreach (CpuType type in Enum.GetValues(typeof(CpuType)))
        {
            foreach (var code in Instructions)
            {
                var counter = new Rig(type, code); int steps = 0;
                do { counter.Step(); steps++; } while (!counter.Boundary && steps < 40);
                for (int at = 1; at < steps; at++) foreach (bool pulse in new[]{false,true})
                {
                    int position = at;
                    Run(type + "/" + BitConverter.ToString(code) + "/" + at + "/pulse=" + pulse,
                        () => Prefix(type, code, position, pulse));
                }
            }
            Run(type + "/signals", () => Signals(type));
        }
        Run("EventManager one-cycle request", BusPulse);
        Console.WriteLine("SUMMARY: cases=" + cases + " assertions=" + checks + " failures=" + failures);
        return failures == 0 ? 0 : 1;
    }
}
