// Bounded diagnostic register replay of existing v88 fixtures; not guest playback.
// Link with tools/SoundCards/{SoundCardsProbe,MusicFixtures}.cs. No benchmarks.
using System;
using System.Globalization;
using System.IO;
using System.Reflection;

static partial class SoundCardsProbe
{
    public static void AuditOpnMix(string folder)
    {
        foreach(string name in new[]{"Taotao10.tfd","Psycho_Fox_level1_v7.tfd","Taotao10.tfc","sonic_title.tfc"})
        {
            var bytes=File.ReadAllBytes(Path.Combine(folder,name));
            var song=name.EndsWith(".tfd")?Tfd(bytes):Tfc(bytes);
            long samples=0,sumClipped=0,chipClipped=0;int sumPeak=0,chipPeak=0;
            using(var f=new Fixture(0,44100,false)){
                foreach(var w in song.Initial){f.Select(w.Chip,true);f.Reg(w.Reg,w.Value);}
                var field=f.Fm.GetType().GetField("m_samples",BindingFlags.Instance|BindingFlags.NonPublic);
                foreach(var frame in song.Frames){
                    foreach(var w in frame){f.Select(w.Chip,true);f.Reg(w.Reg,w.Value);}
                    f.Frame();var pcm=(short[][])field.GetValue(f.Fm);
                    for(int i=0;i<f.Fm.AudioBuffer.Length;i++){
                        int a=pcm[0][i],b=pcm[1][i],sum=a+b;
                        chipPeak=Math.Max(chipPeak,Math.Max(Math.Abs(a),Math.Abs(b)));
                        sumPeak=Math.Max(sumPeak,Math.Abs(sum));samples++;
                        if(a==32767||a== -32768||b==32767||b== -32768)chipClipped++;
                        if(sum>32767||sum< -32768)sumClipped++;
                    }
                }
            }
            Console.WriteLine("{0}: frames={1} samples={2} chipPeak={3} sumPeak={4} chipRail={5} preMixerSumClamp={6} ({7:F5}%)",name,song.Frames.Count,samples,chipPeak,sumPeak,chipClipped,sumClipped,100D*sumClipped/samples);
        }
    }
}
static class OpnMixAudit
{
    [STAThread] static int Main(string[] args)
    {
        PortableBootstrap.Initialize();CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
        try{SoundCardsProbe.AuditOpnMix(args[0]);return 0;}catch(Exception e){Console.WriteLine(e);return 1;}
    }
}
