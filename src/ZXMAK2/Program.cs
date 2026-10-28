using System;
using ZXMAK2.Dependency;
using ZXMAK2.Host.Presentation.Interfaces;


namespace ZXMAK2
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            try { PortableBootstrap.Initialize(); Run(args); }
            catch (Exception ex) { PortableBootstrap.ShowError(ex.Message); }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void Run(string[] args)
        {
            try { PortablePaths.AcquireInstance(); }
            catch (System.IO.IOException)
            {
                if (args.Length == 1 && System.IO.File.Exists(args[0]))
                { PortableLaunch.Send(args[0]); return; }
                throw;
            }
            Logger.Start();
            try
            {
                PortableLaunch.RegisterOpenWith();
                PortableLaunch.StartReceiver();
                AppDomain.CurrentDomain.UnhandledException +=
                    (s, e) => Logger.Fatal(e.ExceptionObject as Exception, "AppDomain.UnhandledException");
                //AppDomain.CurrentDomain.FirstChanceException += 
                //    (s, e) => Logger.Error(e.Exception, "AppDomain.FirstChanceException");
                RunSafe(args);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                PortableBootstrap.ShowError(ex.Message);
            }
            finally
            {
                PortableLaunch.StopReceiver();
                Logger.Finish();
                PortablePaths.ReleaseInstance();
            }
        }

        private static void RunSafe(string[] args)
        {
            var launcher = Locator.Resolve<ILauncher>();
            launcher.Run(args);
            Locator.Shutdown();
        }
    }
}
