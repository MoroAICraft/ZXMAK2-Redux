// Run ONLY in disposable copies. The executable name is ZXMAK2.exe so discovery
// exercises the production process-name/session protocol, not a mock transport.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using ZXMAK2;

internal static class OpenWithProbe
{
    static int checks;
    static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool pass, string message) { checks++; if (!pass) throw new Exception(message); }
    static void Fails(Action action, string message)
    { bool failed = false; try { action(); } catch { failed = true; } Check(failed, message); }
    static string Temp { get { return PortablePaths.DataDirectory("Temp"); } }
    static string FileFixture(string name) { string file = Path.Combine(Temp, name); File.WriteAllBytes(file, new byte[] { 2, 0, 255, 255 }); return file; }

    static void Set(RegistryKey root, string path, string name, object value)
    { using (var key = root.CreateSubKey(path)) key.SetValue(name, value); }
    static object Get(RegistryKey root, string path, string name)
    { using (var key = root.OpenSubKey(path)) return key == null ? null : key.GetValue(name); }
    static string Old(RegistryKey classes, string exe)
    {
        string id = PortableLaunch.ProgId + "." + PortableLaunch.Identity(exe);
        Set(classes, id, "", "ZXMAK2 BaseConf media");
        Set(classes, id, "FriendlyAppName", "ZXMAK2 (old)");
        Set(classes, id + @"\shell\open\command", "", "\"" + exe + "\" \"%1\"");
        foreach (var ext in PortableLaunch.Extensions) Set(classes, ext + @"\OpenWithProgids", id, "");
        return id;
    }
    static void RegistryChecks()
    {
        string scope = @"Software\ZXMAK2-OpenWithProbe-" + Guid.NewGuid().ToString("N");
        string backup = Path.Combine(Temp, "registry-" + Guid.NewGuid().ToString("N"));
        string a = @"D:\First copy\ZXMAK2.exe", b = @"L:\Другая папка\ZXMAK2.exe";
        try
        {
            using (var root = Registry.CurrentUser.CreateSubKey(scope))
            using (var classes = root.CreateSubKey("Classes"))
            using (var explorer = root.CreateSubKey("FileExts"))
            {
                string oldA = Old(classes, a), oldB = Old(classes, b);
                foreach (var ext in PortableLaunch.Extensions)
                {
                    Set(classes, ext, "", "Unrelated.Default");
                    Set(classes, ext + @"\OpenWithProgids", "Spectaculator.Test", "");
                    Set(explorer, ext + @"\OpenWithProgids", oldA, "");
                    Set(explorer, ext + @"\UserChoice", "ProgId", oldA);
                    Set(explorer, ext + @"\UserChoice", "Hash", "untouched");
                    Set(explorer, ext + @"\OpenWithList", "a", "Spectaculator.exe");
                }
                string fake = PortableLaunch.ProgId + ".012345678901234567890123";
                Set(classes, fake, "", "ZXMAK2 BaseConf media");
                Set(classes, fake + @"\shell\open\command", "", "\"" + a + "\" \"%1\"");
                string broken = PortableLaunch.ProgId + ".999999999999999999999999";
                Set(classes, broken, "", "ZXMAK2 BaseConf media");
                Set(classes, broken + @"\shell\open\command", "", "\" \"%1\"");
                string typed = PortableLaunch.ProgId + ".888888888888888888888888";
                Set(classes, typed, "", 123);
                using (var none = classes.OpenSubKey(@".sna\OpenWithProgids", true)) none.SetValue(oldA, new byte[0], RegistryValueKind.None);
                Check(PortableLaunch.RegisterAt(classes, explorer, a, backup), "initial migration");
                Check(Get(classes, broken, "NoOpenWith") == null, "malformed foreign command ignored");
                Check(Get(classes, typed, "").Equals(123), "foreign non-string value preserved");
                Check(!PortableLaunch.RegisterAt(classes, explorer, a, backup), "idempotence");
                Check(Directory.GetFiles(backup).Length == 1, "idempotent backup count");
                Check(Get(classes, PortableLaunch.ProgId + @"\shell\open\command", "").Equals("\"" + a + "\" --open-with \"%1\""), "quoted stable command");
                foreach (var ext in PortableLaunch.Extensions)
                {
                    Check(Get(classes, ext, "").Equals("Unrelated.Default"), "extension default intact");
                    Check(Get(classes, ext + @"\OpenWithProgids", "Spectaculator.Test") != null, "other app intact");
                    Check(Get(classes, ext + @"\OpenWithProgids", oldA) == null && Get(classes, ext + @"\OpenWithProgids", oldB) == null, "legacy references removed");
                    Check(Get(classes, ext + @"\OpenWithProgids", PortableLaunch.ProgId) != null, "one stable ref");
                    Check(Get(explorer, ext + @"\OpenWithProgids", oldA) == null, "owned Explorer ref removed");
                    Check(Get(explorer, ext + @"\UserChoice", "ProgId").Equals(oldA), "UserChoice intact");
                    Check(Get(explorer, ext + @"\UserChoice", "Hash").Equals("untouched"), "UserChoice hash intact");
                    Check(Get(explorer, ext + @"\OpenWithList", "a").Equals("Spectaculator.exe"), "MRU untouched");
                }
                Check(Get(classes, oldA, "NoOpenWith") != null, "legacy hidden");
                Check(Get(classes, fake, "NoOpenWith") == null, "lookalike not touched");
                Check(PortableLaunch.RegisterAt(classes, explorer, b, backup), "second path updates");
                Check(Get(classes, oldA + @"\shell\open\command", "").Equals("\"" + b + "\" --open-with \"%1\""), "default alias forwards to newest copy");
                Check(classes.GetSubKeyNames().Count(s => s == PortableLaunch.ProgId) == 1, "stable ID not path specific");
                string obstructed = FileFixture("backup-blocker");
                Fails(() => PortableLaunch.RegisterAt(classes, explorer, a, obstructed), "backup failure must block writes");
                Check(Get(classes, PortableLaunch.ProgId + @"\shell\open\command", "").Equals("\"" + b + "\" --open-with \"%1\""), "no mutation on failed backup");
                var first = Directory.GetFiles(backup).OrderBy(File.GetCreationTimeUtc).First();
                string reg = File.ReadAllText(first, Encoding.Unicode);
                Check(!reg.Contains("[HKEY_CURRENT_USER\\Software\\Classes"), "undo restricted to test registry");
                Check(reg.Contains(scope), "undo scope");
                using (var process = Process.Start(new ProcessStartInfo("reg.exe", "import \"" + first + "\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true }))
                { Check(process.WaitForExit(5000) && process.ExitCode == 0, "reg undo imports"); }
                Check(Get(classes, PortableLaunch.ProgId, "ZXMAK2.Owner") == null, "undo removes new stable ID");
                Check(Get(classes, oldA, "NoOpenWith") == null, "undo removes hidden flag");
                Check(Get(classes, oldA + @"\shell\open\command", "").Equals("\"" + a + "\" \"%1\""), "undo exact legacy command");
                Check(Get(classes, ".trd\\OpenWithProgids", oldA) != null, "undo restores ref");
                using (var none = classes.OpenSubKey(@".sna\OpenWithProgids")) Check(none.GetValueKind(oldA) == RegistryValueKind.None, "undo preserves REG_NONE kind");
                Check(PortableLaunch.RegisterAt(classes, explorer, b, backup), "registration after rollback");
                // Old executable starting again rewrites the old command/ref. New one migrates again.
                Old(classes, a);
                Check(PortableLaunch.RegisterAt(classes, explorer, b, backup), "legacy reappearance remigrates");
                Set(classes, PortableLaunch.ProgId, "ZXMAK2.Owner", "someone else");
                Fails(() => PortableLaunch.RegisterAt(classes, explorer, a, backup), "foreign stable key protected");
            }
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(scope, false); }
        Console.WriteLine("PASS isolated registry migration/defaults/backup import/rollback/foreign keys");
    }

    static PortableLaunch.Target Self()
    {
        var target = new PortableLaunch.Target();
        using (var process = Process.GetCurrentProcess())
            typeof(PortableLaunch.Target).GetField("Pipe", Hidden).SetValue(target,
                typeof(PortableLaunch).GetMethod("PipeName", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { process }));
        return target;
    }

    static void LocalChecks()
    {
        string file = FileFixture("Юникод проба.TAP");
        Check(PortableLaunch.ValidateShellFile(file) == file, "unicode/uppercase extension");
        Fails(() => PortableLaunch.ValidateShellFile(FileFixture("x.tzx")), "shell rejects nonregistered type");
        Fails(() => PortableLaunch.ValidateShellFile(Path.Combine(Temp, "missing.tap")), "missing file rejected");
        PortableLaunch.StartReceiver();
        try
        {
            var self = Self(); var id = Guid.NewGuid(); string got;
            PortableLaunch.Send(self, file, id); PortableLaunch.Send(self, file, id);
            Check(!PortableLaunch.TryBeginClose(), "queued request prevents silent close");
            Check(PortableLaunch.TryDequeue(out got) && got == file, "actual pipe unicode");
            Check(!PortableLaunch.TryDequeue(out got), "same request not duplicated");
            Fails(() => PortableLaunch.Send(self, FileFixture("second.tap"), id), "ID cannot change payload");
            Check(PortableLaunch.TryBeginClose(), "empty queue closes");
            Fails(() => PortableLaunch.Send(self, file, Guid.NewGuid()), "reject while closing");
            PortableLaunch.CancelClose();
            for (int i = 0; i < 32; i++) PortableLaunch.Send(self, file, Guid.NewGuid());
            Fails(() => PortableLaunch.Send(self, file, Guid.NewGuid()), "queue bound");
            int count = 0; while (PortableLaunch.TryDequeue(out got)) count++;
            Check(count == 32, "queue no data loss");
            PortableLaunch.Send(file); Check(PortableLaunch.TryDequeue(out got), "same-process compatibility");
        }
        finally { PortableLaunch.StopReceiver(); }
        PortableLaunch.StartReceiver(); PortableLaunch.Send(file); PortableLaunch.StopReceiver();
        string ignored; Check(!PortableLaunch.TryDequeue(out ignored), "restart/stop clears private queue");
        Console.WriteLine("PASS pipe framing/unicode/dedup/close-cancel/queue bounds/stop-restart");
    }

    sealed class Worker : IDisposable
    {
        public Process Process;
        public Worker(string folder)
        {
            Directory.CreateDirectory(folder);
            foreach (string name in new[] { "ZXMAK2.exe", "ZXMAK2.Logging.dll", "log4net.dll" })
                File.Copy(Path.Combine(PortablePaths.Root, name), Path.Combine(folder, name));
            Process = Process.Start(new ProcessStartInfo(Path.Combine(folder, "ZXMAK2.exe"), "server") {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 });
            Check(Read() == "READY", "worker ready");
        }
        public string Read()
        {
            var task = System.Threading.Tasks.Task.Factory.StartNew(() => Process.StandardOutput.ReadLine());
            if (!task.Wait(5000)) throw new Exception("worker timed out"); return task.Result;
        }
        public string Command(string command) { Process.StandardInput.WriteLine(command); Process.StandardInput.Flush(); return Read(); }
        public void Dispose()
        {
            if (!Process.HasExited) { Process.StandardInput.WriteLine("STOP"); Process.StandardInput.Flush(); if (!Process.WaitForExit(4000)) Process.Kill(); }
            Process.Dispose();
        }
    }
    static int Server()
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        PortableLaunch.StartReceiver(); Console.WriteLine("READY");
        try
        {
            string command;
            while ((command = Console.ReadLine()) != null && command != "STOP")
            {
                if (command == "TAKE") { string got; Console.WriteLine(PortableLaunch.TryDequeue(out got) ? got : "EMPTY"); }
                else if (command == "CLOSE") Console.WriteLine(PortableLaunch.TryBeginClose() ? "CLOSING" : "PENDING");
                else if (command == "CANCEL") { PortableLaunch.CancelClose(); Console.WriteLine("READY"); }
            }
        }
        finally { PortableLaunch.StopReceiver(); }
        return 0;
    }
    static void MultiChecks(string dispatcher)
    {
        string file = FileFixture("Маршрут файл.tap");
        Check(!PortableLaunch.Route(file, t => { throw new Exception("chooser without windows"); }), "zero windows starts registered copy");
        string parent = Path.Combine(Temp, "copies-" + Guid.NewGuid().ToString("N"));
        using (var a = new Worker(Path.Combine(parent, "A экземпляр")))
        {
            Check(PortableLaunch.Route(file, t => { throw new Exception("chooser for one window"); }), "one auto-route");
            Check(a.Command("TAKE") == file, "one got file");
            // Real product Program --open-with, with no VM/config/native files in its folder.
            using (var p = Process.Start(new ProcessStartInfo(dispatcher, "--open-with \"" + file + "\"") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = @"C:\Windows" }))
                Check(p.WaitForExit(7000) && p.ExitCode == 0, "real lightweight dispatcher exits");
            Check(a.Command("TAKE") == file, "real dispatcher delivery");
            Check(!Directory.Exists(Path.Combine(Path.GetDirectoryName(dispatcher), "Data")), "dispatcher wrote no state/logs/locks");
            using (var b = new Worker(Path.Combine(parent, "B экземпляр")))
            {
                int calls = 0;
                Check(PortableLaunch.Route(file, list => { calls++; Check(list.Count == 2, "two exact targets"); return list.Single(t => t.ProcessId == b.Process.Id); }), "two routed");
                Check(calls == 1 && b.Command("TAKE") == file && a.Command("TAKE") == "EMPTY", "selected B only");
                Check(PortableLaunch.Route(file, list => null) && a.Command("TAKE") == "EMPTY" && b.Command("TAKE") == "EMPTY", "cancel has no delivery");
                Check(b.Command("CLOSE") == "CLOSING", "B closes");
                Fails(() => PortableLaunch.Route(file, list => list.Single(t => t.ProcessId == b.Process.Id)), "closing target not redirected");
                Check(a.Command("TAKE") == "EMPTY", "no fallback to A"); b.Command("CANCEL");
                var targets = PortableLaunch.Discover();
                using (var form = new OpenWithDialog(targets))
                {
                    form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual;
                    form.Location = new Point(-20000, -20000); form.Show(); Application.DoEvents();
                    var list = (ListView)typeof(OpenWithDialog).GetField("list", Hidden).GetValue(form);
                    Check(list.Items.Count == 2 && list.Items[0].SubItems[2].Text != list.Items[1].SubItems[2].Text, "chooser paths distinguish copies");
                    using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height)); bitmap.Save(Path.Combine(Temp, "chooser.png")); }
                    list.Items[1].Selected = true;
                    var button = (Button)typeof(OpenWithDialog).GetField("open", Hidden).GetValue(form);
                    Check(button.Enabled, "chooser enables explicit selection"); button.PerformClick();
                    Check(form.Selected == targets[1] && form.DialogResult == DialogResult.OK, "chooser opens selected target");
                }
                // A client stalls mid-frame. Discovery must keep this known v2 instance
                // as unavailable, not treat it as absent and start an unrelated VM.
                var busy = targets.Single(t => t.ProcessId == a.Process.Id);
                string pipeName = (string)typeof(PortableLaunch.Target).GetField("Pipe", Hidden).GetValue(busy);
                using (var slow = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
                {
                    slow.Connect(1000); slow.WriteByte(20); slow.Flush();
                    var live = PortableLaunch.Discover();
                    Check(live.Count == 2 && live.Single(t => t.ProcessId == a.Process.Id).Unavailable, "busy instance not silently omitted");
                    Fails(() => PortableLaunch.Route(file, list => list.Single(t => t.ProcessId == a.Process.Id)), "busy cannot reroute");
                }
                Thread.Sleep(150);
                var stale = targets.Single(t => t.ProcessId == b.Process.Id);
                b.Process.Kill(); b.Process.WaitForExit();
                Fails(() => PortableLaunch.Send(stale, file, Guid.NewGuid()), "stale target does not fallback");
                Check(PortableLaunch.Discover().Count == 1, "crashed process disappears");
            }
        }
        Check(PortableLaunch.Discover().Count == 0, "no stale markers after exit");
        Console.WriteLine("PASS real multi-process routing/real EXE dispatch/cancel/closing/crash/chooser");
    }

    [STAThread] static int Main(string[] args)
    {
        try
        {
            if (args.Length != 0 && args[0] == "server") return Server();
            RegistryChecks(); LocalChecks(); MultiChecks(args[0]);
            Console.WriteLine("PASS Open With v95: " + checks + " checks"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
