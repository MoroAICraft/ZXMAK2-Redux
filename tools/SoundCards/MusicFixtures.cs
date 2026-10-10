// Diagnostic decoder of the four user fixtures, not an emulator/player feature.
// Binary command semantics cross-checked against the primary ZXTune sources:
// https://github.com/vitamin-caig/zxtune/blob/develop/src/formats/chiptune/fm/tfc.cpp
// https://github.com/vitamin-caig/zxtune/blob/develop/src/module/players/tfm/tfc.cpp
// https://github.com/vitamin-caig/zxtune/blob/develop/src/formats/chiptune/fm/tfd.cpp
// Independent C# diagnostic implementation. No musical files are altered.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

static partial class SoundCardsProbe
{
    struct Write { public int Chip,Reg,Value; public Write(int chip,int reg,int value){Chip=chip;Reg=reg;Value=value;} }
    sealed class Song { public List<List<Write>> Frames=new List<List<Write>>();public List<Write> Initial=new List<Write>(); }
    static void NewFrames(Song song,int count){while(count-->0)song.Frames.Add(new List<Write>());}
    static Song Tfd(byte[] data)
    {
        if(Encoding.ASCII.GetString(data,0,4)!="TFMD")throw new Exception("TFD signature");
        int p=4;for(int text=0;text<3;text++){while(data[p++]!=0){}}
        var song=new Song();int chip=0;
        while(p<data.Length)
        {
            int cmd=data[p++];if(cmd==0xFB)return song;
            if(cmd==0xFF||cmd==0xFE){NewFrames(song,cmd==0xFF?1:3+data[p++]);chip=0;}
            else if(cmd==0xFD)chip=1;else if(cmd==0xFC)chip=0;else if(cmd!=0xFA)
                (song.Frames.Count==0?song.Initial:song.Frames.Last()).Add(new Write(chip,cmd,data[p++]));
        }
        throw new Exception("TFD no finish");
    }
    static int Byte(byte[] data,ref int p){if(p<0||p>=data.Length)throw new Exception("TFC cursor");return data[p++];}
    static int Signed(byte[] data,ref int p){return (short)((Byte(data,ref p)<<8)|Byte(data,ref p));}
    static void Freq(List<Write> frame,int channel,int value){frame.Add(new Write(channel/3,0xA4+channel%3,value>>8));frame.Add(new Write(channel/3,0xA0+channel%3,value&255));}
    static int FrameData(byte[] data,int p,int channel,List<Write> frame,ref int freq)
    {
        int flags=Byte(data,ref p);if((flags&0xC0)!=0)frame.Add(new Write(channel/3,0x28,channel%3));
        if((flags&1)!=0){freq=(Byte(data,ref p)<<8)|Byte(data,ref p);Freq(frame,channel,freq);}
        for(int regs=(flags&0x3E)>>1;regs>0;regs--)frame.Add(new Write(channel/3,Byte(data,ref p),Byte(data,ref p)));
        if((flags&0x80)!=0)frame.Add(new Write(channel/3,0x28,0xF0|channel%3));return p;
    }
    static Song Tfc(byte[] data)
    {
        if(Encoding.ASCII.GetString(data,0,6)!="TFMcom"||data[9]!=50)throw new Exception("Only validated 50Hz TFC fixtures supported");
        var streams=new List<List<Write>>[6];
        for(int channel=0;channel<6;channel++)
        {
            var frames=streams[channel]=new List<List<Write>>();int p=data[10+channel*2]|data[11+channel*2]<<8;
            int repeat=0,ret=0,freq=0,limit=100000;
            while(p!=0&&limit-->0)
            {
                if(repeat>0&&--repeat==0){p=ret;ret=0;}
                int cmd;
                for(;;){cmd=Byte(data,ref p);if(cmd==0x7E)continue;if(cmd==0x7F){p=0;break;}
                    if(cmd==0xD0){if(repeat!=0||ret!=0)throw new Exception("nested TFC repeat");repeat=Byte(data,ref p);int offset=Signed(data,ref p);ret=p;p+=offset;}
                    else p--;break;}
                if(p==0)break;
                var frame=new List<Write>();frames.Add(frame);cmd=Byte(data,ref p);
                if(cmd==0xBF){int off=Signed(data,ref p);FrameData(data,p+off,channel,frame,ref freq);}
                else if(cmd==0xFF){int off=-256+Byte(data,ref p);FrameData(data,p+off,channel,frame,ref freq);}
                else if(cmd>=0xE0){for(int skip=255-cmd;skip>0;skip--)frames.Add(new List<Write>());}
                else if(cmd>=0xC0){freq=(freq&0xFF00)|((freq+cmd+0x30)&255);Freq(frame,channel,freq);}
                else p=FrameData(data,p-1,channel,frame,ref freq);
            }
            if(limit<=0)throw new Exception("TFC runaway");
        }
        int length=streams[0].Count;Check(streams.All(s=>s.Count==length),"fixture streams have unequal length");
        var song=new Song();for(int frame=0;frame<length;frame++){song.Frames.Add(new List<Write>());foreach(var stream in streams)song.Frames[frame].AddRange(stream[frame]);}return song;
    }
    static string Replay(Song song,int kind,bool legacy,string wav,out int peak,out double rms,out int clipped)
    {
        peak=clipped=0;double energy=0;long count=0;
        using(var f=new Fixture(kind,44100,legacy))using(var pcm=new MemoryStream())
        {
            foreach(var w in song.Initial){f.Select(w.Chip,true);f.Reg(w.Reg,w.Value);}
            foreach(var frame in song.Frames)
            {
                foreach(var w in frame){f.Select(w.Chip,true);f.Reg(w.Reg,w.Value);}
                foreach(uint packed in f.Frame()){short value=(short)packed;peak=Math.Max(peak,Math.Abs((int)value));energy+=(double)value*value;count++;if(value==short.MinValue||value==short.MaxValue)clipped++;pcm.WriteByte((byte)value);pcm.WriteByte((byte)(value>>8));}
            }
            rms=Math.Sqrt(energy/Math.Max(1,count));byte[] bytes=pcm.ToArray();
            if(wav!=null)using(var writer=new BinaryWriter(File.Open(wav,FileMode.Create))){writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+bytes.Length);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(44100);writer.Write(88200);writer.Write((short)2);writer.Write((short)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(bytes.Length);writer.Write(bytes);}
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");
        }
    }
    static void Music(string folder)
    {
        string output=ZXMAK2.PortablePaths.DataDirectory("Recordings");
        string[] names={"Taotao10.tfd","Psycho_Fox_level1_v7.tfd","Taotao10.tfc","sonic_title.tfc"};int[] lengths={2304,3584,2306,626},events={14812,5965,8273,175};
        for(int index=0;index<names.Length;index++)
        {
            byte[] data=File.ReadAllBytes(Path.Combine(folder,names[index]));var song=names[index].EndsWith(".tfd")?Tfd(data):Tfc(data);
            int writes=song.Initial.Count+song.Frames.Sum(frame=>frame.Count);Check(song.Frames.Count==lengths[index]&&writes==events[index],"fixture decode count "+names[index]+": "+song.Frames.Count+"/"+writes);
            using(var sha=SHA256.Create())Console.WriteLine("{0}: frames={1}, writes={2}, SHA256={3}",names[index],song.Frames.Count,writes,BitConverter.ToString(sha.ComputeHash(data)).Replace("-",""));
            string reference=null;int peak,clip;double rms;
            for(int kind=0;kind<4;kind++){string hash=Replay(song,kind,false,kind==0?Path.Combine(output,names[index]+"-ymfm.wav"):null,out peak,out rms,out clip);if(reference==null)reference=hash;Check(hash==reference,"musical board PCM disagreement");Check(peak>0,"silent song");Console.WriteLine("  board {0}: peak={1}, RMS={2:F1}, saturated={3}, PCM={4}",kind,peak,rms,clip,hash);}
            Replay(song,0,true,Path.Combine(output,names[index]+"-legacy.wav"),out peak,out rms,out clip);Console.WriteLine("  legacy control: peak={0}, RMS={1:F1}, saturated={2}",peak,rms,clip);
        }
        Console.WriteLine("WAV controls retained in {0}; actual OmniPlayer/NedoOS launch not performed by this probe.",output);
    }
}
