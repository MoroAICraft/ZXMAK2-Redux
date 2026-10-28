using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace ZXMAK2
{
    // Host integration only. Never makes ZXMAK2 the Windows default program.
    public static class PortableLaunch
    {
        private static readonly Queue<string> Pending = new Queue<string>();
        private static readonly object Sync = new object();
        private static volatile bool stopped;
        private static NamedPipeServerStream listener;
        public static readonly string[] Extensions = { ".trd", ".scl", ".tap", ".sna" };

        public static string Identity(string path)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(
                    Path.GetFullPath(path).TrimEnd('\\').ToUpperInvariant()))).Replace("-", "").Substring(0, 24);
        }
        private static string PipeName
        { get { return "ZXMAK2.Open." + Identity(PortablePaths.Root) + "." + WindowsIdentity.GetCurrent().User.Value; } }

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(uint id, uint flags, IntPtr item1, IntPtr item2);

        public static void RegisterOpenWith()
        {
            try
            {
                using (var classes = Registry.CurrentUser.CreateSubKey(@"Software\Classes"))
                    if (RegisterAt(classes, Path.Combine(PortablePaths.Root, "ZXMAK2.exe")))
                        SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Exception ex) { Logger.Warn("Open With registration skipped: " + ex.Message); }
        }

        // Public for isolated registry testing; no global ClassesRoot writes.
        public static bool RegisterAt(RegistryKey classes, string executable)
        {
            executable = Path.GetFullPath(executable);
            var progId = "ZXMAK2.Portable." + Identity(executable);
            var command = "\"" + executable + "\" \"%1\"";
            bool changed = false;
            using (var key = classes.CreateSubKey(progId))
            {
                key.SetValue("", "ZXMAK2 BaseConf media");
                key.SetValue("FriendlyAppName", "ZXMAK2 (" + new DirectoryInfo(Path.GetDirectoryName(executable)).Name + ")");
                using (var open = key.CreateSubKey(@"shell\open\command"))
                {
                    changed = !string.Equals(open.GetValue("") as string, command, StringComparison.Ordinal);
                    if (changed) open.SetValue("", command);
                }
                using (var icon = key.CreateSubKey("DefaultIcon")) icon.SetValue("", "\"" + executable + "\",0");
            }
            foreach (var extension in Extensions)
                using (var key = classes.CreateSubKey(extension + @"\OpenWithProgids"))
                {
                    if (key.GetValue(progId) == null) { key.SetValue(progId, "", RegistryValueKind.String); changed = true; }
                }
            return changed;
        }

        public static void StartReceiver()
        {
            stopped = false;
            var thread = new Thread(Receive) { IsBackground = true, Name = "Open With receiver" };
            thread.Start();
        }
        public static void StopReceiver()
        {
            stopped = true;
            lock (Sync) { if (listener != null) listener.Dispose(); listener = null; }
        }
        public static bool TryDequeue(out string path)
        {
            lock (Sync) { path = Pending.Count == 0 ? null : Pending.Dequeue(); return path != null; }
        }

        private static byte[] ReadTimed(PipeStream pipe, int length)
        {
            var data = new byte[length];
            for (int offset = 0; offset < length; )
            {
                var read = pipe.BeginRead(data, offset, length - offset, null, null);
                int count;
                using (var wait = read.AsyncWaitHandle)
                {
                    if (!wait.WaitOne(2000)) throw new IOException("Timed out receiving Open With request.");
                    count = pipe.EndRead(read);
                }
                if (count == 0) throw new EndOfStreamException();
                offset += count;
            }
            return data;
        }
        public static void Send(string path)
        {
            var bytes = Encoding.UTF8.GetBytes(Path.GetFullPath(path));
            if (bytes.Length > 131072) throw new IOException("File path too long.");
            using (var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
            {
                pipe.Connect(2500);
                var length = BitConverter.GetBytes(bytes.Length);
                pipe.Write(length, 0, length.Length); pipe.Write(bytes, 0, bytes.Length); pipe.Flush();
                if (ReadTimed(pipe, 1)[0] != 1) throw new IOException("The running emulator cannot queue another file.");
            }
        }
        private static void Receive()
        {
            while (!stopped)
            {
                try
                {
                    var security = new PipeSecurity();
                    security.SetAccessRuleProtection(true, false);
                    security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User,
                        PipeAccessRights.FullControl, AccessControlType.Allow));
                    using (var pipe = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 4096, 4096, security))
                    {
                        lock (Sync) { if (stopped) return; listener = pipe; }
                        pipe.WaitForConnection();
                        int length = BitConverter.ToInt32(ReadTimed(pipe, 4), 0);
                        if (length <= 0 || length > 131072) throw new InvalidDataException("Invalid Open With request.");
                        var path = Encoding.UTF8.GetString(ReadTimed(pipe, length));
                        bool accepted = false;
                        lock (Sync)
                        {
                            if (Pending.Count < 32 && Path.IsPathRooted(path) && File.Exists(path))
                            { Pending.Enqueue(path); accepted = true; }
                        }
                        pipe.WriteByte(accepted ? (byte)1 : (byte)0); pipe.Flush();
                    }
                }
                catch (Exception ex) { if (!stopped) Logger.Warn("Open With receive: " + ex.Message); }
                finally { lock (Sync) listener = null; }
                if (!stopped) Thread.Sleep(25);
            }
        }
    }
}
