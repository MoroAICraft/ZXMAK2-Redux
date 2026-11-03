// Compile together with the UNCHANGED production DirectSound.cs.
// Only the native device boundary is replaced. No hardware/Windows audio is opened.
using System;
using System.Threading;
using System.Reflection;
using System.Diagnostics;
using ZXMAK2.Host.Entities;
using ZXMAK2.Host.WinForms.Mdx;
using FakeBuffer = ZXMAK2.DirectX.DirectSound.SecondarySoundBuffer;

static class SoundFaultProbe {
    static int checks;
    static void Check(bool b,string s){checks++;if(!b)throw new Exception(s);}
    static Thread Worker(DirectSound sound){return (Thread)typeof(DirectSound).GetField("_wavePlayThread",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(sound);}
    static void Wake(DirectSound sound){((AutoResetEvent)typeof(DirectSound).GetField("_fillEvent",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(sound)).Set();}
    [STAThread] static int Main() {
        try {
            foreach(bool startup in new[]{false,true}) {
                FakeBuffer.FailWrite=startup;FakeBuffer.Writes=0;
                using(var sound=new DirectSound(null,44100,4)) {
                    var thread=Worker(sound);sound.IsSynchronized=true;
                    if(!startup){Check(SpinWait.SpinUntil(()=>FakeBuffer.Writes>=4,2000),"startup missing");FakeBuffer.FailWrite=true;Wake(sound);}
                    Check(thread.Join(2000),"failure handler did not finish");
                    FakeBuffer.FailWrite=false;int before=FakeBuffer.Writes;
                    var frame=new FrameSound(44100,new[]{new uint[882]});frame.Refresh();var info=new FrameInfo(null,0,0,44100,false);
                    for(int i=0;i<8;i++)sound.PushFrame(info,frame);
                    Wake(sound);Thread.Sleep(30);
                    Check(!thread.IsAlive&&FakeBuffer.Writes==before,"unexpected recovery");
                    Check(sound.GetLoadLevel()==1&&sound.IsSyncSupported,"did not reproduce stalled full queue");
                    var sw=Stopwatch.StartNew();for(int i=0;i<5;i++)sound.PushFrame(info,frame);sw.Stop();
                    Console.WriteLine("FINDING: native write exception ({0}) leaves worker dead, queue={1}, IsSyncSupported={2}, five pushes={3}ms; clearing device fault does not recover",startup?"startup":"playing",sound.GetLoadLevel(),sound.IsSyncSupported,sw.ElapsedMilliseconds);
                    sound.CancelWait();
                }
            }
            FakeBuffer.FailWrite=false;
            using(var sound=new DirectSound(null,44100,4)) {
                Check(SpinWait.SpinUntil(()=>FakeBuffer.Writes>=4,2000),"healthy startup missing");
                var worker=Worker(sound);sound.Dispose();Check(!worker.IsAlive,"healthy worker dispose hung");
            }
            Console.WriteLine("AUDIT COMPLETE: {0} assertions; source-level native boundary injection only",checks);return 0;
        } catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
namespace ZXMAK2.Host.WinForms.Tools { internal static class AuditNamespace {} }
namespace ZXMAK2.DirectX.Native {
    public static unsafe class NativeHelper {
        public static void INITBLK(void* p,byte v,int n){for(int i=0;i<n;i++)((byte*)p)[i]=v;}
        public static void CPBLK(void* d,void* s,int n){for(int i=0;i<n;i++)((byte*)d)[i]=((byte*)s)[i];}
    }
}
namespace ZXMAK2.DirectX.DirectSound {
    public enum DSSCL{PRIORITY} public enum DSBLOCK{None} public enum DSBPLAY_FLAGS{LOOPING}
    public enum DSBSTATUS{PLAYING=1} [Flags] public enum DSBCAPS_FLAGS{CTRLVOLUME=1,CTRLPOSITIONNOTIFY=2,GLOBALFOCUS=4,GETCURRENTPOSITION2=8}
    public class WaveFormat {public WaveFormat(int rate,short bits,short channels){}}
    public class DSBUFFERDESC {public int dwBufferBytes;public DSBCAPS_FLAGS dwFlags;public WaveFormat Format;}
    public class DSBPOSITIONNOTIFY {public int dwOffset;public WaitHandle WaitHandle;}
    public sealed class DirectSound8 : IDisposable {public void SetCooperativeLevel(IntPtr p,DSSCL s){}public void Dispose(){}}
    public sealed class SecondarySoundBuffer : IDisposable {
        public static volatile bool FailWrite;public static int Writes;static int positions;
        public SecondarySoundBuffer(DirectSound8 d,DSBUFFERDESC b){}
        public DSBSTATUS Status{get{return DSBSTATUS.PLAYING;}}public int PlayPosition{get{return (Interlocked.Increment(ref positions)%4)*3528;}}
        public void SetNotificationPositions(DSBPOSITIONNOTIFY[] p){}public void Play(int p,DSBPLAY_FLAGS f){}
        public void Stop(){} public void Dispose(){}
        public unsafe void Write(int p,void* b,int n,DSBLOCK f){if(FailWrite)throw new System.IO.IOException("Injected native buffer-lost/write failure");Interlocked.Increment(ref Writes);}
    }
}
