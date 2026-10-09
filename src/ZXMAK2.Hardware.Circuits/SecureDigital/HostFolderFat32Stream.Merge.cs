using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace ZXMAK2.Hardware.Circuits.SecureDigital
{
    public sealed partial class HostFolderFat32Stream
    {
        private bool m_strictReads;

        private static byte[] HashFile(string path)
        {
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var hash = SHA256.Create()) return hash.ComputeHash(input);
        }

        private static void CheckOriginalFile(Node node)
        {
            var file = new FileInfo(node.FullPath);
            if (!file.Exists || file.Length != node.Length ||
                SafeDate(file.LastWriteTime) != node.LastWriteTime ||
                (file.Attributes & FileAttributes.ReparsePoint) != 0 ||
                (node.OriginalHash != null && !node.OriginalHash.SequenceEqual(HashFile(node.FullPath))))
                throw new IOException("Both the SD session and the PC changed this file: " +
                    node.FullPath + "\nNo changes were overwritten. Cancel or refresh without saving to use the PC version.");
        }

        private void CaptureOriginalForWrite(long sector)
        {
            if (sector < m_dataStartSector) return;
            var cluster = (uint)((sector - m_dataStartSector) / m_sectorsPerCluster) + 2;
            var allocation = FindAllocation(cluster);
            if (allocation == null || allocation.Node.IsDirectory) return;
            var node = allocation.Node;
            if (node.OriginalSnapshotPath != null) return;
            // Only a file that actually receives guest data writes is copied,
            // never the whole folder or the virtual 128MB..16GB image.
            CheckOriginalFile(node);
            var folder = SdSessionStorage.GetDirectory("Originals");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".bin");
            try
            {
                using (var input = new FileStream(node.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    input.CopyTo(output);
                CheckOriginalFile(node);
                node.OriginalHash = HashFile(path);
                node.OriginalSnapshotPath = path;
            }
            catch
            {
                if (File.Exists(path)) File.Delete(path);
                throw;
            }
        }

        private static void RemoveOriginalSnapshots(Node node)
        {
            foreach (var child in node.Children) RemoveOriginalSnapshots(child);
            if (node.OriginalSnapshotPath == null) return;
            try { File.Delete(node.OriginalSnapshotPath); }
            catch (IOException ex) { Logger.Warn("Cannot remove SD original snapshot: {0}", ex.Message); }
            catch (UnauthorizedAccessException ex) { Logger.Warn("Cannot remove SD original snapshot: {0}", ex.Message); }
            finally { node.OriginalSnapshotPath = null; node.OriginalHash = null; }
        }

        private static void RebaseCommittedMetadata(Node node)
        {
            foreach (var child in node.Children) RebaseCommittedMetadata(child);
            if (!node.IsDirectory && File.Exists(node.FullPath))
                node.LastWriteTime = GetLastWriteTime(new FileInfo(node.FullPath));
        }

        private void CheckCommitInputs(Dictionary<string, Node> originals,
            List<GuestEntry> changedFiles, List<string> oldFiles,
            List<string> oldDirectories, List<string> newDirectories)
        {
            if (!Directory.Exists(m_sourceFolder) ||
                (File.GetAttributes(m_sourceFolder) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("The mounted source folder is missing or unsafe.");

            var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var reads = new HashSet<Node>();
            foreach (var path in oldFiles)
            {
                var target = Path.Combine(m_sourceFolder, path);
                var replacement = changedFiles.Any(entry => string.Equals(entry.RelativePath, path, StringComparison.OrdinalIgnoreCase));
                if (replacement || File.Exists(target) || Directory.Exists(target)) reads.Add(originals[path]);
            }
            foreach (var entry in changedFiles)
            {
                var remaining = (long)entry.Length;
                foreach (var cluster in entry.Chain)
                {
                    var allocation = FindAllocation(cluster);
                    for (int s = 0; s < m_sectorsPerCluster && remaining > 0; s++)
                    {
                        var number = m_dataStartSector + (long)(cluster - 2) * m_sectorsPerCluster + s;
                        if (!m_changedSectors.ContainsKey(number) && allocation != null && !allocation.Node.IsDirectory)
                            reads.Add(allocation.Node);
                        remaining -= Math.Min(512L, remaining);
                    }
                }
            }
            foreach (var node in reads) CheckOriginalFile(node);
            foreach (var path in changedFiles.Select(entry => entry.RelativePath)
                .Concat(oldFiles).Concat(oldDirectories).Concat(newDirectories))
            {
                var parent = Path.GetDirectoryName(path);
                while (!string.IsNullOrEmpty(parent))
                {
                    directories.Add(parent);
                    parent = Path.GetDirectoryName(parent);
                }
            }
            foreach (var relative in directories.Concat(newDirectories).Concat(oldDirectories).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var path = Path.Combine(m_sourceFolder, relative);
                if (Directory.Exists(path))
                {
                    if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("A source directory became a reparse point: " + path);
                }
                else if (originals.ContainsKey(relative) && originals[relative].IsDirectory &&
                    !oldDirectories.Contains(relative, StringComparer.OrdinalIgnoreCase))
                    throw new IOException("A parent directory was removed on the PC: " + path);
            }
            foreach (var relative in oldDirectories)
            {
                var path = Path.Combine(m_sourceFolder, relative);
                if (Directory.Exists(path)) CheckForExternalAdditions(path, relative, originals);
            }
        }
    }
}
