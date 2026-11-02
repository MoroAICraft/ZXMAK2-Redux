using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace ZXMAK2
{
    public static partial class PortableLaunch
    {
        public const string ProgId = "ZXMAK2.Portable";
        private const string Owner = "ZXMAK2-Fork.OpenWith.v2";

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(uint id, uint flags, IntPtr item1, IntPtr item2);

        public static void RegisterOpenWith()
        {
            try
            {
                using (var mutex = new Mutex(false, "Local\\ZXMAK2.OpenWith.Registry." + WindowsIdentity.GetCurrent().User.Value))
                {
                    bool held = false;
                    try
                    {
                        try { held = mutex.WaitOne(3000); } catch (AbandonedMutexException) { held = true; }
                        if (!held) throw new IOException("Another copy is updating Open With registration.");
                        using (var classes = Registry.CurrentUser.CreateSubKey(@"Software\Classes"))
                        using (var explorer = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts", true))
                            if (RegisterAt(classes, explorer, Path.Combine(PortablePaths.Root, "ZXMAK2.exe"),
                                PortablePaths.DataDirectory("Backups\\OpenWith")))
                                SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
                    }
                    finally { if (held) mutex.ReleaseMutex(); }
                }
            }
            catch (Exception ex) { Logger.Warn("Open With registration skipped: " + ex.Message); }
        }

        // All registry access is injected for tests. Tests never write real Classes/FileExts.
        public static bool RegisterAt(RegistryKey classes, string executable)
        { return RegisterAt(classes, null, executable, PortablePaths.DataDirectory("Backups\\OpenWith")); }

        public static bool RegisterAt(RegistryKey classes, RegistryKey explorer, string executable, string backups)
        {
            executable = Path.GetFullPath(executable);
            if (!string.Equals(Path.GetFileName(executable), "ZXMAK2.exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Open With requires ZXMAK2.exe.");
            string command = "\"" + executable + "\" --open-with \"%1\"";
            using (var existing = classes.OpenSubKey(ProgId))
                if (existing != null && (string)existing.GetValue("ZXMAK2.Owner") != Owner)
                    throw new InvalidDataException("The stable Open With identifier is already owned by another registration.");

            var edits = new List<RegistryEdit>();
            Add(edits, classes, ProgId, "ZXMAK2.Owner", Owner);
            Add(edits, classes, ProgId, "", "ZXMAK2 BaseConf media");
            Add(edits, classes, ProgId, "FriendlyAppName", "ZXMAK2");
            Add(edits, classes, ProgId, "NoOpenWith", null);
            Add(edits, classes, ProgId + @"\shell\open\command", "", command);
            Add(edits, classes, ProgId + @"\DefaultIcon", "", "\"" + executable + "\",0");
            foreach (string ext in Extensions) Add(edits, classes, ext + @"\OpenWithProgids", ProgId, "");

            foreach (string name in classes.GetSubKeyNames())
            {
                if (!name.StartsWith(ProgId + ".", StringComparison.Ordinal) || name.Length != ProgId.Length + 25) continue;
                string origin;
                using (var key = classes.OpenSubKey(name))
                {
                    if (key.GetValue("") as string != "ZXMAK2 BaseConf media") continue;
                    origin = key.GetValue("ZXMAK2.LegacyPath") as string;
                    if (key.GetValue("ZXMAK2.Owner") as string != Owner)
                    {
                        using (var open = key.OpenSubKey(@"shell\open\command"))
                        {
                            var old = open == null ? null : open.GetValue("") as string;
                            const string suffix = "\" \"%1\"";
                            if (old == null || old.Length <= suffix.Length + 1 ||
                                !old.StartsWith("\"", StringComparison.Ordinal) || !old.EndsWith(suffix, StringComparison.Ordinal)) continue;
                            origin = old.Substring(1, old.Length - suffix.Length - 1);
                        }
                    }
                }
                try
                {
                    if (origin == null || !Path.IsPathRooted(origin) ||
                        !string.Equals(Path.GetFileName(origin), "ZXMAK2.exe", StringComparison.OrdinalIgnoreCase) ||
                        name != ProgId + "." + Identity(origin)) continue;
                }
                catch (ArgumentException) { continue; }

                // Retain aliases for any existing UserChoice/default referring to the old ID.
                // UserChoice itself and the extension default are never read-modify-written.
                Add(edits, classes, name, "ZXMAK2.Owner", Owner);
                Add(edits, classes, name, "ZXMAK2.LegacyPath", origin);
                Add(edits, classes, name, "NoOpenWith", "");
                Add(edits, classes, name, "FriendlyAppName", "ZXMAK2");
                Add(edits, classes, name + @"\shell\open\command", "", command);
                Add(edits, classes, name + @"\DefaultIcon", "", "\"" + executable + "\",0");
                foreach (string ext in Extensions)
                {
                    Add(edits, classes, ext + @"\OpenWithProgids", name, null);
                    if (explorer != null) Add(edits, explorer, ext + @"\OpenWithProgids", name, null);
                }
            }
            if (edits.Count == 0) return false;

            // Durable value-level undo BEFORE the first mutation. Refuse migration if backup fails.
            Directory.CreateDirectory(backups);
            string backup = Path.Combine(backups, "OpenWith-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".reg");
            var undo = new StringBuilder("Windows Registry Editor Version 5.00\r\n\r\n; ZXMAK2 Open With undo. Close all ZXMAK2 copies before importing.\r\n");
            foreach (var edit in edits) edit.AppendUndo(undo);
            bool newRoot;
            using (var existing = classes.OpenSubKey(ProgId)) newRoot = existing == null;
            if (newRoot) undo.Append("\r\n[-").Append(classes.Name).Append('\\').Append(ProgId).Append("]\r\n");
            using (var file = new FileStream(backup, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                var bytes = Encoding.Unicode.GetPreamble(); file.Write(bytes, 0, bytes.Length);
                bytes = Encoding.Unicode.GetBytes(undo.ToString()); file.Write(bytes, 0, bytes.Length); file.Flush(true);
            }
            int applied = 0;
            try { foreach (var edit in edits) { edit.Apply(false); applied++; } }
            catch
            {
                for (int i = applied - 1; i >= 0; i--)
                    try { edits[i].Apply(true); } catch (Exception ex) { Logger.Warn("Open With undo failed: " + ex.Message); }
                if (newRoot)
                    try { classes.DeleteSubKeyTree(ProgId, false); } catch (Exception ex) { Logger.Warn("Open With key undo failed: " + ex.Message); }
                throw;
            }
            Logger.Info("Open With registry backup: " + backup);
            return true;
        }

        private static void Add(List<RegistryEdit> edits, RegistryKey root, string path, string name, string value)
        {
            using (var key = root.OpenSubKey(path))
            {
                var old = key == null ? null : key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                var kind = old == null ? RegistryValueKind.String : key.GetValueKind(name);
                if ((old == null && value == null) || (kind == RegistryValueKind.String && old is string && (string)old == value)) return;
                edits.Add(new RegistryEdit { Root = root, Path = path, Name = name, Before = old, Kind = kind, After = value });
            }
        }

        private sealed class RegistryEdit
        {
            public RegistryKey Root;
            public string Path, Name, After;
            public object Before;
            public RegistryValueKind Kind;
            public void Apply(bool undo)
            {
                object value = undo ? Before : After;
                using (var key = value == null ? Root.OpenSubKey(Path, true) : Root.CreateSubKey(Path))
                {
                    if (key == null) return;
                    if (value == null) key.DeleteValue(Name, false);
                    else key.SetValue(Name, value, undo ? Kind : RegistryValueKind.String);
                }
            }
            private static string Quote(string value) { return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""; }
            public void AppendUndo(StringBuilder text)
            {
                text.Append("\r\n[").Append(Root.Name).Append('\\').Append(Path).Append("]\r\n");
                text.Append(Name.Length == 0 ? "@" : Quote(Name)).Append('=');
                if (Before == null) { text.Append("-\r\n"); return; }
                byte[] bytes; int kind;
                switch (Kind)
                {
                    case RegistryValueKind.String: kind = 1; bytes = Encoding.Unicode.GetBytes((string)Before + "\0"); break;
                    case RegistryValueKind.ExpandString: kind = 2; bytes = Encoding.Unicode.GetBytes((string)Before + "\0"); break;
                    case RegistryValueKind.MultiString: kind = 7; bytes = Encoding.Unicode.GetBytes(string.Join("\0", (string[])Before) + "\0\0"); break;
                    case RegistryValueKind.DWord: kind = 4; bytes = BitConverter.GetBytes((int)Before); break;
                    case RegistryValueKind.QWord: kind = 11; bytes = BitConverter.GetBytes((long)Before); break;
                    case RegistryValueKind.None: kind = 0; bytes = (byte[])Before; break;
                    case RegistryValueKind.Binary: kind = 3; bytes = (byte[])Before; break;
                    default: throw new InvalidDataException("Unsupported registry backup value.");
                }
                text.Append("hex(").Append(kind.ToString("x")).Append("):");
                for (int i = 0; i < bytes.Length; i++)
                {
                    if (i != 0) text.Append(i % 24 == 0 ? ",\\\r\n  " : ",");
                    text.Append(bytes[i].ToString("x2"));
                }
                text.Append("\r\n");
            }
        }
    }
}
