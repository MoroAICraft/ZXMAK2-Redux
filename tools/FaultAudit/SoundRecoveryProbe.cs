// Compile with production DirectSound.cs; only the native boundary is fake.
using System;
using System.Threading;
using System.Reflection;
using System.Diagnostics;
using System.Linq;
using System.Collections.Concurrent;
using ZXMAK2.Host.Entities;
using ZXMAK2.Host.Interfaces;
using ZXMAK2.Host.Services;
using ZXMAK2.Host.WinForms.Mdx;
using Fake = ZXMAK2.DirectX.DirectSound.SecondarySoundBuffer;
using Device = ZXMAK2.DirectX.DirectSound.DirectSound8;

static class SoundRecoveryProbe {
    static int checks;
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool b,string s){checks++;if(!b)throw new Exception(s);}
    static Thread Worker(DirectSound s){return (Thread)typeof(DirectSound).GetField("_wavePlayThread",F).GetValue(s);}
    static void Wake(DirectSound s){((AutoResetEvent)typeof(DirectSound).GetField("_fillEvent",F).GetValue(s)).Set();}
    static void Ready(DirectSound s){Check(SpinWait.SpinUntil(()=>s.IsSyncSupported,3500),"output did not become available");}
    sealed class Samples : IFrameSound {
        readonly uint[] samples=Enumerable.Range(0,882).Select(i=>(uint)(i+1)).ToArray();
        public int SampleRate{get{return 44100;}} public uint[] GetBuffer(){return samples;}public void Refresh(){}
    }
    static readonly FrameInfo Info=new FrameInfo(null,0,0,44100,false);
    static readonly Samples Frame=new Samples();
    static void Reset(){Fake.Fault="";Fake.Faults=0;Fake.StopThrows=false;Fake.Writes=0;Fake.BadSilence=0;Fake.Matched=false;Device.Attempts=0;Device.Created=0;Device.Disposed=0;Fake.Created=0;Fake.Disposed=0;}
    static void Balance(){Check(Device.Created==Device.Disposed&&Fake.Created==Fake.Disposed,"native resource leak");}
    static void Recovery(string failure,bool startup) {
        Reset();if(startup)Fake.Fault=failure;
        using(var sound=new DirectSound(null,44100,4)) {
            Thread worker=Worker(sound);sound.IsSynchronized=true;
            if(!startup){Ready(sound);Fake.Fault=failure;Wake(sound);}
            Check(SpinWait.SpinUntil(()=>!sound.IsSyncSupported&&Fake.Faults>0,2500),"fault not observed: "+failure);
            var sw=Stopwatch.StartNew();for(int i=0;i<8;i++)sound.PushFrame(Info,Frame);sw.Stop();
            Check(sw.ElapsedMilliseconds<150,"dead audio consumer still waits per frame");
            Check(worker.IsAlive,"recovery worker died");
            Fake.Fault="";Ready(sound);Check(Device.Attempts>=2,"device not recreated");
            sound.IsSynchronized=false;sound.PushFrame(Info,Frame);
            for(int i=0;i<4&&!Fake.Matched;i++){Wake(sound);Thread.Sleep(15);}
            Check(Fake.Matched,"PCM did not resume unchanged after recovery");
            Check(Fake.BadSilence==0,"uninitialized PCM at startup/recovery");
            var fill=(ConcurrentQueue<uint[]>)typeof(DirectSound).GetField("_fillQueue",F).GetValue(sound);
            var play=(ConcurrentQueue<uint[]>)typeof(DirectSound).GetField("_playQueue",F).GetValue(sound);
            Check(SpinWait.SpinUntil(()=>fill.Count+play.Count==4,500),"managed buffer pool changed");
            sound.Dispose();Check(!worker.IsAlive,"dispose did not stop worker");
        }
        Balance();Console.WriteLine("PASS recovery: {0}, {1}; PCM unchanged, buffers balanced",failure,startup?"startup":"playing");
    }
    static void Persistent() {
        Reset();Fake.Fault="create";
        using(var sound=new DirectSound(null,44100,4)) {
            Check(SpinWait.SpinUntil(()=>Device.Attempts>0,1000),"create not reached");
            Thread.Sleep(2200);Check(Device.Attempts>=2&&Device.Attempts<=4,"unbounded retry/spin");
            Check(!sound.IsSyncSupported,"persistent failure reports sync available");
            var sw=Stopwatch.StartNew();sound.Dispose();sw.Stop();Check(sw.ElapsedMilliseconds<500,"dispose waits for recovery timer");
        }
        Balance();Console.WriteLine("PASS persistent failure: bounded retry frequency, immediate shutdown");
    }
    sealed class HostSound : IHostSound {
        public bool Available;public int Pushes;public int SampleRate{get{return 44100;}}
        public bool IsSyncSupported{get{return Available;}}public bool IsSynchronized{get;set;}
        public void PushFrame(IFrameInfo i,IFrameSound s){Pushes++;}public double GetLoadLevel(){return 0;}
        public void CancelWait(){}public void Dispose(){}
    }
    static void Pacing() {
        var sound=new HostSound();
        using(var host=new HostService(null,sound,null,null,null)) {
            host.SyncSource=SyncSource.Sound;var sw=Stopwatch.StartNew();
            for(int i=0;i<10;i++)host.PushFrame(Info,null,Frame);
            sw.Stop();Check(sw.ElapsedMilliseconds>=140&&sw.ElapsedMilliseconds<1500,"unavailable audio did not fall back to time pacing");
            Check(host.SyncSource==SyncSource.Sound&&sound.IsSynchronized,"stored sync preference changed");
            Console.WriteLine("PASS host time fallback: 10 frames={0}ms",sw.ElapsedMilliseconds);
            sound.Available=true;sw.Restart();for(int i=0;i<10;i++)host.PushFrame(Info,null,Frame);sw.Stop();
            Check(sw.ElapsedMilliseconds<150&&sound.Pushes==20,"host kept time pacing after recovery");
            sound.Available=false;host.SyncSource=SyncSource.None;sw.Restart();for(int i=0;i<10;i++)host.PushFrame(Info,null,Frame);sw.Stop();
            Check(sw.ElapsedMilliseconds<150,"turbo/no-sync mode was changed");
            host.CancelPush();
        }
    }
    static void StopFailure() {
        Reset();using(var sound=new DirectSound(null,44100,4)) {
            Ready(sound);Fake.StopThrows=true;var thread=Worker(sound);sound.Dispose();Check(!thread.IsAlive,"Stop exception prevented disposal");
        }
        Balance();Fake.StopThrows=false;Console.WriteLine("PASS Stop exception still releases device and exits");
    }
    [STAThread] static int Main() {
        try {
            foreach(string failure in new[]{"create","notify","write","play"})Recovery(failure,true);
            foreach(string failure in new[]{"write","position","lost"})Recovery(failure,false);
            Persistent();StopFailure();Pacing();
            Console.WriteLine("PASS sound recovery: {0} assertions; native boundary injection, not physical unplug",checks);return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
namespace ZXMAK2.Host.WinForms.Tools { internal static class RecoveryNamespace {} }
namespace ZXMAK2.DirectX.Native {
    public static unsafe class NativeHelper {
        public static void INITBLK(void* p,byte v,int n){for(int i=0;i<n;i++)((byte*)p)[i]=v;}
        public static void CPBLK(void* d,void* s,int n){for(int i=0;i<n;i++)((byte*)d)[i]=((byte*)s)[i];}
    }
}
namespace ZXMAK2.DirectX.DirectSound {
    public enum DSSCL{PRIORITY}public enum DSBLOCK{None}public enum DSBPLAY_FLAGS{LOOPING}
    [Flags] public enum DSBSTATUS{PLAYING=1,BUFFERLOST=2,LOOPING=4}
    [Flags] public enum DSBCAPS_FLAGS{CTRLVOLUME=1,CTRLPOSITIONNOTIFY=2,GLOBALFOCUS=4,GETCURRENTPOSITION2=8}
    public class WaveFormat{public WaveFormat(int rate,short bits,short channels){}}
    public class DSBUFFERDESC{public int dwBufferBytes;public DSBCAPS_FLAGS dwFlags;public WaveFormat Format;}
    public class DSBPOSITIONNOTIFY{public int dwOffset;public WaitHandle WaitHandle;}
    public sealed class DirectSound8:IDisposable {
        public static int Attempts,Created,Disposed;
        public DirectSound8(){Interlocked.Increment(ref Attempts);SecondarySoundBuffer.Fail("create");Interlocked.Increment(ref Created);}
        public void SetCooperativeLevel(IntPtr p,DSSCL s){}public void Dispose(){Interlocked.Increment(ref Disposed);}
    }
    public sealed class SecondarySoundBuffer:IDisposable {
        public static volatile string Fault="";public static volatile bool StopThrows,Matched;
        public static int Writes,Created,Disposed,BadSilence,Faults;int positions,writes;
        public static void Fail(string stage){if(Fault==stage){Interlocked.Increment(ref Faults);throw new System.IO.IOException("Injected native "+stage+" failure");}}
        public SecondarySoundBuffer(DirectSound8 d,DSBUFFERDESC b){Interlocked.Increment(ref Created);}
        public DSBSTATUS Status{get{if(Fault=="lost"){Interlocked.Increment(ref Faults);return DSBSTATUS.BUFFERLOST|DSBSTATUS.PLAYING;}return DSBSTATUS.PLAYING|DSBSTATUS.LOOPING;}}
        public int PlayPosition{get{Fail("position");return (++positions%4)*3528;}}
        public void SetNotificationPositions(DSBPOSITIONNOTIFY[] p){Fail("notify");}public void Play(int p,DSBPLAY_FLAGS f){Fail("play");}
        public void Stop(){if(StopThrows)throw new System.IO.IOException("Injected Stop failure");}
        public void Dispose(){Interlocked.Increment(ref Disposed);}
        public unsafe void Write(int offset,void* data,int size,DSBLOCK f){
            Fail("write");var samples=(uint*)data;bool match=size==882*4;
            for(int i=0;i<size/4;i++){if(writes<4&&samples[i]!=0)Interlocked.Increment(ref BadSilence);if(samples[i]!=(uint)(i+1))match=false;}
            if(match)Matched=true;writes++;Interlocked.Increment(ref Writes);
        }
    }
}
