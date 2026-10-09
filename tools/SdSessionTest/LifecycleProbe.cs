using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using ZXMAK2.Dependency;
using ZXMAK2.Engine;
using ZXMAK2.Hardware.Evo;
using ZXMAK2.Hardware.Circuits.SecureDigital;
using ZXMAK2.Host.Entities;
using ZXMAK2.Host.Interfaces;
using ZXMAK2.Host.Presentation;

// Headless lifecycle regression. No Windows UI automation or real user media.
internal static class LifecycleProbe
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private sealed class Query : IUserQuery
    {
        public DlgResult Next;
        public int Calls;
        public Action BeforeShow;
        public DlgResult Show(string message, string caption, DlgButtonSet buttons, DlgIcon icon)
        { Calls++; if (BeforeShow != null) BeforeShow(); return Next; }
        public object ObjectSelector(object[] values, string caption) { throw new NotSupportedException(); }
        public bool QueryText(string caption, string text, ref string value) { throw new NotSupportedException(); }
        public bool QueryValue(string caption, string text, string format, ref int value, int min, int max)
        { throw new NotSupportedException(); }
    }
    private sealed class Messages : IUserMessage
    {
        public int Errors;
        public void ErrorDetails(Exception ex) { Errors++; }
        public void Error(Exception ex) { Errors++; }
        public void Error(string fmt, params object[] args) { Errors++; }
        public void Warning(Exception ex) { throw ex; }
        public void Warning(string fmt, params object[] args) { throw new Exception(fmt); }
        public void Info(string fmt, params object[] args) { }
    }
    private sealed class Host : IHostService
    {
        public IHostKeyboard Keyboard { get { return null; } }
        public IHostMouse Mouse { get { return null; } }
        public IHostJoystick Joystick { get { return null; } }
        public SyncSource SyncSource { get; set; }
        public int SampleRate { get { return 44100; } }
        public bool IsCaptured { get { return false; } }
        public IMediaRecorder MediaRecorder { get; set; }
        public bool CheckSyncSourceSupported(SyncSource value) { return true; }
        public void PushFrame(IFrameInfo info, IFrameVideo video, IFrameSound sound) { Thread.Sleep(1); }
        public void CancelPush() { }
        public void Capture() { }
        public void Uncapture() { }
        public void Dispose() { }
    }
    private static void Assert(bool result, string text)
    { checks++; if (!result) throw new Exception(text); }
    private static SdCard Card(ZsdPentEvo device)
    { return (SdCard)typeof(ZsdPentEvo).GetField("card", Private).GetValue(device); }
    private static byte[] Sector(Stream stream, long index)
    {
        stream.Position = index * 512;
        var data = new byte[512];
        if (stream.Read(data, 0, data.Length) != data.Length) throw new EndOfStreamException();
        return data;
    }
    private static long FileSector(Stream stream)
    {
        var boot = Sector(stream, 0);
        var start = BitConverter.ToUInt16(boot, 14) + (long)boot[16] * BitConverter.ToUInt32(boot, 36);
        var dir = Sector(stream, start);
        for (int i = 0; i < dir.Length; i += 32)
        {
            if (System.Text.Encoding.ASCII.GetString(dir, i, 11) != "TEST    TRD") continue;
            uint cluster = (uint)(BitConverter.ToUInt16(dir, i + 20) << 16) | BitConverter.ToUInt16(dir, i + 26);
            return start + (cluster - 2) * boot[13];
        }
        throw new Exception("TEST.TRD missing");
    }
    private static void WriteGuest(ZsdPentEvo device, byte value)
    {
        var stream = Card(device).HostFolderStream;
        var index = FileSector(stream);
        var sector = Sector(stream, index);
        sector[0] = value;
        stream.Position = index * 512;
        stream.Write(sector, 0, sector.Length);
    }
    private static int Main(string[] args)
    {
        if (args.Length == 3 && args[0] == "export")
        {
            using (var source = new HostFolderFat32Stream(args[1])) source.ExportImage(args[2]);
            return 0;
        }
        if (args.Length == 1 && args[0] == "relocated")
        {
            var config = new System.Xml.XmlDocument();
            config.LoadXml("<Device sdSource='HostFolder' sdPath='source' />");
            var relocated = new ZsdPentEvo();
            relocated.LoadConfigXml(config.DocumentElement);
            typeof(ZsdPentEvo).GetMethod("RestoreConfiguredCard", Private).Invoke(relocated, null);
            string expected = Path.Combine(Path.GetDirectoryName(typeof(SdSessionStorage).Assembly.Location), "source");
            Assert(relocated.IsMediaMounted && relocated.ConfiguredSourcePath == expected,
                "copied runtime did not rebase its internal SD folder");
            WriteGuest(relocated, (byte)'P');
            var journal = (string)typeof(HostFolderFat32Stream).GetField("m_journalPath", Private)
                .GetValue(Card(relocated).HostFolderStream);
            Assert(journal.StartsWith(SdSessionStorage.GetDirectory("HostFolders") + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase), "copied runtime journal escaped new directory");
            Card(relocated).HostFolderStream.DiscardChanges(); relocated.BusDisconnect();
            Console.WriteLine("Copied runtime + changed CWD + internal source/journal: PASS");
            return 0;
        }
        string scratch = Path.Combine(Path.GetDirectoryName(typeof(SdSessionStorage).Assembly.Location),
            "sd-lifecycle-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        var device = new ZsdPentEvo();
        try
        {
            var query = new Query();
            Locator.Resolve<IResolver>().RegisterInstance<IUserQuery>(null, query);
            string folder = Path.Combine(scratch, "source"); Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, "TEST.TRD"); File.WriteAllText(file, "ORIGINAL");
            string export = Path.Combine(scratch, "export.img");
            device.MountHostFolder(folder);
            Card(device).HostFolderStream.ExportImage(export);
            Assert(!device.HasPendingChanges && File.Exists(export), "clean folder export failed/marked dirty");
            WriteGuest(device, (byte)'S');
            Card(device).HostFolderStream.ExportImage(export);
            using (var image = File.OpenRead(export))
                Assert(Sector(image, FileSector(image))[0] == (byte)'S', "image export lost pending guest writes");
            Assert(device.HasPendingChanges && File.ReadAllText(file) == "ORIGINAL",
                "export committed folder or cleared pending changes");
            query.Next = DlgResult.Cancel;
            Assert(!device.EjectCard() && device.IsMediaMounted && device.HasPendingChanges,
                "cancel eject lost mounted changes");
            Assert(device.MountHostFolder(folder) == -1 && device.HasPendingChanges,
                "cancel refresh lost mounted changes");
            Assert(!device.MountCard(export) && Card(device).HostFolderStream != null,
                "cancel replacement lost source");
            query.Next = DlgResult.Yes;
            Assert(device.EjectCard() && File.ReadAllBytes(file)[0] == (byte)'S', "eject/save failed");
            device.MountHostFolder(folder);
            WriteGuest(device, (byte)'D'); query.Next = DlgResult.No;
            Assert(device.MountHostFolder(folder) >= 0 && !device.HasPendingChanges &&
                File.ReadAllBytes(file)[0] == (byte)'S', "refresh/discard touched PC file");
            int before = query.Calls;
            File.WriteAllText(Path.Combine(folder, "pc.txt"), "NEW");
            Assert(device.MountHostFolder(folder) >= 0 && query.Calls == before,
                "PC-only changes triggered a save prompt");
            WriteGuest(device, (byte)'I'); query.Next = DlgResult.Yes;
            Assert(device.MountCard(export) && Card(device).ImageSessionStream != null &&
                File.ReadAllBytes(file)[0] == (byte)'I', "replacement/save failed");
            var imageSession = Card(device).ImageSessionStream;
            imageSession.Position = 3; imageSession.WriteByte(0x78);
            query.Next = DlgResult.Cancel;
            Assert(!device.EjectCard() && device.HasPendingChanges, "image cancel lost changes");
            query.Next = DlgResult.No; device.EjectCard();
            Assert(File.ReadAllBytes(file)[0] == (byte)'I', "image discard affected original folder");
            device.BusDisconnect();

            // A second process loads assemblies from an actually copied runtime,
            // not a synthetic substitute for its application directory.
            string copied = Path.Combine(scratch, "copied-runtime"); Directory.CreateDirectory(copied);
            string originalRuntime = Path.GetDirectoryName(typeof(SdSessionStorage).Assembly.Location);
            foreach (string runtimeFile in Directory.GetFiles(originalRuntime))
                File.Copy(runtimeFile, Path.Combine(copied, Path.GetFileName(runtimeFile)));
            Directory.CreateDirectory(Path.Combine(copied, "source"));
            File.WriteAllText(Path.Combine(copied, "source", "TEST.TRD"), "PORTABLE");
            var start = new System.Diagnostics.ProcessStartInfo(Path.Combine(copied, "SdLifecycleProbe.exe"), "relocated");
            start.UseShellExecute = false; start.CreateNoWindow = true;
            start.RedirectStandardOutput = true; start.RedirectStandardError = true;
            start.WorkingDirectory = scratch;
            using (var child = System.Diagnostics.Process.Start(start))
            {
                string output = child.StandardOutput.ReadToEnd(); string error = child.StandardError.ReadToEnd();
                child.WaitForExit(); Console.Write(output);
                Assert(child.ExitCode == 0, "copied runtime failed: " + error);
            }

            using (var vm = new VirtualMachine(new Host(), null))
            {
                vm.Spectrum.BusManager.Init(vm.Spectrum, true);
                device = vm.Spectrum.BusManager.FindDevice<ZsdPentEvo>();
                if (device == null)
                {
                    vm.Spectrum.BusManager.Disconnect();
                    device = new ZsdPentEvo(); vm.Spectrum.BusManager.Add(device);
                    vm.Spectrum.BusManager.Connect();
                }
                device.MountHostFolder(folder);
                var model = (MainViewModel)FormatterServices.GetUninitializedObject(typeof(MainViewModel));
                typeof(MainViewModel).GetField("m_vm", Private).SetValue(model, vm);
                var messages = new Messages();
                typeof(MainViewModel).GetField("m_userMessage", Private).SetValue(model, messages);
                query.BeforeShow = delegate { Assert(!vm.IsRunning, "guest was running during save dialog"); };
                WriteGuest(device, (byte)'C'); vm.DoRun(); query.Next = DlgResult.Cancel;
                Assert(!model.PrepareClose() && vm.IsRunning && device.HasPendingChanges,
                    "cancel close did not resume/preserve session");
                // An unrelated PC addition is not a conflict. A real collision
                // is a PC edit to the same TEST.TRD that the guest changed.
                File.WriteAllText(file, "PC CONFLICT"); query.Next = DlgResult.Yes;
                Assert(!model.PrepareClose() && vm.IsRunning && device.HasPendingChanges && messages.Errors == 1,
                    "failed save did not abort close/resume/preserve session");
                query.Next = DlgResult.No;
                Assert(model.PrepareClose() && !vm.IsRunning && !device.HasPendingChanges,
                    "accepted close resumed guest or retained dirty state");
                device.MountHostFolder(folder); WriteGuest(device, (byte)'V'); vm.DoRun(); query.Next = DlgResult.Yes;
                Assert(model.PrepareClose() && !vm.IsRunning && File.ReadAllBytes(file)[0] == (byte)'V',
                    "close/save failed or resumed guest after save");
                vm.DoRun(); before = query.Calls;
                Assert(model.PrepareClose() && !vm.IsRunning && query.Calls == before,
                    "clean close prompted or left guest running");
            }
            Console.WriteLine("SD lifecycle/confirmations/export/close: PASS ({0} assertions)", checks);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            device.BusDisconnect();
            if (Directory.Exists(scratch)) Directory.Delete(scratch, true);
        }
    }
}
