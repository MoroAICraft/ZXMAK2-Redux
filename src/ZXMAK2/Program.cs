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
            bool shellOpen = args.Length > 0 && args[0] == "--open-with";
            if (shellOpen)
            {
                try
                {
                    if (args.Length != 2) throw new ArgumentException("Expected one file after --open-with.");
                    string path = PortableLaunch.ValidateShellFile(args[1]);
                    if (PortableLaunch.Route(path, OpenWithDialog.Choose)) return;
                    args = new[] { path }; // No running copy: normal startup of this registered EXE.
                }
                catch (Exception ex) { Environment.ExitCode = 1; OpenWithDialog.Error(ex); return; }
            }
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
                PortableLaunch.BeginSession(!shellOpen);
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
