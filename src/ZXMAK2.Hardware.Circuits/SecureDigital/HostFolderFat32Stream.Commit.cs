using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ZXMAK2.Hardware.Circuits.SecureDigital
{
    public sealed partial class HostFolderFat32Stream
    {
        private sealed class GuestEntry
        {
            public string RelativePath;
            public bool IsDirectory;
            public uint StartCluster;
            public uint Length;
            public List<uint> Chain;
        }

        private static ushort ReadWord(byte[] data, int offset)
        {
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }

        private static uint ReadDword(byte[] data, int offset)
        {
            return (uint)(data[offset] | (data[offset + 1] << 8) |
                (data[offset + 2] << 16) | (data[offset + 3] << 24));
        }

        private uint GuestFatValue(uint cluster)
        {
            var sector = new byte[SectorSize];
            ReadSector(ReservedSectors + (long)cluster / 128, sector);
            return ReadDword(sector, (int)(cluster % 128) * 4) & 0x0FFFFFFF;
        }

        private List<uint> ReadChain(uint firstCluster)
        {
            var chain = new List<uint>();
            var visited = new HashSet<uint>();
            var cluster = firstCluster;
            while (cluster >= 2 && cluster < m_clusterCount + 2)
            {
                if (!visited.Add(cluster))
                    throw new InvalidDataException("The SD card contains a cyclic FAT chain.");
                chain.Add(cluster);
                var next = GuestFatValue(cluster);
                if (next >= 0x0FFFFFF8)
                    return chain;
                if (next < 2 || next >= m_clusterCount + 2)
                    throw new InvalidDataException("The SD card contains an invalid FAT chain.");
                cluster = next;
            }
            throw new InvalidDataException("The SD card contains an invalid cluster number.");
        }

        private static string ShortDisplayName(byte[] sector, int offset)
        {
            var stem = Encoding.ASCII.GetString(sector, offset, 8).TrimEnd();
            var extension = Encoding.ASCII.GetString(sector, offset + 8, 3).TrimEnd();
            if ((sector[offset + 12] & 0x08) != 0)
                stem = stem.ToLowerInvariant();
            if ((sector[offset + 12] & 0x10) != 0)
                extension = extension.ToLowerInvariant();
            return extension.Length == 0 ? stem : stem + "." + extension;
        }

        private static string LongNamePart(byte[] sector, int offset)
        {
            var positions = new[] { 1, 3, 5, 7, 9, 14, 16, 18, 20, 22, 24, 28, 30 };
            var builder = new StringBuilder();
            foreach (var position in positions)
            {
                var value = ReadWord(sector, offset + position);
                if (value == 0 || value == 0xFFFF)
                    break;
                builder.Append((char)value);
            }
            return builder.ToString();
        }

        private static void ValidateGuestName(string name)
        {
            if (string.IsNullOrEmpty(name) || name == "." || name == ".." ||
                name.Length > 255 || name.EndsWith(" ", StringComparison.Ordinal) ||
                name.EndsWith(".", StringComparison.Ordinal) ||
                name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new InvalidDataException("The SD card contains a name Windows cannot save.");
            var stem = name.Split('.')[0].ToUpperInvariant();
            if (stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL" ||
                (stem.Length == 4 && stem.StartsWith("COM", StringComparison.Ordinal) &&
                    stem[3] >= '1' && stem[3] <= '9') ||
                (stem.Length == 4 && stem.StartsWith("LPT", StringComparison.Ordinal) &&
                    stem[3] >= '1' && stem[3] <= '9'))
                throw new InvalidDataException("The SD card contains a reserved Windows name.");
        }

        private Dictionary<string, GuestEntry> ReadGuestTree()
        {
            var baseline = new byte[SectorSize];
            var current = new byte[SectorSize];
            FillSector(0, baseline);
            ReadSector(0, current);
            if (!baseline.SequenceEqual(current))
                throw new InvalidDataException(
                    "The card boot sector changed; export the session as an image.");

            var entries = new Dictionary<string, GuestEntry>(
                StringComparer.OrdinalIgnoreCase);
            var visitedDirectories = new HashSet<uint>();
            var claimedClusters = new HashSet<uint>();
            ReadGuestDirectory(2, string.Empty, entries, visitedDirectories,
                claimedClusters);
            return entries;
        }

        private void ReadGuestDirectory(uint startCluster, string parent,
            Dictionary<string, GuestEntry> entries,
            HashSet<uint> visitedDirectories,
            HashSet<uint> claimedClusters)
        {
            if (!visitedDirectories.Add(startCluster))
                throw new InvalidDataException("The SD card contains a cyclic directory.");
            var chain = ReadChain(startCluster);
            foreach (var cluster in chain)
            {
                if (!claimedClusters.Add(cluster))
                    throw new InvalidDataException(
                        "The SD card contains shared FAT clusters.");
            }
            var longParts = new SortedDictionary<int, string>();
            foreach (var cluster in chain)
            {
                for (var sectorNumber = 0; sectorNumber < m_sectorsPerCluster; sectorNumber++)
                {
                    var sector = new byte[SectorSize];
                    ReadSector(m_dataStartSector +
                        (long)(cluster - 2) * m_sectorsPerCluster + sectorNumber, sector);
                    for (var offset = 0; offset < SectorSize; offset += 32)
                    {
                        if (sector[offset] == 0)
                            return;
                        if (sector[offset] == 0xE5)
                        {
                            longParts.Clear();
                            continue;
                        }
                        var attributes = sector[offset + 11];
                        if (attributes == 0x0F)
                        {
                            var ordinal = sector[offset] & 0x1F;
                            if (ordinal == 0 || ordinal > 20)
                                throw new InvalidDataException("Invalid FAT long filename.");
                            longParts[ordinal] = LongNamePart(sector, offset);
                            continue;
                        }
                        if ((attributes & 0x08) != 0)
                        {
                            longParts.Clear();
                            continue;
                        }
                        var name = ShortDisplayName(sector, offset);
                        if (longParts.Count > 0)
                        {
                            var text = new StringBuilder();
                            var ordinal = 1;
                            foreach (var part in longParts)
                            {
                                if (part.Key != ordinal++)
                                    throw new InvalidDataException("Incomplete FAT long filename.");
                                text.Append(part.Value);
                            }
                            name = text.ToString();
                            longParts.Clear();
                        }
                        if (name == "." || name == "..")
                            continue;
                        ValidateGuestName(name);
                        var path = parent.Length == 0 ? name :
                            Path.Combine(parent, name);
                        var entry = new GuestEntry
                        {
                            RelativePath = path,
                            IsDirectory = (attributes & 0x10) != 0,
                            StartCluster = (uint)(ReadWord(sector, offset + 20) << 16 |
                                ReadWord(sector, offset + 26)),
                            Length = ReadDword(sector, offset + 28),
                        };
                        if (entries.ContainsKey(path))
                            throw new InvalidDataException("Duplicate FAT path: " + path);
                        entries.Add(path, entry);
                        if (entry.IsDirectory)
                        {
                            ReadGuestDirectory(entry.StartCluster, path,
                                entries, visitedDirectories, claimedClusters);
                        }
                        else if (entry.Length != 0)
                        {
                            entry.Chain = ReadChain(entry.StartCluster);
                            foreach (var fileCluster in entry.Chain)
                            {
                                if (!claimedClusters.Add(fileCluster))
                                    throw new InvalidDataException(
                                        "The SD card contains shared FAT clusters.");
                            }
                            if ((long)entry.Chain.Count * m_clusterSize < entry.Length)
                                throw new InvalidDataException(
                                    "A FAT file chain is shorter than its file size: " + path);
                        }
                        else
                        {
                            entry.Chain = new List<uint>();
                        }
                    }
                }
            }
        }

        private static void AddSnapshotEntries(Node node, string parent,
            Dictionary<string, Node> entries)
        {
            foreach (var child in node.Children)
            {
                var path = parent.Length == 0 ? child.Name :
                    Path.Combine(parent, child.Name);
                entries.Add(path, child);
                if (child.IsDirectory)
                    AddSnapshotEntries(child, path, entries);
            }
        }

        private bool IsUnchangedFile(GuestEntry guest, Node original)
        {
            if (original == null || original.IsDirectory ||
                guest.Length != original.Length ||
                guest.Chain.Count != original.ClusterCount)
                return false;
            for (var index = 0; index < guest.Chain.Count; index++)
            {
                if (guest.Chain[index] != original.StartCluster + index)
                    return false;
                for (var sector = 0; sector < m_sectorsPerCluster; sector++)
                {
                    var number = m_dataStartSector +
                        (long)(guest.Chain[index] - 2) * m_sectorsPerCluster + sector;
                    byte[] changed;
                    if (m_changedSectors.TryGetValue(number, out changed))
                    {
                        var baseline = new byte[SectorSize];
                        FillSector(number, baseline);
                        var offset = (long)index * m_clusterSize + sector * SectorSize;
                        var used = (int)Math.Min(SectorSize, Math.Max(0, original.Length - offset));
                        for (int b = 0; b < used; b++)
                            if (changed[b] != baseline[b]) return false;
                    }
                }
            }
            return true;
        }

        private static void CheckForExternalAdditions(string directory,
            string relative, Dictionary<string, Node> originals)
        {
            foreach (var item in new DirectoryInfo(directory).GetFileSystemInfos())
            {
                if (IsWindowsJunk(item.Name))
                    continue;
                var path = relative.Length == 0 ? item.Name :
                    Path.Combine(relative, item.Name);
                if (!originals.ContainsKey(path))
                    throw new IOException(
                        "The mounted folder changed outside the emulator: " + path);
                if ((item.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("A source entry became a reparse point: " + path);
                if (item is DirectoryInfo)
                    CheckForExternalAdditions(item.FullName, path, originals);
            }
        }

        private void StageGuestFile(GuestEntry entry, string path)
        {
            var previousStrict = m_strictReads;
            m_strictReads = true;
            try
            {
            using (var output = new FileStream(path, FileMode.CreateNew,
                FileAccess.Write, FileShare.None))
            {
                var remaining = (long)entry.Length;
                foreach (var cluster in entry.Chain)
                {
                    for (var sectorNumber = 0;
                        sectorNumber < m_sectorsPerCluster && remaining > 0;
                        sectorNumber++)
                    {
                        var sector = new byte[SectorSize];
                        ReadSector(m_dataStartSector +
                            (long)(cluster - 2) * m_sectorsPerCluster + sectorNumber,
                            sector);
                        var take = (int)Math.Min(SectorSize, remaining);
                        output.Write(sector, 0, take);
                        remaining -= take;
                    }
                }
                if (remaining != 0)
                    throw new InvalidDataException("Incomplete FAT file: " + entry.RelativePath);
            }
            }
            finally { m_strictReads = previousStrict; }
        }

        private static int Depth(string path)
        {
            return path.Count(character => character == Path.DirectorySeparatorChar);
        }

        public void CommitChanges()
        {
            CommitChanges(null);
        }

        public bool HasFileChanges
        {
            get
            {
                if (!HasChanges) return false;
                try
                {
                    var guest = ReadGuestTree();
                    var originals = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);
                    AddSnapshotEntries(m_root, string.Empty, originals);
                    if (guest.Count != originals.Count) return true;
                    return originals.Any(item => !guest.ContainsKey(item.Key) ||
                        guest[item.Key].IsDirectory != item.Value.IsDirectory ||
                        (!item.Value.IsDirectory && !IsUnchangedFile(guest[item.Key], item.Value)));
                }
                catch (IOException) { return true; } // Unknown/damaged sessions still get a discard/cancel choice.
                catch (InvalidDataException) { return true; }
                catch (UnauthorizedAccessException) { return true; }
            }
        }

        public void CommitChanges(Action<string> afterSave)
        {
            if (HasChanges && !PortablePaths.IsInternal(m_sourceFolder))
            {
                CommitExternalFolder(afterSave);
                return;
            }
            if (!HasChanges)
            {
                if (afterSave != null) afterSave(null);
                return;
            }
            var guest = ReadGuestTree();
            var originals = new Dictionary<string, Node>(
                StringComparer.OrdinalIgnoreCase);
            AddSnapshotEntries(m_root, string.Empty, originals);

            var changedFiles = guest.Values.Where(entry => !entry.IsDirectory &&
                (!originals.ContainsKey(entry.RelativePath) ||
                 !IsUnchangedFile(entry, originals[entry.RelativePath]))).ToList();
            var oldFiles = originals.Where(item => !item.Value.IsDirectory &&
                (!guest.ContainsKey(item.Key) ||
                 guest[item.Key].IsDirectory ||
                 changedFiles.Any(entry => string.Equals(entry.RelativePath,
                     item.Key, StringComparison.OrdinalIgnoreCase))))
                .Select(item => item.Key).ToList();
            var oldDirectories = originals.Where(item => item.Value.IsDirectory &&
                (!guest.ContainsKey(item.Key) || !guest[item.Key].IsDirectory))
                .Select(item => item.Key).OrderByDescending(Depth).ToList();
            var newDirectories = guest.Values.Where(entry => entry.IsDirectory &&
                (!originals.ContainsKey(entry.RelativePath) ||
                 !originals[entry.RelativePath].IsDirectory))
                .Select(entry => entry.RelativePath).OrderBy(Depth).ToList();

            CheckCommitInputs(originals, changedFiles, oldFiles, oldDirectories, newDirectories);
            // PC and guest may have independently removed the same item.
            oldFiles = oldFiles.Where(path => File.Exists(Path.Combine(m_sourceFolder, path))).ToList();
            oldDirectories = oldDirectories.Where(path => Directory.Exists(Path.Combine(m_sourceFolder, path))).ToList();

            foreach (var entry in changedFiles)
            {
                var target = Path.Combine(m_sourceFolder, entry.RelativePath);
                if (!originals.ContainsKey(entry.RelativePath) &&
                    (File.Exists(target) || Directory.Exists(target)))
                    throw new IOException("A file was added outside the emulator: " + target);
            }
            foreach (var directory in newDirectories)
            {
                var target = Path.Combine(m_sourceFolder, directory);
                if (!originals.ContainsKey(directory) && File.Exists(target))
                    throw new IOException("A folder was added outside the emulator: " + target);
            }

            // Complete every expensive read before touching the mounted folder.
            var stage = Path.Combine(SdSessionStorage.GetDirectory("Staging"),
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            var staged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var entry in changedFiles)
                {
                    var path = Path.Combine(stage, staged.Count.ToString("D8") + ".bin");
                    StageGuestFile(entry, path);
                    staged.Add(entry.RelativePath, path);
                }

                // Files may have changed during the potentially long staging
                // pass. Recheck immediately before moving any source file.
                CheckCommitInputs(originals, changedFiles, oldFiles, oldDirectories, newDirectories);

                var backup = Path.Combine(m_sourceFolder,
                    ".zxmak2-sd-backup-" + Guid.NewGuid().ToString("N"));
                var moved = new List<string>();
                var createdFiles = new List<string>();
                var createdDirectories = new List<string>();
                var removedDirectories = new List<string>();
                var leases = new List<FileStream>();
                var backupCanBeRemoved = false;
                Directory.CreateDirectory(backup);
                try
                {
                    CloseCachedFile();
                    foreach (var path in oldFiles)
                    {
                        var target = Path.Combine(backup, path);
                        Directory.CreateDirectory(Path.GetDirectoryName(target));
                        File.Move(Path.Combine(m_sourceFolder, path), target);
                        moved.Add(path);
                        leases.Add(new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read));
                        CheckOriginalFile(originals[path], target);
                    }
                    foreach (var path in oldDirectories)
                    {
                        Directory.Delete(Path.Combine(m_sourceFolder, path));
                        removedDirectories.Add(path);
                    }
                    foreach (var path in newDirectories)
                    {
                        if (!Directory.Exists(Path.Combine(m_sourceFolder, path)))
                        {
                            Directory.CreateDirectory(Path.Combine(m_sourceFolder, path));
                            createdDirectories.Add(path);
                        }
                    }
                    foreach (var item in staged)
                    {
                        var target = Path.Combine(m_sourceFolder, item.Key);
                        var temporary = target + ".zxmak2-new-" +
                            Guid.NewGuid().ToString("N");
                        try
                        {
                            File.Copy(item.Value, temporary, false);
                            if (File.Exists(target) || Directory.Exists(target))
                                throw new IOException("Source path changed: " + target);
                            File.Move(temporary, target);
                            createdFiles.Add(item.Key);
                            leases.Add(new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read));
                            if (!HashFile(target).SequenceEqual(HashFile(item.Value)))
                                throw new IOException("A PC process replaced a file during SD save: " + target);
                        }
                        finally
                        {
                            if (File.Exists(temporary))
                                File.Delete(temporary);
                        }
                    }
                    if (afterSave != null) afterSave(backup);
                    DiscardChanges();
                    RebaseCommittedMetadata(m_root);
                    backupCanBeRemoved = true;
                }
                catch
                {
                    foreach (var lease in leases) lease.Dispose();
                    leases.Clear();
                    try
                    {
                        foreach (var path in createdFiles)
                        {
                            // Never delete a PC version that appeared during
                            // a failed transaction. Leave the rollback backup.
                            if (!HashFile(Path.Combine(m_sourceFolder, path)).SequenceEqual(HashFile(staged[path])))
                                throw new IOException("A file changed during rollback: " + path);
                            File.Delete(Path.Combine(m_sourceFolder, path));
                        }
                        foreach (var path in createdDirectories.OrderByDescending(Depth))
                            Directory.Delete(Path.Combine(m_sourceFolder, path));
                        foreach (var path in removedDirectories.OrderBy(Depth))
                            Directory.CreateDirectory(Path.Combine(m_sourceFolder, path));
                        foreach (var path in moved)
                            File.Move(Path.Combine(backup, path),
                                Path.Combine(m_sourceFolder, path));
                        backupCanBeRemoved = true;
                    }
                    catch (Exception rollbackError)
                    {
                        throw new IOException(
                            "SD save failed and automatic rollback was incomplete. " +
                            "Original files remain in " + backup, rollbackError);
                    }
                    throw;
                }
                finally
                {
                    foreach (var lease in leases) lease.Dispose();
                    if (backupCanBeRemoved)
                    {
                        try { Directory.Delete(backup, true); }
                        catch (IOException ex) { Logger.Warn("Cannot remove SD backup: {0}", ex.Message); }
                    }
                }
            }
            finally
            {
                try { Directory.Delete(stage, true); }
                catch (IOException ex) { Logger.Warn("Cannot remove SD staging files: {0}", ex.Message); }
            }
        }

        public void ExportImage(string imagePath)
        {
            var target = Path.GetFullPath(imagePath);
            PortablePaths.RequireInternal(target);
            if (target.StartsWith(m_sourceFolder.TrimEnd(
                    Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                throw new IOException(
                    "Save the SD image outside the mounted source folder.");
            // Export must never quietly substitute zero-filled sectors when
            // a PC edit made an uncaptured original unreadable/incompatible.
            var originals = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);
            AddSnapshotEntries(m_root, string.Empty, originals);
            foreach (var node in originals.Values)
            {
                if (node.IsDirectory)
                {
                    if (Directory.Exists(node.FullPath) &&
                        (File.GetAttributes(node.FullPath) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("A source directory became a reparse point: " + node.FullPath);
                }
                else if (node.OriginalSnapshotPath == null) CheckOriginalFile(node);
            }
            var previousPosition = m_position;
            var previousStrict = m_strictReads;
            var temporary = target + ".zxmak2-new-" +
                Guid.NewGuid().ToString("N");
            try
            {
                m_strictReads = true;
                using (var output = new FileStream(temporary, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None))
                {
                    m_position = 0;
                    var buffer = new byte[64 * 1024];
                    while (m_position < m_length)
                    {
                        var read = Read(buffer, 0,
                            (int)Math.Min(buffer.Length, m_length - m_position));
                        if (read == 0)
                            throw new EndOfStreamException();
                        output.Write(buffer, 0, read);
                    }
                    output.Flush();
                }
                if (File.Exists(target))
                    File.Replace(temporary, target, null);
                else
                    File.Move(temporary, target);
            }
            finally
            {
                m_position = previousPosition;
                m_strictReads = previousStrict;
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }

        private void RebaseSourcePaths(Node node, string previous, string target)
        {
            node.FullPath = target + node.FullPath.Substring(previous.Length);
            foreach (var child in node.Children) RebaseSourcePaths(child, previous, target);
        }

        private void CommitExternalFolder(Action<string> afterSave)
        {
            // Copy only on explicit Save. Start with the CURRENT PC tree, then
            // apply the existing selective, conflict-checked guest transaction.
            var previous = m_sourceFolder;
            var target = PortablePaths.NewSavePath(previous, "SD-Folders");
            PortablePaths.CopyDirectory(previous, target);
            CloseCachedFile();
            RebaseSourcePaths(m_root, previous, target);
            m_sourceFolder = target;
            try
            {
                CommitChanges(afterSave);
                Logger.Info("External SD folder saved locally: {0}", target);
            }
            catch
            {
                CloseCachedFile();
                RebaseSourcePaths(m_root, target, previous);
                m_sourceFolder = previous;
                Logger.Warn("External SD save failed; source/session retained. Unused local copy: {0}", target);
                throw;
            }
        }
    }
}
