using System;
using System.IO;

// Level-independent coarse comparison, NOT a listening test or identical
// register-trace test. The two tracker players can schedule writes differently.
internal static class AudioReferenceCompare
{
    private sealed class Wave { internal int Rate; internal float[] L,R; }
    private static Wave Load(string path)
    {
        using(var r=new BinaryReader(File.OpenRead(path))) {
            if(new string(r.ReadChars(4))!="RIFF")throw new Exception("RIFF required");r.ReadInt32();r.ReadChars(4);
            int rate=0,channels=0,bits=0;
            while(r.BaseStream.Position+8<=r.BaseStream.Length) {
                string tag=new string(r.ReadChars(4));uint size=r.ReadUInt32();long end=r.BaseStream.Position+size+(size&1);
                if(tag=="fmt "){if(r.ReadInt16()!=1)throw new Exception("PCM required");channels=r.ReadInt16();rate=r.ReadInt32();r.ReadInt32();r.ReadInt16();bits=r.ReadInt16();}
                if(tag=="data") {
                    if(channels!=2||bits!=16)throw new Exception("stereo PCM16 required");
                    var wave=new Wave{Rate=rate,L=new float[size/4],R=new float[size/4]};
                    for(int i=0;i<wave.L.Length;i++){wave.L[i]=r.ReadInt16();wave.R[i]=r.ReadInt16();} return wave;
                }
                r.BaseStream.Position=end;
            }throw new Exception("Missing data");
        }
    }
    private static double[] Envelope(Wave w)
    {
        int n=w.Rate/50;var env=new double[w.L.Length/n];
        for(int f=0;f<env.Length;f++) {
            double sumL=0,sumR=0,square=0;
            for(int j=0;j<n;j++){int i=f*n+j;double l=w.L[i],r=w.R[i];sumL+=l;sumR+=r;square+=l*l+r*r;}
            env[f]=Math.Sqrt(Math.Max(0,square/(2*n)-(sumL*sumL+sumR*sumR)/(2*n*(double)n)));
        }return env;
    }
    private static double Correlation(double[] a,double[] b,int offset,int skip)
    {
        double s1=0,s2=0,q1=0,q2=0,prod=0;int count=a.Length-skip;
        for(int i=skip;i<a.Length;i++){double x=a[i],y=b[i+offset];s1+=x;s2+=y;q1+=x*x;q2+=y*y;prod+=x*y;}
        return (prod-s1*s2/count)/Math.Sqrt((q1-s1*s1/count)*(q2-s2*s2/count));
    }
    private static void Fft(double[] re,double[] im)
    {
        int n=re.Length;
        for(int i=1,j=0;i<n;i++) {int bit=n>>1;for(;(j&bit)!=0;bit>>=1)j^=bit;j^=bit;
            if(i<j){double t=re[i];re[i]=re[j];re[j]=t;t=im[i];im[i]=im[j];im[j]=t;}}
        for(int len=2;len<=n;len<<=1){double phase=-2*Math.PI/len,rootR=Math.Cos(phase),rootI=Math.Sin(phase);
            for(int start=0;start<n;start+=len){double wr=1,wi=0;for(int j=0;j<len/2;j++) {
                int a=start+j,b=a+len/2;double br=re[b]*wr-im[b]*wi,bi=re[b]*wi+im[b]*wr;
                re[b]=re[a]-br;im[b]=im[a]-bi;re[a]+=br;im[a]+=bi;
                double t=wr*rootR-wi*rootI;wi=wr*rootI+wi*rootR;wr=t;
            }}}
    }
    private static readonly int[] Bands={0,70,250,1000,5000,10000,15000,22051};
    private static void Spectrum(Wave w,int start,int count,string label)
    {
        const int n=8192;var re=new double[n];var im=new double[n];var power=new double[Bands.Length-1];
        double meanL=0,meanR=0,ql=0,qr=0;
        for(int i=start;i<start+count;i++){double l=w.L[i],r=w.R[i];meanL+=l;meanR+=r;ql+=l*l;qr+=r*r;}
        meanL/=count;meanR/=count;double ac=Math.Sqrt((ql/count-meanL*meanL+qr/count-meanR*meanR)/2);
        int blocks=0;
        for(int i=start;i+n<=start+count;i+=n)for(int channel=0;channel<2;channel++) {
            var data=channel==0?w.L:w.R;double mean=0;for(int j=0;j<n;j++)mean+=data[i+j];mean/=n;
            for(int j=0;j<n;j++){re[j]=(data[i+j]-mean)*(.5-.5*Math.Cos(2*Math.PI*j/(n-1)));im[j]=0;}
            Fft(re,im);blocks++;
            for(int bin=1;bin<=n/2;bin++){double hz=(double)bin*w.Rate/n;int band=0;while(band<power.Length-1 && hz>=Bands[band+1])band++;
                power[band]+=re[bin]*re[bin]+im[bin]*im[bin];}
        }
        double total=0;foreach(double p in power)total+=p;
        Console.WriteLine("SEGMENT {0}: offset={1:F3}s length={2:F3}s AC_RMS={3:F3} dBFS={4:F3} windows={5}",label,(double)start/w.Rate,(double)count/w.Rate,ac,20*Math.Log10(ac/32768),blocks);
        for(int band=0;band<power.Length;band++)Console.WriteLine("BAND {0} {1}-{2}Hz powerPercent={3:F5}",label,Bands[band],Bands[band+1],100*power[band]/total);
    }
    private static int Main(string[] args)
    {
        try {
            var raw=Load(args[0]);var mixed=Load(args[1]);var reference=Load(args[2]);
            if(raw.Rate!=mixed.Rate||raw.Rate!=reference.Rate||raw.L.Length!=mixed.L.Length)throw new Exception("Rate/length mismatch");
            var a=Envelope(raw);var b=Envelope(reference);int best=0;double score=-2;
            for(int shift=0;shift<Math.Min(600,b.Length-a.Length);shift++){double c=Correlation(a,b,shift,100);if(c>score){score=c;best=shift;}}
            Console.WriteLine("Coarse20ms energy-envelope alignment: bestOffset={0:F3}s Pearson={1:F5}; skip first2sec, search0..12sec",best/50D,score);
            int skip=raw.Rate*2,count=raw.L.Length-skip;
            Spectrum(raw,skip,count,"ZX raw AY");Spectrum(mixed,skip,count,"ZX final mix");Spectrum(reference,best*(raw.Rate/50)+skip,count,"ZXTune AY HQ");
            Console.WriteLine("LIMIT: coarse musical segment alignment and normalized spectrum only; no sample-perfect alignment or subjective quality verdict.");
            return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
