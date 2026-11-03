// Bounded synthetic diagnostics. Run only in a private portable runtime.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Xml;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Hardware;
using ZXMAK2.Hardware.Evo;
using ZXMAK2.Hardware.Spectrum;

static class CardLevelProbe
{
    static bool ExpectFix;
    static bool SkipV97Hash;
    static bool ExpectMaxFix;
    static bool ExpectHeldFix;
    static int Checks;
    static void Check(bool ok,string name){Checks++;if(!ok)throw new Exception(name);}
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static object Field(object o,string name){for(Type t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(name,Flags|BindingFlags.DeclaredOnly);if(f!=null)return f.GetValue(o);}throw new Exception(name);}
    static void FieldSet(object o,string name,object value){for(Type t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(name,Flags|BindingFlags.DeclaredOnly);if(f!=null){f.SetValue(o,value);return;}}throw new Exception(name);}
    static object Call(object o,string name,params object[] args){return o.GetType().GetMethod(name,Flags).Invoke(o,args);}
    static void Set(object o,string name,object v){o.GetType().GetProperty(name,Flags).SetValue(o,v,null);}
    static SoundDeviceBase Renderer(string name){return (SoundDeviceBase)Activator.CreateInstance(typeof(TurboSoundFmPro).Assembly.GetType("ZXMAK2.Hardware.Evo."+name),true);}
    static double Rms(IEnumerable<short> data){var a=data.Select(x=>(double)x).ToArray();double mean=a.Average();return Math.Sqrt(a.Select(x=>(x-mean)*(x-mean)).Average());}
    static string Hash(uint[] a){byte[] b=new byte[a.Length*4];Buffer.BlockCopy(a,0,b,0,b.Length);using(var s=SHA256.Create())return BitConverter.ToString(s.ComputeHash(b)).Replace("-","");}
    sealed class Fixture:IDisposable
    {
        public readonly Spectrum Machine=new Spectrum();
        public readonly BusDeviceBase Board;
        public Fixture(BusDeviceBase board,bool evo=false,int rate=44100,BusDeviceBase second=null)
        {
            Board=board;var bus=Machine.BusManager;bus.Init(Machine,true);bus.Disconnect();bus.Clear();bus.SampleRate=rate;
            bus.Add(evo?(BusDeviceBase)new MemoryPentEvo():new MemorySpectrum48());bus.Add(evo?(BusDeviceBase)new UlaPentEvo():new UlaSpectrum48());bus.Add(board);
            if(second!=null)bus.Add(second);
            if(!bus.Connect())throw new Exception("connect "+board.Name);
            Machine.DebugReset();var mem=bus.FindDevice<IMemoryDevice>();
            Machine.CPU.WRMEM(0x4000,0x18);Machine.CPU.WRMEM(0x4001,0xFE);Machine.CPU.regs.PC=0x4000;
        }
        public void Reg(int r,int v){Machine.CPU.WRPORT((ushort)(0xC4+(r>>8)*2),(byte)r);Machine.CPU.WRPORT((ushort)(0xC5+(r>>8)*2),(byte)v);}
        public void Frame(){Machine.ExecuteFrame();}
        public void Dispose(){Machine.BusManager.Disconnect();Machine.Dispose();}
    }
    static void OplTone(Action<int,int> reg)
    {reg(0x105,1);foreach(int slot in new[]{0,3}){reg(0x20+slot,0x21);reg(0x40+slot,slot==0?63:0);reg(0x60+slot,0xF0);reg(0x80+slot,0x0F);}reg(0xC0,0x30);reg(0xA0,0x44);reg(0xB0,0x32);}
    static void Omni()
    {
        uint[] reference=null;
        foreach(int volume in new[]{100,50,0})
        {
            var b=new ZxOmniSoundDevice();b.GeneralSoundEnabled=false;b.Volume=volume;
            Console.WriteLine("Omni properties requested={0} own={1} moon={2} neogs={3} children={4}",volume,b.Volume,b.MoonSound.Volume,b.NeoGs.Volume,String.Join(",",b.SoundRenderers.Select(x=>x.Volume.ToString())));
            using(var f=new Fixture(b))
            {
                OplTone(f.Reg);for(int i=0;i<5;i++)f.Frame();var a=new List<uint>();for(int i=0;i<20;i++){f.Frame();a.AddRange(b.MoonSound.AudioBuffer);}
                Console.WriteLine("Omni OPL PCM volume={0} rms={1:F6} hash={2}",volume,Rms(a.Select(x=>(short)x)),Hash(a.ToArray()));
                if(volume==100){reference=a.ToArray();if(!SkipV97Hash)Check(Hash(reference)=="761E8DB71C8643BB851CF6F0C9442C993AF4B3A46E849B81C1DA6EEA2A2B593D","100% accepted v97 PCM changed");}
                else if(ExpectFix)for(int j=0;j<a.Count;j++){
                    Check((short)a[j]==(short)reference[j]*volume/100,"Moon volume left scaling");
                    Check((short)(a[j]>>16)==(short)(reference[j]>>16)*volume/100,"Moon volume right scaling");
                }
            }
        }
        uint[] unmuted=null;
        foreach(bool mute in new[]{false,true})using(var f=new Fixture(new ZxOmniSoundDevice()))
        {
            var b=(ZxOmniSoundDevice)f.Board;OplTone(f.Reg);for(int i=0;i<5;i++)f.Frame();
            ((ISoundRenderer)b).Volume=mute?0:100;for(int i=0;i<10;i++)f.Frame();
            ((SoundDeviceBase)b).Volume=100;for(int i=0;i<2;i++)f.Frame();
            var a=(uint[])b.MoonSound.AudioBuffer.Clone();if(unmuted==null)unmuted=a;
            else Check(Hash(a)==Hash(unmuted),"Moon mute froze native state");
        }
        Console.WriteLine("Omni mute preserves chip progression PASS");
    }
    static void VolumeContracts()
    {
        foreach(ZxMultiSoundCore b in new ZxMultiSoundCore[]{new ZxMultiSoundDevice(),new ZxMultiSoundMaxDevice(),new ZxOmniSoundDevice()})
        {
            foreach(int v in new[]{50,0,100,140,-20}){
                int expected=Math.Max(0,Math.Min(100,v));((ISoundRenderer)b).Volume=v;
                Console.WriteLine("Volume interface {0} request={1} concrete={2} base={3} children={4}",b.Name,v,b.Volume,((SoundDeviceBase)b).Volume,String.Join(",",b.SoundRenderers.Select(x=>x.Volume.ToString())));
                if(ExpectFix){Check(b.Volume==expected,"concrete/interface mismatch");Check(b.SoundRenderers.All(x=>x.Volume==expected),"interface propagation");}
            }
            var doc=new XmlDocument();doc.LoadXml("<Device volume='37'/>");b.LoadConfigXml(doc.DocumentElement);
            if(ExpectFix)Check(b.SoundRenderers.All(x=>x.Volume==37),"XML load children");
            var saved=new XmlDocument();saved.LoadXml("<Device/>");b.SaveConfigXml(saved.DocumentElement);
            Check(saved.DocumentElement.GetAttribute("volume")=="37","XML save wrong volume");
            b.Volume=23;if(ExpectFix)Check(b.SoundRenderers.All(x=>x.Volume==23),"concrete propagation");
            using(var f=new Fixture(b)){
                if(ExpectFix)Check(b.SoundRenderers.All(x=>x.Volume==23),"BusInit propagation");
                ((SoundDeviceBase)b).Volume=61;if(ExpectFix)Check(b.SoundRenderers.All(x=>x.Volume==61),"live base propagation");
                f.Frame();b.Volume=0;f.Frame();
                if(ExpectFix)Check(b.SoundRenderers.All(x=>x.Volume==0),"live mute children");
            }
        }
        var extreme=new ZxmSoundCardExtreme();((ISoundRenderer)extreme).Volume=37;Check(extreme.SoundRenderers.All(x=>x.Volume==37),"Extreme unchanged propagation");
        var ts=new TurboSoundFmPro();ts.Volume=37;Check(ts.SoundRenderers.All(x=>x.Volume==37),"TSFM unchanged propagation");
    }
    static void OpnTone(object r,int chips,int channels,int tl)
    {
        Set(r,"Enabled",true);
        for(int chip=0;chip<chips;chip++)for(int ch=0;ch<channels;ch++)
        {
            foreach(int slot in new[]{0,4,8,12}){
                Call(r,"SetRegister",chip,0x30+slot+ch,(byte)1);Call(r,"SetRegister",chip,0x40+slot+ch,(byte)tl);
                Call(r,"SetRegister",chip,0x50+slot+ch,(byte)31);Call(r,"SetRegister",chip,0x60+slot+ch,(byte)0);Call(r,"SetRegister",chip,0x70+slot+ch,(byte)0);Call(r,"SetRegister",chip,0x80+slot+ch,(byte)15);
            }
            Call(r,"SetRegister",chip,0xA4+ch,(byte)0x22);Call(r,"SetRegister",chip,0xA0+ch,(byte)0x69);Call(r,"SetRegister",chip,0xB0+ch,(byte)7);Call(r,"SetRegister",chip,0x28,(byte)(0xF0|ch));
        }
    }
    static void Opn()
    {
        foreach(int chips in new[]{1,2})foreach(int channels in new[]{1,3})foreach(int tl in new[]{0,16,40})
        using(var f=new Fixture(Renderer("Ym2203FmRenderer")))
        {
            var r=(SoundDeviceBase)f.Board;OpnTone(r,chips,channels,tl);for(int i=0;i<5;i++)f.Frame();
            var pcm=new List<uint>();long above=0,total=0;int nativePeak=0,sumPeak=0;
            for(int i=0;i<10;i++){f.Frame();pcm.AddRange(r.AudioBuffer);var arrays=(short[][])Field(r,"m_samples");for(int j=0;j<r.AudioBuffer.Length;j++){int sum=arrays[0][j]+arrays[1][j];sumPeak=Math.Max(sumPeak,Math.Abs(sum));nativePeak=Math.Max(nativePeak,Math.Abs((int)arrays[0][j]));if(sum>32767||sum< -32768)above++;total++;}}
            Console.WriteLine("OPN chips={0} channels={1} TL={2} chipPeak={3} sumPeak={4} clampBeforeMixer={5}/{6} PCM rms={7:F3} peak={8}",chips,channels,tl,nativePeak,sumPeak,above,total,Rms(pcm.Select(x=>(short)x)),pcm.Max(x=>Math.Abs((int)(short)x)));
        }
    }
    static void Saa()
    {
        foreach(int channels in new[]{1,3,6})foreach(int volume in new[]{100,50})
        using(var f=new Fixture((SoundDeviceBase)Activator.CreateInstance(typeof(TurboSoundFmPro).Assembly.GetType("ZXMAK2.Hardware.Evo.TsFmSaa1099Renderer"),new object[]{(uint)6})))
        {
            var r=(SoundDeviceBase)f.Board;r.Volume=volume;for(int ch=0;ch<channels;ch++){Call(r,"SetReg",ch,(byte)0xFF);Call(r,"SetReg",8+ch,(byte)100);}for(int j=0;j<3;j++)Call(r,"SetReg",0x10+j,(byte)0x44);Call(r,"SetReg",0x14,(byte)((1<<channels)-1));Call(r,"SetReg",0x1C,(byte)1);
            for(int i=0;i<5;i++)f.Frame();var pcm=new List<uint>();int rawPeak=0;long rails=0;
            for(int i=0;i<10;i++){f.Frame();pcm.AddRange(r.AudioBuffer);var raw=(byte[])Field(r,"m_externalPcm");for(int j=0;j<raw.Length;j+=2)rawPeak=Math.Max(rawPeak,Math.Abs((int)BitConverter.ToInt16(raw,j)));rails+=r.AudioBuffer.Count(x=>(short)x==32767||(short)x== -32768);}
            Console.WriteLine("SAA channels={0} volume={1} nativePeak={2} PCM rms={3:F3} peak={4} railSamples={5}",channels,volume,rawPeak,Rms(pcm.Select(x=>(short)x)),pcm.Max(x=>Math.Abs((int)(short)x)),rails);
        }
    }
    static void NeoGs()
    {
        uint[] reference=null;
        foreach(int volume in new[]{100,50,0})using(var f=new Fixture(new NeoGsDevice()))
        {
            var r=(NeoGsDevice)f.Board;r.Volume=volume;var all=new List<uint>();
            // Exercise actual mixed DAC+MP3 output path with known samples,
            // not a guest song or native codec test. Skip CPU/timed DAC here,
            // so their pending old samples cannot overwrite the injected wave.
            for(int frame=0;frame<5;frame++){
                if(frame!=0)typeof(SoundDeviceBase).GetMethod("BeginFrame",Flags).Invoke(r,null);
                long start=(long)Field(r,"m_frameMasterStart");
                for(int i=0;i<100;i++){
                    int a=(int)(Math.Sin((frame*100+i)*0.31)*8000),b=(int)(Math.Cos((frame*100+i)*0.23)*4000);
                    FieldSet(r,"m_dacLeft",a);FieldSet(r,"m_dacRight",b);FieldSet(r,"m_mp3Left",b);FieldSet(r,"m_mp3Right",-a);
                    Call(r,"OutputMixed",start+i*24000L);
                }
                FieldSet(r,"m_boardMaster",start+2400000L);
                FieldSet(r,"m_nextDacMaster",start+2400001L);
                typeof(SoundDeviceBase).GetMethod("EndFrame",Flags,null,Type.EmptyTypes,null).Invoke(r,null);
                all.AddRange(r.AudioBuffer);
            }
            Console.WriteLine("NeoGS injected mixed PCM volume={0} rms={1:F6} hash={2}",volume,Rms(all.Select(x=>(short)x)),Hash(all.ToArray()));
            if(volume==100)reference=all.ToArray();
            else if(ExpectFix)for(int i=0;i<all.Count;i++){
                Check((short)all[i]==(short)reference[i]*volume/100,"NeoGS left volume");
                Check((short)(all[i]>>16)==(short)(reference[i]>>16)*volume/100,"NeoGS right volume");
            }
        }
    }
    static void OplLevels()
    {
        foreach(int kind in new[]{0,1,2}){
            BusDeviceBase board=kind==0?(BusDeviceBase)new ZxOpl3Ur4qbpDevice():kind==1?(BusDeviceBase)new ZxMultiSoundMaxDevice():new ZxmMoonSoundDevice();
            var max=board as ZxMultiSoundMaxDevice;if(max!=null){max.GeneralSoundEnabled=false;max.AutomaticConfiguration=false;}
            using(var f=new Fixture(board,true)){
                OplTone(f.Reg);for(int i=0;i<5;i++)f.Frame();
                var r=kind==1?((IAdditionalSoundRenderers)board).SoundRenderers.First(x=>x.GetType().Name=="ZxMaxOpl3Renderer"):(ISoundRenderer)board;
                var all=new List<uint>();for(int i=0;i<20;i++){f.Frame();all.AddRange(r.AudioBuffer);}
                Console.WriteLine("OPL same patch board={0} PCM RMS={1:F6} peak={2}",board.Name,Rms(all.Select(x=>(short)x)),all.Max(x=>Math.Abs((int)(short)x)));
            }
        }
    }
    static void InitializeMaxOpl3ForProbe(SoundDeviceBase r)
    {
        var native=r.GetType().GetNestedType("NativeMethods",BindingFlags.NonPublic);
        var write=native.GetMethod("Write",BindingFlags.Static|BindingFlags.NonPublic);
        var read=native.GetMethod("Read",BindingFlags.Static|BindingFlags.NonPublic);
        var core=Field(r,"m_core");
        Action<uint,byte> w=(p,v)=>write.Invoke(null,new object[]{core,p,v});
        w(2,5);w(3,2); // Enable NEW2 only to access the hidden OPL4 mixer.
        w(4,0xF8);w(5,0); // Full-scale FM, no post-PCM gain compensation.
        Check((byte)read.Invoke(null,new object[]{core,(uint)5})==0,"Max F8 init did not apply");
        w(2,5);w(3,0);w(0,0); // Restore FM reset mode and address.
        read.Invoke(null,new object[]{core,(uint)0}); // Consume OPL4-only ID.
    }
    static void MaxModel()
    {
        for(int vector=0;vector<4;vector++)foreach(int kind in new[]{0,1,2})
        {
            BusDeviceBase board=kind==0?(BusDeviceBase)new ZxOpl3Ur4qbpDevice():new ZxMultiSoundMaxDevice();
            var max=board as ZxMultiSoundMaxDevice;if(max!=null){max.GeneralSoundEnabled=false;max.AutomaticConfiguration=false;}
            using(var f=new Fixture(board,true)){
                var r=kind!=0?(SoundDeviceBase)((IAdditionalSoundRenderers)board).SoundRenderers.First(x=>x.GetType().Name=="ZxMaxOpl3Renderer"):(SoundDeviceBase)board;
                if(kind==2)InitializeMaxOpl3ForProbe(r);
                if(kind==1&&ExpectMaxFix){
                    var native=r.GetType().GetNestedType("NativeMethods",BindingFlags.NonPublic);var core=Field(r,"m_core");
                    native.GetMethod("Write",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{core,(uint)4,(byte)0xF8});
                    Check((byte)native.GetMethod("Read",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{core,(uint)5})==0,"production Max F8 must be zero");
                    native.GetMethod("Write",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{core,(uint)0,(byte)0});
                }
                byte status=f.Machine.CPU.RDPORT(0xC4);Check(status==0,"OPL reset status");
                OplTone(f.Reg);f.Reg(0x43,vector*8);f.Reg(0xA0,0x44+vector*35);f.Reg(0xC0,0x30|(vector&1));
                for(int i=0;i<5;i++)f.Frame();var all=new List<uint>();
                for(int i=0;i<20;i++){f.Frame();all.AddRange(r.AudioBuffer);}
                f.Reg(2,255);f.Reg(4,1);f.Frame();byte timer=f.Machine.CPU.RDPORT(0xC4);Check((timer&0xC0)==0xC0,"OPL timer A after init");
                f.Reg(4,0x80);Check(f.Machine.CPU.RDPORT(0xC4)==0,"OPL timer clear after init");
                f.Machine.DebugReset();f.Machine.CPU.regs.PC=0x4000;if(kind==2)InitializeMaxOpl3ForProbe(r);
                Check(f.Machine.CPU.RDPORT(0xC4)==0,"OPL second reset status");
                // Device ResetState during a partially rendered host frame.
                // This is a timing-state probe, not a guest playback claim.
                for(int reset=0;reset<3;reset++){
                    OplTone(f.Reg);f.Machine.CPU.Tact+=10000;r.ResetState();
                    Check(f.Machine.CPU.RDPORT(0xC4)==0,"OPL mid-frame reset status");
                    f.Frame();
                }
                Console.WriteLine("Max model vector={0} kind={1} reset={2:X2} timer={3:X2} RMS={4:F6} peak={5} hash={6}",vector,kind,status,timer,Rms(all.Select(x=>(short)x)),all.Max(x=>Math.Abs((int)(short)x)),Hash(all.ToArray()));
            }
        }
    }
    static void PairRoutes()
    {
        uint[] reference=null;
        foreach(bool pair in new[]{false,true}){
            var opl=new ZxOpl3Ur4qbpDevice();var multi=new ZxMultiSoundDevice();multi.GeneralSoundEnabled=false;multi.AutomaticConfiguration=false;
            using(var f=new Fixture(opl,true,44100,pair?multi:null)){
                OplTone(f.Reg);if(pair)OpnTone(Field(multi,"m_fm"),2,3,40);
                for(int i=0;i<5;i++)f.Frame();var pcm=new List<uint>();for(int i=0;i<10;i++){f.Frame();pcm.AddRange(opl.AudioBuffer);}
                if(reference==null)reference=pcm.ToArray();else Check(Hash(reference)==Hash(pcm.ToArray()),"MultiSound changed separate OPL3 renderer");
                if(pair){
                    ((ISoundRenderer)multi).Volume=0;Check(opl.Volume==100,"MultiSound volume leaked to OPL3");
                    f.Reg(2,255);f.Reg(4,1);f.Frame();Check((f.Machine.CPU.RDPORT(0xC4)&0xC0)==0xC0,"pair OPL timer route");
                }
                Console.WriteLine("OPL3 + MultiSound pair={0} independent PCM hash={1}",pair,Hash(pcm.ToArray()));
            }
        }
    }
    static void NeoGsProgression()
    {
        uint[] reference=null;long tact=0;ushort pc=0;
        foreach(bool mute in new[]{false,true})using(var f=new Fixture(new NeoGsDevice())){
            var r=(NeoGsDevice)f.Board;for(int i=0;i<5;i++)f.Frame();
            var cpu=(ZXMAK2.Engine.Cpu.CpuUnit)Field(r,"m_cpu");long before=cpu.Tact;
            ((ISoundRenderer)r).Volume=mute?0:100;for(int i=0;i<10;i++)f.Frame();
            Check(cpu.Tact>before,"NeoGS CPU froze while muted");
            r.Volume=100;f.Frame();var pcm=(uint[])r.AudioBuffer.Clone();
            if(reference==null){reference=pcm;tact=cpu.Tact;pc=cpu.regs.PC;}
            else{Check(Hash(pcm)==Hash(reference),"NeoGS mute changed PCM state");Check(cpu.Tact==tact&&cpu.regs.PC==pc,"NeoGS mute changed CPU state");}
            Console.WriteLine("NeoGS live mute={0} CPU tact={1} PC={2:X4} PCM={3}",mute,cpu.Tact,cpu.regs.PC,Hash(pcm));
        }
    }
    static void HeldDac()
    {
        foreach(ZxMultiSoundCore b in new ZxMultiSoundCore[]{new ZxMultiSoundDevice(),new ZxMultiSoundMaxDevice(),new ZxOmniSoundDevice()}){
            b.AutomaticConfiguration=false;b.GeneralSoundEnabled=false;
            using(var f=new Fixture(b)){
                f.Frame();f.Machine.CPU.WRPORT(0x0F,0xC0);for(int i=0;i<4;i++)f.Frame();
                int before=b.AudioBuffer.Max(x=>Math.Abs((int)(short)x));
                ((ISoundRenderer)b).Volume=0;for(int i=0;i<4;i++)f.Frame();
                int muted=b.AudioBuffer.Max(x=>Math.Abs((int)(short)x));
                f.Machine.CPU.WRPORT(0x0F,0xC0);for(int i=0;i<4;i++)f.Frame();
                int repeated=b.AudioBuffer.Max(x=>Math.Abs((int)(short)x));
                b.Volume=50;for(int i=0;i<4;i++)f.Frame();
                int half=b.AudioBuffer.Max(x=>Math.Abs((int)(short)x));
                b.Volume=0;for(int i=0;i<4;i++)f.Frame();
                f.Machine.CPU.WRPORT(0x0F,0xC1);for(int i=0;i<4;i++)f.Frame();
                int changed=b.AudioBuffer.Max(x=>Math.Abs((int)(short)x));
                b.Volume=100;for(int i=0;i<4;i++)f.Frame();int restored=b.AudioBuffer.Max(x=>Math.Abs((int)(short)x));
                if(ExpectHeldFix){Check(before==8064,"DAC fixture amplitude");Check(muted==0&&repeated==0&&changed==0,"held DAC mute");Check(half==4032,"held DAC half volume");Check(restored==8190,"held DAC state did not advance while muted");}
                Console.WriteLine("Held DAC {0}: 100%peak={1} mutedPeak={2} repeatedValuePeak={3} 50%peak={4} changedValuePeak={5} restoredAfterMutedWrite={6}",b.Name,before,muted,repeated,half,changed,restored);
            }
        }
    }
    static void MultiDacFingerprint()
    {
        foreach(ZxMultiSoundCore b in new ZxMultiSoundCore[]{new ZxMultiSoundDevice(),new ZxMultiSoundMaxDevice(),new ZxOmniSoundDevice()}){
            b.AutomaticConfiguration=false;b.GeneralSoundEnabled=false;
            using(var f=new Fixture(b)){
                byte[] program={0x01,0x0F,0x00,0x3E,0x00,0x3C,0xED,0x79,0x18,0xFB};
                for(int i=0;i<program.Length;i++)f.Machine.CPU.WRMEM((ushort)(0x4000+i),program[i]);
                f.Machine.CPU.regs.PC=0x4000;for(int i=0;i<5;i++)f.Frame();
                var all=new List<uint>();for(int i=0;i<20;i++){f.Frame();all.AddRange(b.AudioBuffer);}
                if(ExpectHeldFix)Check(Hash(all.ToArray())=="68886C686FB5C009E22D665274335AEFD6ED90E42EFB60C416F4A3E37E5B1F74","held-DAC fix changed 100% timed DAC waveform");
                Console.WriteLine("Multi DAC 100% fingerprint {0}: RMS={1:F6} hash={2}",b.Name,Rms(all.Select(x=>(short)x)),Hash(all.ToArray()));
            }
        }
    }
    [STAThread] static int Main(string[] args)
    {
        PortableBootstrap.Initialize();CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
        ExpectFix=args.Contains("--expect-fix");
        SkipV97Hash=args.Contains("--skip-v97-hash");
        ExpectMaxFix=args.Contains("--expect-max-fix");
        ExpectHeldFix=args.Contains("--expect-held-fix");
        try{if(args.Contains("--max-model"))MaxModel();else if(args.Contains("--neogs")){NeoGs();NeoGsProgression();}else if(args.Contains("--held-dac")){HeldDac();MultiDacFingerprint();}else{VolumeContracts();Omni();Opn();Saa();NeoGs();NeoGsProgression();OplLevels();PairRoutes();}Console.WriteLine("PASS: "+Checks+" assertions");return 0;}catch(Exception ex){Console.WriteLine(ex);return 1;}
    }
}
