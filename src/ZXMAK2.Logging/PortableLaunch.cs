using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace ZXMAK2
{
    // Host integration only; never touches a VM, its configuration or its media.
    public static partial class PortableLaunch
    {
        public sealed class Target
        {
            public string Folder { get; internal set; }
            public string Title { get; internal set; }
            public int ProcessId { get; internal set; }
            public bool Closing { get; internal set; }
            public bool Unavailable { get; internal set; }
            internal string Pipe;
        }

        private const string Protocol = "ZXMAK2-OPEN-2\n";
        private const int MaxMessage = 131072;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private static readonly Queue<string> Pending = new Queue<string>();
        private static readonly Dictionary<Guid, string> Accepted = new Dictionary<Guid, string>();
        private static readonly Queue<Guid> AcceptedOrder = new Queue<Guid>();
        private static readonly object Sync = new object();
        private static bool stopped = true, closing, shellSession, registerOnReady;
        private static Thread receiver;
        private static NamedPipeServerStream listener;
        private static EventWaitHandle liveMarker;
        public static readonly string[] Extensions = { ".trd", ".scl", ".tap", ".sna" };

        public static string Identity(string path)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Utf8.GetBytes(
                    Path.GetFullPath(path).TrimEnd('\\').ToUpperInvariant()))).Replace("-", "").Substring(0, 24);
        }

        private static string PipeName(Process process)
        {
            // Start time prevents PID reuse; SID + session excludes other users/desktops.
            return "ZXMAK2.Open.v2." + WindowsIdentity.GetCurrent().User.Value + "." +
                process.SessionId + "." + process.Id + "." + process.StartTime.ToUniversalTime().Ticks;
        }

        public static void BeginSession(bool updateRegistration)
        { shellSession = true; registerOnReady = updateRegistration; }

        public static void WindowReady()
        {
            if (!shellSession) return; // Embedded/test views must not register associations.
            try { StartReceiver(); }
            catch (Exception ex) { Logger.Warn("Open With receiver unavailable: " + ex.Message); return; }
            if (registerOnReady) { registerOnReady = false; RegisterOpenWith(); }
        }

        public static void StartReceiver()
        {
            lock (Sync)
            {
                if (receiver != null) return;
                using (var process = Process.GetCurrentProcess())
                {
                    var security = new EventWaitHandleSecurity();
                    security.SetAccessRuleProtection(true, false);
                    security.AddAccessRule(new EventWaitHandleAccessRule(WindowsIdentity.GetCurrent().User,
                        EventWaitHandleRights.FullControl, AccessControlType.Allow));
                    bool created;
                    liveMarker = new EventWaitHandle(true, EventResetMode.ManualReset,
                        "Local\\" + PipeName(process), out created, security);
                }
                stopped = false; closing = false;
                receiver = new Thread(Receive) { IsBackground = true, Name = "Open With receiver" };
                receiver.Start();
            }
        }

        public static void StopReceiver()
        {
            Thread thread;
            lock (Sync)
            {
                stopped = true; closing = true; thread = receiver;
                if (listener != null) listener.Dispose();
            }
            // Worker needs Sync in finally: never join while holding the lock.
            if (thread != null && !thread.Join(3000))
            { Logger.Warn("Open With receiver is still stopping."); return; }
            lock (Sync)
            {
                receiver = null; listener = null;
                if (liveMarker != null) { liveMarker.Dispose(); liveMarker = null; }
                Pending.Clear(); Accepted.Clear(); AcceptedOrder.Clear();
            }
        }

        public static bool TryBeginClose()
        {
            lock (Sync)
            {
                if (Pending.Count != 0) return false;
                closing = true;
                return true;
            }
        }

        public static void CancelClose() { lock (Sync) closing = false; }
        public static bool TryDequeue(out string path)
        {
            lock (Sync) { path = Pending.Count == 0 ? null : Pending.Dequeue(); return path != null; }
        }

        public static string ValidateShellFile(string path)
        {
            path = Path.GetFullPath(path);
            if (!File.Exists(path)) throw new FileNotFoundException("The file no longer exists.", path);
            bool supported = false;
            foreach (var ext in Extensions)
                if (string.Equals(Path.GetExtension(path), ext, StringComparison.OrdinalIgnoreCase)) supported = true;
            if (!supported) throw new InvalidDataException("Open With supports TRD, SCL, TAP and SNA files.");
            return path;
        }

        // Only protocol-aware live processes are eligible. No stale lock/registry process lists.
        public static List<Target> Discover()
        {
            var result = new List<Target>();
            using (var current = Process.GetCurrentProcess())
            {
                var processes = Process.GetProcessesByName(current.ProcessName);
                var budget = Stopwatch.StartNew();
                foreach (var process in processes)
                using (process)
                {
                    if (budget.ElapsedMilliseconds > 5000)
                        throw new TimeoutException("Could not finish checking the running ZXMAK2 windows. No destination was selected.");
                    try
                    {
                        if (process.Id == current.Id || process.SessionId != current.SessionId) continue;
                        string pipe = PipeName(process);
                        // A live marker distinguishes old executables from a busy/new receiver.
                        // Otherwise a transient pipe timeout could incorrectly start a new VM.
                        try { using (EventWaitHandle.OpenExisting("Local\\" + pipe, EventWaitHandleRights.Synchronize)) { } }
                        catch (WaitHandleCannotBeOpenedException) { continue; }
                        string response;
                        try { response = Exchange(pipe, "PROBE", 350); }
                        catch (Exception ex)
                        {
                            if (!(ex is IOException || ex is TimeoutException || ex is UnauthorizedAccessException)) throw;
                            string unavailableFolder = "";
                            try { unavailableFolder = Path.GetDirectoryName(process.MainModule.FileName); } catch (System.ComponentModel.Win32Exception) { }
                            result.Add(new Target { Folder = unavailableFolder, Title = "Not responding", ProcessId = process.Id,
                                Unavailable = true, Pipe = pipe });
                            continue;
                        }
                        if (!response.StartsWith("READY\n", StringComparison.Ordinal) &&
                            !response.StartsWith("CLOSING\n", StringComparison.Ordinal)) continue;
                        var folder = response.Substring(response.IndexOf('\n') + 1);
                        if (!Path.IsPathRooted(folder)) continue;
                        result.Add(new Target { Folder = folder, Title = process.MainWindowTitle,
                            ProcessId = process.Id, Closing = response.StartsWith("CLOSING", StringComparison.Ordinal), Pipe = pipe });
                    }
                    catch (Exception ex)
                    {
                        if (!(ex is IOException || ex is TimeoutException || ex is UnauthorizedAccessException ||
                              ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException || ex is ArgumentException)) throw;
                    }
                }
            }
            result.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Folder, b.Folder));
            return result;
        }

        // False: start this registered copy. True: delivered or explicitly cancelled.
        public static bool Route(string path, Func<IList<Target>, Target> choose)
        {
            path = ValidateShellFile(path);
            var targets = Discover();
            if (targets.Count == 0) return false;
            var target = targets.Count == 1 ? targets[0] : choose(targets);
            if (target == null) return true;
            if (!targets.Contains(target)) throw new InvalidOperationException("Invalid destination.");
            Send(target, path, Guid.NewGuid());
            return true;
        }

        public static void Send(Target target, string path, Guid requestId)
        {
            path = Path.GetFullPath(path);
            if (target.Unavailable) throw new IOException("The selected ZXMAK2 is not responding. No new machine was started.");
            if (target.Closing) throw new IOException("The selected ZXMAK2 window is closing. Please try again.");
            var reply = Exchange(target.Pipe, "OPEN\n" + requestId.ToString("D") + "\n" + path, 2500);
            if (reply != "QUEUED") throw new IOException("The selected ZXMAK2 cannot accept the file: " + reply);
            // ACK means queued, not loaded. Target applies its normal media/machine policy.
            // No automatic retry/fallback after a lost ACK: never open in a different VM.
        }

        // A direct command-line launch of an already running same folder remains local.
        public static void Send(string path)
        {
            bool local;
            lock (Sync) local = receiver != null && !stopped;
            if (local)
            {
                using (var process = Process.GetCurrentProcess())
                    Send(new Target { Folder = PortablePaths.Root, Pipe = PipeName(process) }, path, Guid.NewGuid());
                return;
            }
            var watch = Stopwatch.StartNew();
            do
            {
                foreach (var target in Discover())
                    if (string.Equals(target.Folder, PortablePaths.Root, StringComparison.OrdinalIgnoreCase))
                    { Send(target, path, Guid.NewGuid()); return; }
                Thread.Sleep(100); // bounded startup race, not a permanent polling service
            } while (watch.ElapsedMilliseconds < 5000);
            throw new IOException("This folder is in use, but its ZXMAK2 window is not ready or uses an older Open With protocol.");
        }

        private static byte[] ReadTimed(PipeStream pipe, int length, Stopwatch watch, int timeout)
        {
            var data = new byte[length];
            for (int offset = 0; offset < length; )
            {
                var read = pipe.BeginRead(data, offset, length - offset, null, null);
                int count;
                using (var wait = read.AsyncWaitHandle)
                {
                    if (!wait.WaitOne(Math.Max(0, timeout - (int)watch.ElapsedMilliseconds)))
                        throw new IOException("Timed out communicating with the selected ZXMAK2. Delivery was not confirmed; do not retry blindly.");
                    count = pipe.EndRead(read);
                }
                if (count == 0) throw new EndOfStreamException();
                offset += count;
            }
            return data;
        }

        private static string ReadMessage(PipeStream pipe, Stopwatch watch, int timeout)
        {
            int size = BitConverter.ToInt32(ReadTimed(pipe, 4, watch, timeout), 0);
            if (size <= 0 || size > MaxMessage) throw new InvalidDataException("Invalid Open With message size.");
            string text = Utf8.GetString(ReadTimed(pipe, size, watch, timeout));
            if (!text.StartsWith(Protocol, StringComparison.Ordinal)) throw new InvalidDataException("Unknown Open With protocol.");
            return text.Substring(Protocol.Length);
        }

        private static void WriteMessage(PipeStream pipe, string text, Stopwatch watch, int timeout)
        {
            var bytes = Utf8.GetBytes(Protocol + text);
            if (bytes.Length > MaxMessage) throw new InvalidDataException("Open With message too long.");
            var packet = new byte[bytes.Length + 4];
            BitConverter.GetBytes(bytes.Length).CopyTo(packet, 0); bytes.CopyTo(packet, 4);
            var write = pipe.BeginWrite(packet, 0, packet.Length, null, null);
            using (var wait = write.AsyncWaitHandle)
            {
                if (!wait.WaitOne(Math.Max(0, timeout - (int)watch.ElapsedMilliseconds)))
                    throw new IOException("Timed out sending Open With request; delivery was not confirmed.");
                pipe.EndWrite(write);
            }
        }

        private static string Exchange(string name, string request, int timeout)
        {
            var watch = Stopwatch.StartNew();
            using (var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous,
                TokenImpersonationLevel.Identification))
            {
                pipe.Connect(timeout);
                WriteMessage(pipe, request, watch, timeout);
                return ReadMessage(pipe, watch, timeout);
            }
        }

        private static string Handle(string request)
        {
            lock (Sync)
            {
                if (request == "PROBE") return (closing || stopped ? "CLOSING\n" : "READY\n") + PortablePaths.Root;
                var parts = request.Split(new[] { '\n' }, 3);
                Guid id;
                if (parts.Length != 3 || parts[0] != "OPEN" || !Guid.TryParse(parts[1], out id) || id == Guid.Empty)
                    return "INVALID REQUEST";
                string previous;
                if (Accepted.TryGetValue(id, out previous))
                    return string.Equals(previous, parts[2], StringComparison.Ordinal) ? "QUEUED" : "REQUEST ID REUSED";
                if (closing || stopped) return "WINDOW CLOSING";
                if (Pending.Count >= 32) return "QUEUE FULL";
                // No disk/network IO under Sync: an unavailable UNC source must not
                // block closing the emulator. The normal UI loader validates contents.
                if (!Path.IsPathRooted(parts[2])) return "ABSOLUTE FILE PATH REQUIRED";
                Pending.Enqueue(parts[2]);
                Accepted.Add(id, parts[2]); AcceptedOrder.Enqueue(id);
                if (AcceptedOrder.Count > 256) Accepted.Remove(AcceptedOrder.Dequeue());
                return "QUEUED";
            }
        }

        private static void Receive()
        {
            string name;
            using (var process = Process.GetCurrentProcess()) name = PipeName(process);
            while (true)
            {
                lock (Sync) if (stopped) return;
                try
                {
                    var security = new PipeSecurity();
                    security.SetAccessRuleProtection(true, false);
                    security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User,
                        PipeAccessRights.FullControl, AccessControlType.Allow));
                    using (var pipe = new NamedPipeServerStream(name, PipeDirection.InOut, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 4096, 4096, security))
                    {
                        lock (Sync) { if (stopped) return; listener = pipe; }
                        pipe.WaitForConnection();
                        var watch = Stopwatch.StartNew();
                        string reply = Handle(ReadMessage(pipe, watch, 2000));
                        WriteMessage(pipe, reply, watch, 2000);
                    }
                }
                catch (Exception ex)
                {
                    lock (Sync) { if (stopped) return; Logger.Warn("Open With receive: " + ex.Message); }
                    Thread.Sleep(50); // Malformed clients must not turn this worker into a busy loop.
                }
                finally { lock (Sync) listener = null; }
            }
        }
    }
}
