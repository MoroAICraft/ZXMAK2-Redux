using System;
using System.IO;
using System.Reflection;

namespace ZXMAK2
{
    /// <summary>Host filesystem policy only. No emulated hardware semantics.</summary>
    public static class PortablePaths
    {
        private static FileStream s_instance;
        public static void AcquireInstance()
        {
            var path = Path.Combine(DataDirectory("State"), "instance.lock");
            s_instance = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }

        public static void ReleaseInstance()
        {
            if (s_instance != null) { s_instance.Dispose(); s_instance = null; }
        }

        public static string Root
        {
            get
            {
                var entry = Assembly.GetEntryAssembly();
                return Path.GetFullPath(entry == null ? AppDomain.CurrentDomain.BaseDirectory :
                    Path.GetDirectoryName(entry.Location));
            }
        }

        public static string Resolve(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(Root, path));
        }

        public static bool IsInternal(string path)
        {
            var full = Resolve(path);
            if (full.Length == 0) return false;
            var root = Root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.Equals(full, root, StringComparison.OrdinalIgnoreCase) &&
                !full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return false;
            // An internal junction/symlink must not redirect own writes outside.
            var cursor = full;
            while (!string.Equals(cursor, root, StringComparison.OrdinalIgnoreCase))
            {
                if ((File.Exists(cursor) || Directory.Exists(cursor)) &&
                    (File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0) return false;
                cursor = Path.GetDirectoryName(cursor);
                if (cursor == null) return false;
            }
            return true;
        }

        public static string Store(string path)
        {
            var full = Resolve(path);
            if (full.Length == 0) return string.Empty;
            if (!IsInternal(full)) return full;
            return string.Equals(full, Root, StringComparison.OrdinalIgnoreCase) ? "." :
                full.Substring(Root.TrimEnd(Path.DirectorySeparatorChar).Length + 1);
        }

        public static string ConfigFile(string name)
        {
            var configured = Path.Combine(Root, "Config", name);
            // Flat developer/legacy runtime remains readable during migration.
            var path = Directory.Exists(Path.Combine(Root, "Config")) ? configured : Path.Combine(Root, name);
            RequireInternal(path);
            return path;
        }

        public static string DataDirectory(string relative)
        {
            var path = Path.GetFullPath(Path.Combine(Root, "Data", relative));
            RequireInternal(path);
            Directory.CreateDirectory(path);
            return path;
        }

        public static void RequireInternal(string path)
        {
            if (!IsInternal(path)) throw new IOException("Save inside the emulator folder: " + path);
        }

        public static string NewSavePath(string source, string category)
        {
            var name = Path.GetFileName(source);
            if (string.IsNullOrEmpty(name)) name = "untitled";
            return Path.Combine(DataDirectory(Path.Combine("Saves", category,
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8))), name);
        }

        public static void CopyDirectory(string source, string target)
        {
            RequireInternal(target);
            Directory.CreateDirectory(target);
            foreach (var entry in new DirectoryInfo(source).GetFileSystemInfos())
            {
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Cannot copy a linked media entry: " + entry.FullName);
                var destination = Path.Combine(target, entry.Name);
                if (entry is DirectoryInfo) CopyDirectory(entry.FullName, destination);
                else { RequireInternal(destination); File.Copy(entry.FullName, destination, false); }
            }
        }

        public static string FindRom(string name)
        {
            foreach (var folder in new[] { Path.Combine(Root, "Resources", "ROMs"), Root, Path.Combine(Root, "roms") })
            {
                var path = Path.GetFullPath(Path.Combine(folder, name));
                RequireInternal(path);
                if (File.Exists(path)) return path;
            }
            return string.Empty;
        }

        public static void WriteAtomic(string path, Action<Stream> write)
        {
            path = Resolve(path);
            RequireInternal(path);
            Directory.CreateDirectory(Path.GetDirectoryName(Resolve(path)));
            var pending = Resolve(path) + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { write(stream); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(pending, path, path + ".bak");
                else File.Move(pending, path);
            }
            finally { if (File.Exists(pending)) File.Delete(pending); }
        }
    }
}
