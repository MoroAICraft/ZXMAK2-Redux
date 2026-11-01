using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Security.Cryptography;
using ZXMAK2.Hardware.Circuits.Sound;

// Compile against the product DLL, run in the private portable runtime.
// No register interception, zero->one injection, or reflection writes.
public static class PsgNoiseZeroProbe
{
    static int assertions;
    static readonly BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
    static int Field(PsgChip p, string n) { return (int)typeof(PsgChip).GetField(n, flags).GetValue(p); }
    static double Step(PsgChip p) { return (double)typeof(PsgChip).GetField("m_renderStep", flags).GetValue(p); }
    static void Check(bool ok, string message) { assertions++; if (!ok) throw new Exception(message); }
    static PsgChip Chip(AmpType type, int hz) {
        var p = new PsgChip(); p.AmpType = type; p.ChipFrequency = hz; p.PanType = PanType.Abc;
        p.SetReg(7, 7); p.SetReg(8, 15); p.SetReg(9, 11); p.SetReg(10, 6);
        return p;
    }
    static string Digest(byte[] bytes) { using (var h = SHA256.Create()) return BitConverter.ToString(h.ComputeHash(bytes)).Replace("-", ""); }
    static byte[] Stream(AmpType type, int hz, int period, bool timed, bool mixed) {
        var p = Chip(type, hz);
        if (timed) p.SetReg(-1, -1, 6, (byte)period); else p.SetReg(6, (byte)period);
        Check(p.GetReg(6) == (period & 31), "masked R6 readback");
        var data = new MemoryStream(); var w = new BinaryWriter(data);
        p.UpdateHandler = (t,l,r) => { w.Write(t); w.Write(l); w.Write(r); w.Write(Field(p,"m_seedN")); w.Write(Field(p,"m_cntN")); };
        double time = 0, step = Step(p);
        for (int block=0; block<32; block++) {
            if (mixed) {
                // Also exercise tone, envelope, pan and volume without any prior zero-noise rendering.
                p.SetReg(0, (byte)(block*7)); p.SetReg(2, (byte)(block*3)); p.SetReg(4, (byte)(block*11));
                p.SetReg(7, (byte)(block & 63)); p.SetReg(8, (byte)(block&31));
                p.SetReg(11, (byte)(block*13)); p.SetReg(13, (byte)(block&15));
            }
            time = p.Update(time, time + step*127.5);
        }
        return data.ToArray();
    }
    sealed class Oracle {
        public PsgChip P; public int Seed, Count, Div, Ticks; public double Time, S;
        public Oracle(AmpType type, int initial) {
            P=Chip(type,1750000); P.SetReg(6,(byte)initial); Div=Math.Max(1,initial)*2; S=Step(P);
            P.UpdateHandler=(t,l,r)=> {
                if (++Count>=Div) {Count=0; Seed=unchecked((Seed*2+1)^(((Seed>>16)^(Seed>>13))&1));}
                Ticks++;
                Check(Field(P,"m_seedN")==Seed && Field(P,"m_cntN")==Count,"independent divider/seed oracle");
            };
        }
        public void Advance(int n) { if(n>0) Time=P.Update(Time,Time+S*(n-.5)); }
        public void Write(int value, double fraction) {
            int before=Ticks; double old=Time;
            bool same=P.GetReg(6)==(value&31);
            Time=P.SetReg(Time,Time+S*fraction,6,(byte)value);
            if(same) Check(Time==old && before==Ticks,"unchanged timed write must not render");
            Div=Math.Max(1,value&31)*2;
            Check(Field(P,"m_divN")==Div,"normalized internal divider");
            Check(Field(P,"m_seedN")==Seed && Field(P,"m_cntN")==Count,"write preserves phase/seed");
            Check(P.GetReg(6)==(value&31),"write preserves stored zero/mask");
        }
    }
    public static void Main(string[] args) {
        var types=new[]{AmpType.Ay8910,AmpType.Ym2149F,AmpType.Ym2203};
        if(args[0]=="golden") {
            using(var w=new StreamWriter(args[1]))
                foreach(var type in types) foreach(int hz in new[]{1750000,1773400}) for(int p=1;p<=31;p++)
                    w.WriteLine("{0},{1},{2},{3}",type,hz,p,Digest(Stream(type,hz,p,false,true)));
            Console.WriteLine("PASS nonzero golden: 186 model/clock/period streams, 4096 ticks each"); return;
        }
        foreach(var type in types) {
            foreach(int hz in new[]{1750000,1773400}) foreach(bool timed in new[]{false,true}) {
                string zero=Digest(Stream(type,hz,0,timed,false));
                Check(zero==Digest(Stream(type,hz,1,timed,false)),"0 equals 1 sequence and PCM");
                Check(zero==Digest(Stream(type,hz,32,timed,false)),"0x20 equals 0");
                Check(zero==Digest(Stream(type,hz,33,timed,false)),"0x21 equals 1");
                Check(zero!=Digest(Stream(type,hz,2,timed,false)),"noise 2 remains different");
            }
            var fresh=Chip(type,1750000); // no extra R6 write after constructor Reset
            Check(fresh.GetReg(6)==0 && Field(fresh,"m_divN")==2,"constructor initializes zero divider");
            int shifts=0,seed=0,ticks=0;
            fresh.UpdateHandler=(t,l,r)=>{int s=Field(fresh,"m_seedN");if(s!=seed)shifts++;seed=s;ticks++;};
            fresh.Update(0,Step(fresh)*63.5);
            Check(ticks==64 && shifts==32,"constructor 32 shifts/64 ticks");
            foreach(int a in new[]{0,1,31}) foreach(int b in new[]{0,1,31}) for(int phase=0;phase<66;phase++) {
                var o=new Oracle(type,a); o.Advance(phase);
                // Render up to an off-grid write boundary using the OLD period.
                o.Write(b,1.25); o.Advance(129);
                int s=o.Seed,c=o.Count;
                o.P.SetReg(6,(byte)b); // immediate repeated write uses both SetReg overloads
                Check(Field(o.P,"m_seedN")==s && Field(o.P,"m_cntN")==c,"immediate write retains phase");
                o.Advance(5);
                o.P.Reset(); o.Div=2;
                Check(o.P.GetReg(6)==0 && Field(o.P,"m_divN")==2,"Reset zero divider/readback");
                Check(Field(o.P,"m_seedN")==o.Seed && Field(o.P,"m_cntN")==o.Count,"Reset retains existing phase semantics");
                o.Advance(67);
            }
        }
        Console.WriteLine("PASS corrected PSG: {0} assertions; 1782 transition/phase/reset cases; all 3 amp models",assertions);
    }
}
