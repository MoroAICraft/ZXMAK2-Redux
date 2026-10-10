using System;
using System.IO;

namespace ZXMAK2.Hardware.Circuits.SecureDigital
{
    /// <summary>Portable SD state. Never fall back to AppData or system TEMP.</summary>
    public static class SdSessionStorage
    {
        private static string AppFolder
        {
            get { return PortablePaths.Root; }
        }

        public static string GetDirectory(string kind)
        {
            return Path.Combine(AppFolder, "Data", "SD-sessions", kind);
        }

        public static string ResolveSourcePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;
            return Path.GetFullPath(Path.IsPathRooted(path) ? path.Trim() :
                Path.Combine(AppFolder, path.Trim()));
        }

        public static string StoreSourcePath(string path)
        {
            var fullPath = ResolveSourcePath(path);
            if (fullPath.Length == 0)
                return string.Empty;
            var prefix = AppFolder.TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return fullPath.Substring(prefix.Length);
            return string.Equals(fullPath, AppFolder, StringComparison.OrdinalIgnoreCase) ?
                "." : fullPath;
        }
    }
}
