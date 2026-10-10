using System;
using System.IO;
using System.Reflection;
using File = ZXMAK2.PortableFile;
using Directory = ZXMAK2.PortableDirectory;

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
            // Native extended prefixes are IO details, never saved descriptors.
            if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) path = @"\\" + path.Substring(8);
            else if (path.StartsWith(@"\\?\", StringComparison.Ordinal)) path = path.Substring(4);
            return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(Root, path));
        }

        public static string IoPath(string path)
        {
            var full = Resolve(path);
            if (full.Length < 248) return full;
            return full.StartsWith(@"\\", StringComparison.Ordinal) ?
                @"\\?\UNC\" + full.Substring(2) : @"\\?\" + full;
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
            CopyDirectory(source, target, null);
        }

        public static void CopyDirectory(string source, string target, Action<string> progress)
        {
            source = Resolve(source);
            target = Resolve(target);
            if (source.Length > Path.GetPathRoot(source).Length) source = source.TrimEnd(Path.DirectorySeparatorChar);
            if (target.Length > Path.GetPathRoot(target).Length) target = target.TrimEnd(Path.DirectorySeparatorChar);
            if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase) ||
                target.StartsWith(source.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new IOException("The saved copy cannot be inside its source folder.");
            RequireInternal(target);
            if (progress != null) progress("Checking destination paths before copying...");
            ValidateCopyPaths(source, target);
            long files = 0, bytes = 0;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            CopyDirectoryContents(source, target, path => {
                files++; bytes += new FileInfo(IoPath(path)).Length;
                if (progress != null && (files == 1 || watch.ElapsedMilliseconds >= 200)) {
                    progress(string.Format("Copying external folder: {0:N0} files, {1:N1} MB copied", files, bytes / 1048576D));
                    watch.Restart();
                }
            });
            if (progress != null) progress(string.Format("Copied {0:N0} files. Applying SD session changes...", files));
        }

        // Fail before a multi-GB copy if the runtime cannot normalize a deep
        // destination, or a linked source would escape the mounted tree.
        private static void ValidateCopyPaths(string source, string target)
        {
            RequireInternal(target);
            if ((new DirectoryInfo(IoPath(source)).Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Cannot copy a linked media folder: " + source);
            foreach (var entry in new DirectoryInfo(IoPath(source)).GetFileSystemInfos())
            {
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Cannot copy a linked media entry: " + entry.FullName);
                var destination = Resolve(Path.Combine(target, entry.Name));
                RequireInternal(destination);
                if (entry is DirectoryInfo) ValidateCopyPaths(entry.FullName, destination);
            }
        }

        private static void CopyDirectoryContents(string source, string target, Action<string> copied)
        {
            RequireInternal(target);
            Directory.CreateDirectory(target);
            foreach (var entry in new DirectoryInfo(IoPath(source)).GetFileSystemInfos())
            {
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Cannot copy a linked media entry: " + entry.FullName);
                var destination = Path.Combine(target, entry.Name);
                if (entry is DirectoryInfo) CopyDirectoryContents(entry.FullName, destination, copied);
                else { RequireInternal(destination); File.Copy(entry.FullName, destination, false); copied(entry.FullName); }
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
                using (var stream = new FileStream(IoPath(pending), FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { write(stream); stream.Flush(true); }
                if (File.Exists(path))
                {
                    var relative = Store(path);
                    var backup = Path.Combine(DataDirectory("Backups"), relative + ".bak");
                    RequireInternal(backup);
                    Directory.CreateDirectory(Path.GetDirectoryName(backup));
                    File.Replace(pending, path, backup);
                }
                else File.Move(pending, path);
            }
            finally { if (File.Exists(pending)) File.Delete(pending); }
        }
    }

    // Narrow IO adapters used by portable storage and host-folder SD only.
    // They do not authorize writes: callers retain RequireInternal/conflict
    // checks. Extended paths avoid relying on a machine-wide Windows setting.
    public static class PortableFile
    {
        public static bool Exists(string p) { return System.IO.File.Exists(PortablePaths.IoPath(p)); }
        public static FileAttributes GetAttributes(string p) { return System.IO.File.GetAttributes(PortablePaths.IoPath(p)); }
        public static void Delete(string p) { System.IO.File.Delete(PortablePaths.IoPath(p)); }
        public static void Copy(string source, string target, bool overwrite) { System.IO.File.Copy(PortablePaths.IoPath(source), PortablePaths.IoPath(target), overwrite); }
        public static void Move(string source, string target) { System.IO.File.Move(PortablePaths.IoPath(source), PortablePaths.IoPath(target)); }
        public static void Replace(string source, string target, string backup) { System.IO.File.Replace(PortablePaths.IoPath(source), PortablePaths.IoPath(target), backup == null ? null : PortablePaths.IoPath(backup)); }
    }
    public static class PortableDirectory
    {
        public static bool Exists(string p) { return System.IO.Directory.Exists(PortablePaths.IoPath(p)); }
        public static DirectoryInfo CreateDirectory(string p) { return System.IO.Directory.CreateDirectory(PortablePaths.IoPath(p)); }
        public static void Delete(string p) { System.IO.Directory.Delete(PortablePaths.IoPath(p)); }
        public static void Delete(string p, bool recursive) { System.IO.Directory.Delete(PortablePaths.IoPath(p), recursive); }
    }
}
