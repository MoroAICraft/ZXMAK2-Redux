// Real v96 main view/VM/DirectSound/D3D, unmodified packaged DLLs.
// Launcher wrapper omits shell registration; telemetry only, no UI input injection.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using System.ComponentModel;
using Microsoft.Win32;
using ZXMAK2;
using ZXMAK2.Dependency;
using ZXMAK2.Engine;
using ZXMAK2.Host.Interfaces;
using ZXMAK2.Host.Presentation.Interfaces;
using ZXMAK2.Host.WinForms.Views;
using ZXMAK2.Host.WinForms.Mdx;
using ZXMAK2.Host.WinForms.Controls;

static class HardwareSessionProbe {
    static readonly object gate=new object();
    static string log;static long frames,presents;
    static VirtualMachine observed;static IAllocatorPresenter allocator;
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    static object Field(object o,string name){return o==null?null:o.GetType().GetField(name,F).GetValue(o);}
    static void Log(string text){lock(gate)File.AppendAllText(log,DateTime.UtcNow.ToString("o")+" "+text+Environment.NewLine);}
    [STAThread] static int Main() {
        PortableBootstrap.Initialize();return Run();
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static int Run() {
        // Initialize WinForms defaults before telemetry creates any window handles.
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(MainView).TypeHandle);
        log=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Data","hardware-session.txt");Directory.CreateDirectory(Path.GetDirectoryName(log));
        Logger.Start();Log("START no shell registration; no user media; PID="+System.Diagnostics.Process.GetCurrentProcess().Id);
        foreach(var screen in Screen.AllScreens)Log("SCREEN "+screen.DeviceName+" bounds="+screen.Bounds+" primary="+screen.Primary);
        SystemEvents.PowerModeChanged+=(s,e)=>Log("POWER "+e.Mode);
        SystemEvents.DisplaySettingsChanged+=(s,e)=>Log("DISPLAY settings changed");
        SystemEvents.SessionSwitch+=(s,e)=>Log("SESSION "+e.Reason);
        using(var timer=new System.Windows.Forms.Timer {Interval=1000}) {
            timer.Tick+=(s,e)=> {
                try {
                    var view=Application.OpenForms.OfType<MainView>().FirstOrDefault();if(view==null)return;
                    var vm=(VirtualMachine)Field(view.DataContext,"m_vm");
                    if(vm!=null&&!Object.ReferenceEquals(observed,vm)){observed=vm;vm.Spectrum.BusManager.FrameReady+=()=>Interlocked.Increment(ref frames);}
                    if(allocator==null){var video=(RenderVideo)Field(view,"renderVideo");allocator=(IAllocatorPresenter)Field(video,"_allocator");allocator.PresentCompleted+=(a,b)=>Interlocked.Increment(ref presents);}
                    var sound=(DirectSound)Field(view.Host,"m_sound");var thread=(Thread)Field(sound,"_wavePlayThread");
                    Log("TICK frames="+Interlocked.Read(ref frames)+" presents="+Interlocked.Read(ref presents)+
                        " running="+(vm!=null&&vm.IsRunning)+" sound="+(sound!=null&&sound.IsSyncSupported)+
                        " worker="+(thread!=null&&thread.IsAlive)+" queue="+(sound==null?-1:sound.GetLoadLevel())+
                        " sync="+view.Host.SyncSource+" rendering="+allocator.IsRendering+" videoError="+allocator.ErrorMessage+
                        " window="+view.WindowState+" bounds="+view.Bounds+" screen="+Screen.FromControl(view).DeviceName);
                }catch(Exception ex){Log("TELEMETRY ERROR "+ex);}
            };
            timer.Start();
            try {Locator.Resolve<ILauncher>().Run(new string[0]);Locator.Shutdown();Log("CLEAN EXIT");return 0;}
            catch(Exception ex){Log("FATAL "+ex);return 1;}
            finally {timer.Stop();Logger.Finish();}
        }
    }
}
