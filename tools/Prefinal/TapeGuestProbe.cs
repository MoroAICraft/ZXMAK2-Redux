using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Drawing;
using ZXMAK2;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Hardware.Evo;
using ZXMAK2.Hardware.General;
using ZXMAK2.Host.Entities;
using ZXMAK2.Host.Interfaces;
using ZXMAK2.Serializers;

static class TapeGuestProbe
{
    sealed class Keyboard : IKeyboardState { public Key? Held; public bool this[Key k] {get{return Held==k;}} }
    static Spectrum machine;static Keyboard keys=new Keyboard();static string output;
    static void Frames(int n)
    {
        for(int i=0;i<n;i++)
        {
            machine.BusManager.FindDevice<KeyboardDevice>().KeyboardState=keys;
            machine.BusManager.FindDevice<CmosPentEvo>().KeyboardState=keys;
            machine.ExecuteFrame();
        }
    }
    static void Press(Key key){keys.Held=key;Frames(8);keys.Held=null;Frames(8);}
    static void Shot(string name)
    {
        var ula=machine.BusManager.FindDevice<UlaPentEvo>();ula.ForceRedrawFrame();var v=ula.VideoData;
        using(var b=new Bitmap(v.Size.Width,v.Size.Height))
        {
            for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)b.SetPixel(x,y,Color.FromArgb(v.Buffer[y*b.Width+x]));
            b.Save(Path.Combine(output,name+".png"));
        }
        var tape=machine.BusManager.FindDevice<ITapeDevice>();
        Console.WriteLine("{0} PC={1:X4} HasTape={2} Playing={3} Block={4}",name,machine.CPU.regs.PC,tape.HasTape,tape.IsPlay,tape.CurrentBlock);
    }
    static void Block(MemoryStream s,byte[] b)
    {s.WriteByte((byte)b.Length);s.WriteByte((byte)(b.Length>>8));s.Write(b,0,b.Length);}
    static byte[] Tape()
    {
        byte[] basic={0,10,9,0,0xEA,(byte)'V',(byte)'9',(byte)'2',(byte)'T',(byte)'A',(byte)'P',(byte)'E',13};
        var header=new byte[19];header[1]=0;System.Text.Encoding.ASCII.GetBytes("V92TAPE   ").CopyTo(header,2);
        header[12]=(byte)basic.Length;header[15]=0x80;header[16]=(byte)basic.Length;
        for(int i=0;i<18;i++)header[18]^=header[i];
        var data=new byte[basic.Length+2];data[0]=255;basic.CopyTo(data,1);for(int i=0;i<data.Length-1;i++)data[data.Length-1]^=data[i];
        using(var s=new MemoryStream()){Block(s,header);Block(s,data);return s.ToArray();}
    }
    static int Main(string[] args)
    {
        try
        {
            PortableBootstrap.Initialize();output=PortablePaths.DataDirectory("Temp\\TapeGuest");
            var config=new XmlDocument();config.Load(PortablePaths.ConfigFile("machines.config"));
            var bus=config.SelectSingleNode("/Machines/Bus[@name='ZX-Evo BSconf']");
            machine=new Spectrum();machine.BusManager.Init(machine,true);machine.BusManager.LoadConfigXmlChecked(bus);
            var mem=machine.BusManager.FindDevice<MemoryPentEvo>();foreach(var page in mem.RamPages)Array.Clear(page,0,page.Length);
            machine.DebugReset();Frames(180);Shot("boot");
            var tape=machine.BusManager.FindDevice<ITapeDevice>();tape.UseTraps=true;tape.UseAutoPlay=true;
            var data=Tape();BaseConfMedia.Validate("fixture.tap",data);
            using(var s=new MemoryStream(data))machine.BusManager.LoadManager.GetSerializer(".tap").Deserialize(s);
            Press(Key.T);Frames(100);Shot("after-T");
            foreach(var arg in args){Press((Key)Enum.Parse(typeof(Key),arg));Frames(100);Shot("key-"+arg);}
            Frames(500);Shot("loaded");
            byte[] marker={0,10,9,0,0xEA,(byte)'V',(byte)'9',(byte)'2',(byte)'T',(byte)'A',(byte)'P',(byte)'E',13};bool loaded=false;
            foreach(var page in mem.RamPages)for(int p=0;p<page.Length-marker.Length;p++)
            {
                int match=0;while(match<marker.Length && page[p+match]==marker[match])match++;
                if(match==marker.Length){loaded=true;break;}
            }
            Console.WriteLine("Guest ROM TAP marker in RAM: "+loaded);
            machine.BusManager.Disconnect();machine.Dispose();return loaded?0:2;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
