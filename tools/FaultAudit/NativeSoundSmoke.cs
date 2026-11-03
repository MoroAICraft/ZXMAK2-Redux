// Real default DirectSound endpoint, SILENCE ONLY, hidden owner window.
// No endpoint/device/volume settings are changed. No emulator instance started.
using System;
using System.Threading;
using System.Windows.Forms;
using ZXMAK2.Host.Entities;
using ZXMAK2.Host.WinForms.Mdx;
static class NativeSoundSmoke {
    [STAThread] static int Main() {
        try {
            using(var form=new Form())using(var sound=new DirectSound(form,44100,4)) {
                if(!SpinWait.SpinUntil(()=>sound.IsSyncSupported,5000))throw new Exception("Default DirectSound endpoint not available");
                var info=new FrameInfo(null,0,0,44100,false);var frame=new FrameSound(44100,new[]{new uint[882]});
                for(int i=0;i<30;i++){sound.PushFrame(info,frame);Thread.Sleep(20);}
                if(!sound.IsSyncSupported)throw new Exception("Output failed during silent playback");
                Console.WriteLine("PASS actual DirectSound default endpoint: initialize, silent playback, dispose; no unplug/suspend test");
            }
            return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
