using System;
using System.IO;
using System.Linq;

// Research analysis of generated PCM artifacts and the user's copied SMF.
internal static class AudioFileMetrics
{
    private static void Wav(string path)
    {
        using(var reader=new BinaryReader(File.OpenRead(path))) {
            if(new string(reader.ReadChars(4))!="RIFF") throw new Exception("Not RIFF");
            reader.ReadInt32(); if(new string(reader.ReadChars(4))!="WAVE") throw new Exception("Not WAVE");
            int rate=0,channels=0,bits=0; long bytes=0,offset=0;
            while(reader.BaseStream.Position+8<=reader.BaseStream.Length) {
                string id=new string(reader.ReadChars(4)); uint size=reader.ReadUInt32(); long next=reader.BaseStream.Position+size+(size&1);
                if(id=="fmt ") {if(reader.ReadInt16()!=1) throw new Exception("Not PCM");channels=reader.ReadInt16();rate=reader.ReadInt32();reader.ReadInt32();reader.ReadInt16();bits=reader.ReadInt16();}
                if(id=="data") {offset=reader.BaseStream.Position; bytes=size;break;} reader.BaseStream.Position=next;
            }
            if(bits!=16 || channels!=2 || offset==0) throw new Exception("Expected stereo PCM16");
            long frames=bytes/4; reader.BaseStream.Position=offset; long clipped=0; int peak=0;
            double sl=0,sr=0,ql=0,qr=0,lr=0,changes=0; short lastL=0,lastR=0;
            for(long i=0;i<frames;i++) {
                short l=reader.ReadInt16(),r=reader.ReadInt16();sl+=l;sr+=r;ql+=(double)l*l;qr+=(double)r*r;lr+=(double)l*r;
                peak=Math.Max(peak,Math.Max(Math.Abs((int)l),Math.Abs((int)r)));
                if(l==32767 || l==-32768 || r==32767 || r==-32768) clipped++;
                if(i>0) changes+=(double)(l-lastL)*(l-lastL)+(double)(r-lastR)*(r-lastR);
                lastL=l;lastR=r;
            }
            double ml=sl/frames,mr=sr/frames,vl=ql/frames-ml*ml,vr=qr/frames-mr*mr;
            double ac=Math.Sqrt((vl+vr)/2), correlation=(lr/frames-ml*mr)/Math.Sqrt(vl*vr);
            Console.WriteLine("WAV {0}: rate={1} seconds={2:F3} ACrms={3:F3} dBFSrms={4:F3} peak={5} mean={6:F3}/{7:F3} stereoCorrelation={8:F4} endpointFrames={9} differenceRMS={10:F3}",
                Path.GetFileName(path),rate,(double)frames/rate,ac,20*Math.Log10(ac/32768D),peak,ml,mr,correlation,clipped,Math.Sqrt(changes/(frames*2)));
        }
    }
    private static int Big(byte[] b,ref int pos,int count)
    {int result=0;for(int i=0;i<count;i++) result=(result<<8)|b[pos++];return result;}
    private static int Var(byte[] b,ref int pos)
    {int result=0;for(int i=0;i<4;i++){byte value=b[pos++];result=(result<<7)|(value&127);if(value<128)return result;}throw new Exception("Invalid MIDI VLQ");}
    private static void Midi(string path)
    {
        byte[] b=File.ReadAllBytes(path);int pos=0;
        if(System.Text.Encoding.ASCII.GetString(b,pos,4)!="MThd")throw new Exception("Not SMF");pos+=4;
        int header=Big(b,ref pos,4),format=Big(b,ref pos,2),tracks=Big(b,ref pos,2),division=Big(b,ref pos,2);pos=8+header;
        int notes=0,min=127,max=0;long sum=0,maxTick=0;var channels=new int[16];
        var cc7=new int[128];var cc11=new int[128]; int tempos=0,minTempo=int.MaxValue,maxTempo=0;
        for(int track=0;track<tracks;track++) {
            string tag=System.Text.Encoding.ASCII.GetString(b,pos,4);pos+=4;int length=Big(b,ref pos,4),end=pos+length;
            if(tag!="MTrk")throw new Exception("Missing track");int status=0;long tick=0;
            while(pos<end) {
                tick+=Var(b,ref pos);int value=b[pos];if(value>=128) {status=value;pos++;}
                if(status==255) {int type=b[pos++],len=Var(b,ref pos);if(type==0x51 && len==3){int t=(b[pos]<<16)|(b[pos+1]<<8)|b[pos+2];tempos++;minTempo=Math.Min(minTempo,t);maxTempo=Math.Max(maxTempo,t);}pos+=len;continue;}
                if(status==0xF0 || status==0xF7){int len=Var(b,ref pos);pos+=len;status=0;continue;}
                int command=status&0xF0,channel=status&15;if(command<0x80 || command>0xE0)throw new Exception("Unsupported MIDI event");
                int a=b[pos++];int c=(command==0xC0 || command==0xD0) ? 0:b[pos++];
                if(command==0x90 && c>0){notes++;channels[channel]++;min=Math.Min(min,c);max=Math.Max(max,c);sum+=c;}
                if(command==0xB0 && a==7)cc7[c]++;if(command==0xB0 && a==11)cc11[c]++;
            }
            maxTick=Math.Max(maxTick,tick);pos=end;
        }
        Console.WriteLine("SMF format={0} tracks={1} division={2} maxTick={3} noteOns={4} velocity min/avg/max={5}/{6:F2}/{7} tempos={8} tempoMicrosecRange={9}/{10}",format,tracks,division,maxTick,notes,min,(double)sum/Math.Max(1,notes),max,tempos,minTempo,maxTempo);
        Console.WriteLine("MIDI note channels="+String.Join(",",Enumerable.Range(0,16).Where(x=>channels[x]>0).Select(x=>(x+1)+":"+channels[x])));
        Console.WriteLine("MIDI CC7 values="+String.Join(",",Enumerable.Range(0,128).Where(x=>cc7[x]>0).Select(x=>x+":"+cc7[x])));
        Console.WriteLine("MIDI CC11 values="+String.Join(",",Enumerable.Range(0,128).Where(x=>cc11[x]>0).Select(x=>x+":"+cc11[x])));
        Console.WriteLine("LIMIT: event statistics, not integrated tempo/duration or emulator playback.");
    }
    private static int Main(string[] args)
    {try{foreach(string path in args){if(Path.GetExtension(path).Equals(".mid",StringComparison.OrdinalIgnoreCase))Midi(path);else Wav(path);}return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
