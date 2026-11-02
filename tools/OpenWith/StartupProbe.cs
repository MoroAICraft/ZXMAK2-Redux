// Compiles the real Program.cs entry logic with a fake ILauncher, not a VM.
// Run separately for each mode because Locator.Shutdown disposes its container.
using System;
using System.IO;
using System.Reflection;
using ZXMAK2;
using ZXMAK2.Dependency;
using ZXMAK2.Host.Presentation.Interfaces;

static class StartupProbe
{
    sealed class Launcher : ILauncher
    {
        public bool Called, ExpectedRegistration;
        public string ExpectedPath;
        public void Run(string[] args)
        {
            Called = true;
            if (args.Length != 1 || args[0] != ExpectedPath) throw new Exception("Startup args lost or dispatcher flag leaked to VM");
            if ((bool)typeof(PortableLaunch).GetField("registerOnReady", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null) != ExpectedRegistration)
                throw new Exception("Registration policy changed by dispatch mode");
            // No WindowReady call: unsuccessful startup must not register with Windows.
        }
    }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            PortableBootstrap.Initialize();
            bool shell = args[0] == "shell";
            string path = Path.Combine(PortablePaths.DataDirectory("Temp"), "startup.tap");
            File.WriteAllBytes(path, new byte[] { 2, 0, 255, 255 });
            var launcher = new Launcher { ExpectedPath = path, ExpectedRegistration = !shell };
            Locator.Resolve<IResolver>().RegisterInstance<ILauncher>(null, launcher);
            var input = shell ? new[] { "--open-with", path } : new[] { path };
            typeof(Program).GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { input });
            if (!launcher.Called) throw new Exception("Local startup was incorrectly forwarded");
            if (Directory.Exists(Path.Combine(PortablePaths.Root, "Data", "Backups", "OpenWith")))
                throw new Exception("Startup before ready mutated registration");
            PortablePaths.AcquireInstance(); PortablePaths.ReleaseInstance();
            Console.WriteLine("PASS actual Program startup: " + args[0] + ", args/registration deferred/lock release"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
