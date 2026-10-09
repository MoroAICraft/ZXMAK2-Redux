using System;
using System.IO;
using System.Reflection;

// SPI regression for CMD24/CMD25. Uses the selected Circuits.dll, memory only.
class SdWriteProtocolProbe {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static object card;static Type type;static MemoryStream media;static int checks;
    static byte Clock(byte value){byte read=(byte)type.GetMethod("Rd",F).Invoke(card,null);type.GetMethod("Wr",F).Invoke(card,new object[]{value});return read;}
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
    static void Setup(bool writable){
        card=Activator.CreateInstance(type);media=new MemoryStream(new byte[8192],writable);
        type.GetField("fstream",F).SetValue(card,media);type.GetMethod("UpdateCardCapacity",F).Invoke(card,new object[]{media.Length});
        type.GetMethod("Reset",F).Invoke(card,null);
        Command(0,0,0x95,1);Command(16,512,0xFF,0);
    }
    static void Command(int number,uint argument,byte crc,byte expected){
        Clock(0xFF);Clock((byte)(0x40|number));for(int n=3;n>=0;n--)Clock((byte)(argument>>(n*8)));Clock(crc);
        byte reply=0xFF;for(int n=0;n<16&&reply==0xFF;n++)reply=Clock(0xFF);
        Check(reply==expected,"CMD"+number+" R1="+reply.ToString("X2")+" expected="+expected.ToString("X2"));
    }
    static byte[] Pattern(int seed){var b=new byte[512];for(int n=0;n<512;n++)b[n]=(byte)(n*19+seed);return b;}
    static void Block(byte token,byte[] data,byte expected){
        Check(Clock(token)==0xFF,"Data token produced premature response");
        for(int n=0;n<data.Length;n++)Check(Clock(data[n])==0xFF,"Response appeared during data byte "+n);
        Check(Clock(0x12)==0xFF,"Response appeared before first CRC byte");
        Check(Clock(0x34)==0xFF,"Response appeared before second CRC byte");
        Check((Clock(0xFF)&31)==expected,"Wrong data response");
    }
    static void Match(int offset,byte[] data){var all=media.ToArray();for(int n=0;n<512;n++)Check(all[offset+n]==data[n],"Media mismatch "+n);}
    static int Main(string[] args){try{
        string path=Path.GetFullPath(args[0]);AppDomain.CurrentDomain.AssemblyResolve+=delegate(object o,ResolveEventArgs e){string dll=Path.Combine(Path.GetDirectoryName(path),new AssemblyName(e.Name).Name+".dll");return File.Exists(dll)?Assembly.LoadFrom(dll):null;};
        type=Assembly.LoadFrom(path).GetType("ZXMAK2.Hardware.Circuits.SecureDigital.SdCard",true);
        Setup(true);var one=Pattern(7);Command(24,512,0xFF,0);Block(0xFE,one,5);Match(512,one);
        Command(17,512,0xFF,0);byte b=0xFF;for(int n=0;n<16&&b!=0xFE;n++)b=Clock(0xFF);Check(b==0xFE,"Read token");
        for(int n=0;n<512;n++)Check(Clock(0xFF)==one[n],"CMD17 readback mismatch");Clock(0xFF);Clock(0xFF);
        Setup(true);var two=Pattern(11);Command(25,1024,0xFF,0);Block(0xFC,one,5);Block(0xFC,two,5);Clock(0xFD);Clock(0xFF);
        Match(1024,one);Match(1536,two);Command(24,2048,0xFF,0);Block(0xFE,two,5);Match(2048,two);
        Setup(false);Command(24,512,0xFF,0);Block(0xFE,one,13);Match(512,new byte[512]);
        Setup(true);Command(24,8192,0xFF,0);Block(0xFE,one,13);Check(media.Length==8192,"Out of range extended media");
        Console.WriteLine("PASS: "+checks+" SD SPI checks: R1, two CRC clocks, CMD24/CMD25, readback, stop, read-only and bounds");return 0;
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
