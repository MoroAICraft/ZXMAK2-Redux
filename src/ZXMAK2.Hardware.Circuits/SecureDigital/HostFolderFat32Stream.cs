using System;
using System.Collections.Generic;
using System.IO;
using System.Text;


namespace ZXMAK2.Hardware.Circuits.SecureDigital
{
    /// <summary>
    /// Sector-addressable FAT32 snapshot backed by a host folder.
    /// Directory metadata is captured at mount time; file bytes are read from
    /// the original files only when their virtual sectors are requested.
    /// </summary>
    public sealed partial class HostFolderFat32Stream : Stream
    {
        private const int SectorSize = 512;
        private const int ReservedSectors = 32;
        private const int FatCount = 2;
        private const long Fat32FileLimit = 0x100000000L;
        // Leave FAT/directory headroom when the file payload itself is close
        // to the requested 8 GiB collection size.
        private const long MaximumImageLength = 16L * 1024L * 1024L * 1024L;
        private const uint EndOfChain = 0x0FFFFFFF;

        private readonly string m_sourceFolder;
        private readonly List<string> m_warnings = new List<string>();
        private readonly HashSet<string> m_reportedReadErrors =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Allocation> m_allocations = new List<Allocation>();
        private readonly Node m_root;
        private readonly int m_sectorsPerCluster;
        private readonly int m_clusterSize;
        private readonly uint m_clusterCount;
        private readonly uint m_sectorsPerFat;
        private readonly long m_dataStartSector;
        private readonly long m_length;
        private long m_position;
        private string m_cachedFilePath;
        private FileStream m_cachedFile;

        public HostFolderFat32Stream(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                throw new ArgumentException("A host folder path is required.", "folderPath");
            }

            m_sourceFolder = Path.GetFullPath(folderPath.Trim());
            var rootInfo = new DirectoryInfo(m_sourceFolder);
            if (!rootInfo.Exists)
            {
                throw new DirectoryNotFoundException(
                    "The host folder was not found: " + m_sourceFolder);
            }
            if ((rootInfo.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("The root host folder cannot be a reparse point.");
            }

            m_root = ScanDirectory(rootInfo, null, true);
            m_sectorsPerCluster = SelectSectorsPerCluster(m_root);
            m_clusterSize = m_sectorsPerCluster * SectorSize;

            AssignShortNames(m_root);
            CalculateClusterRequirements(m_root);

            uint nextCluster = 2;
            AllocateTree(m_root, ref nextCluster, true);
            var usedClusters = nextCluster - 2;
            var reserveClusters = Math.Max(128U, usedClusters / 100U);
            var minimumFat32Clusters = 65525U;
            m_clusterCount = Math.Max(minimumFat32Clusters, usedClusters + reserveClusters);
            m_sectorsPerFat = (uint)(((ulong)(m_clusterCount + 2) * 4 +
                SectorSize - 1) / SectorSize);
            m_dataStartSector = ReservedSectors + (long)FatCount * m_sectorsPerFat;
            var totalSectors = m_dataStartSector +
                (long)m_clusterCount * m_sectorsPerCluster;
            m_length = totalSectors * SectorSize;
            if (m_length > MaximumImageLength)
            {
                throw new NotSupportedException(
                    "The selected folder does not fit in the maximum 16 GiB virtual SD card.");
            }

            BuildDirectoryData(m_root);
            InitializeJournal();
        }

        public string SourceFolder
        {
            get { return m_sourceFolder; }
        }

        public IList<string> Warnings
        {
            get { return m_warnings.AsReadOnly(); }
        }

        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return true; } }
        public override bool CanWrite { get { return true; } }
        public override long Length { get { return m_length; } }

        public override long Position
        {
            get { return m_position; }
            set
            {
                if (value < 0 || value > m_length)
                    throw new ArgumentOutOfRangeException("value");
                m_position = value;
            }
        }

        public override void Flush()
        {
            if (m_journal != null)
                m_journal.Flush();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CloseJournal();
                CloseCachedFile();
            }
            base.Dispose(disposing);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");
            if (offset < 0 || count < 0 || offset > buffer.Length - count)
                throw new ArgumentOutOfRangeException();
            if (count == 0 || m_position >= m_length)
                return 0;

            var remaining = (int)Math.Min((long)count, m_length - m_position);
            var completed = 0;
            var sector = new byte[SectorSize];
            while (completed < remaining)
            {
                var sectorIndex = m_position / SectorSize;
                var withinSector = (int)(m_position % SectorSize);
                ReadSector(sectorIndex, sector);
                var take = Math.Min(
                    remaining - completed,
                    SectorSize - withinSector);
                Buffer.BlockCopy(sector, withinSector, buffer, offset + completed, take);
                completed += take;
                m_position += take;
            }
            return completed;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long next;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    next = offset;
                    break;
                case SeekOrigin.Current:
                    next = m_position + offset;
                    break;
                case SeekOrigin.End:
                    next = m_length + offset;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("origin");
            }
            Position = next;
            return m_position;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException("The virtual SD card has a fixed capacity.");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");
            if (offset < 0 || count < 0 || offset > buffer.Length - count)
                throw new ArgumentOutOfRangeException();
            if (m_position > m_length - count)
                throw new IOException("Write past the end of the virtual SD card.");
            var sector = new byte[SectorSize];
            while (count > 0)
            {
                var index = m_position / SectorSize;
                var within = (int)(m_position % SectorSize);
                var take = Math.Min(count, SectorSize - within);
                ReadSector(index, sector);
                var changed = false;
                for (var byteIndex = 0; byteIndex < take; byteIndex++)
                {
                    if (sector[within + byteIndex] != buffer[offset + byteIndex])
                    {
                        changed = true;
                        break;
                    }
                }
                if (changed)
                {
                    Buffer.BlockCopy(buffer, offset, sector, within, take);
                    AppendJournalSector(index, sector);
                    m_changedSectors[index] = (byte[])sector.Clone();
                }
                m_position += take;
                offset += take;
                count -= take;
            }
        }

        private Node ScanDirectory(
            DirectoryInfo directory,
            Node parent,
            bool isRoot)
        {
            var node = new Node();
            node.IsDirectory = true;
            node.Parent = parent;
            node.Name = isRoot ? string.Empty : directory.Name;
            node.FullPath = directory.FullName;
            node.LastWriteTime = GetLastWriteTime(directory);
            node.CreationTime = GetCreationTime(directory);

            FileSystemInfo[] entries;
            try
            {
                entries = directory.GetFileSystemInfos();
            }
            catch (Exception ex)
            {
                if (isRoot)
                    throw new IOException("Cannot scan the selected host folder.", ex);
                AddWarning("Skipped inaccessible folder: " + directory.FullName);
                return null;
            }

            Array.Sort(entries, delegate(FileSystemInfo left, FileSystemInfo right)
            {
                var compare = StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
                return compare != 0 ? compare : StringComparer.Ordinal.Compare(left.Name, right.Name);
            });

            foreach (var entry in entries)
            {
                if (IsWindowsJunk(entry.Name))
                    continue;
                FileAttributes attributes;
                try
                {
                    attributes = entry.Attributes;
                }
                catch (Exception)
                {
                    AddWarning("Skipped inaccessible item: " + entry.FullName);
                    continue;
                }
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    AddWarning("Skipped reparse point: " + entry.FullName);
                    continue;
                }
                if (entry.Name.Length > 255)
                {
                    AddWarning("Skipped name longer than 255 characters: " + entry.FullName);
                    continue;
                }

                var childDirectory = entry as DirectoryInfo;
                if (childDirectory != null)
                {
                    var childNode = ScanDirectory(childDirectory, node, false);
                    if (childNode != null)
                        node.Children.Add(childNode);
                    continue;
                }

                var file = entry as FileInfo;
                if (file == null)
                {
                    AddWarning("Skipped unsupported filesystem object: " + entry.FullName);
                    continue;
                }
                long length;
                try
                {
                    length = file.Length;
                }
                catch (Exception)
                {
                    AddWarning("Skipped inaccessible file: " + file.FullName);
                    continue;
                }
                if (length >= Fat32FileLimit)
                {
                    AddWarning("Skipped file larger than the FAT32 limit: " + file.FullName);
                    continue;
                }

                var child = new Node();
                child.Parent = node;
                child.Name = file.Name;
                child.FullPath = file.FullName;
                child.Length = length;
                child.LastWriteTime = GetLastWriteTime(file);
                child.CreationTime = GetCreationTime(file);
                node.Children.Add(child);
            }
            return node;
        }

        private int SelectSectorsPerCluster(Node root)
        {
            var totalFileBytes = SumFileBytes(root);
            var choices = new[] { 8, 16, 32, 64 };
            foreach (var choice in choices)
            {
                var clusterSize = (long)choice * SectorSize;
                var estimatedClusters = EstimateUsedClusters(root, clusterSize);
                var reserve = Math.Max(128L, estimatedClusters / 100L);
                var clusterCount = Math.Max(65525L, estimatedClusters + reserve);
                var fatSectors = ((clusterCount + 2) * 4 + SectorSize - 1) / SectorSize;
                var length = (ReservedSectors + FatCount * fatSectors +
                    clusterCount * choice) * SectorSize;
                if (length <= MaximumImageLength && totalFileBytes <= MaximumImageLength)
                    return choice;
            }
            throw new NotSupportedException(
                "The selected folder does not fit in the maximum 16 GiB virtual SD card.");
        }

        private static DateTime GetLastWriteTime(FileSystemInfo item)
        {
            try
            {
                return SafeDate(item.LastWriteTime);
            }
            catch
            {
                return new DateTime(1980, 1, 1);
            }
        }

        private static DateTime GetCreationTime(FileSystemInfo item)
        {
            try
            {
                return SafeDate(item.CreationTime);
            }
            catch
            {
                return new DateTime(1980, 1, 1);
            }
        }

        private static long SumFileBytes(Node node)
        {
            long total = node.IsDirectory ? 0 : node.Length;
            foreach (var child in node.Children)
                total += SumFileBytes(child);
            return total;
        }

        private static long EstimateUsedClusters(Node node, long clusterSize)
        {
            if (!node.IsDirectory)
                return node.Length == 0 ? 0 : (node.Length + clusterSize - 1) / clusterSize;
            long entryCount = node.Parent == null ? 1 : 3;
            foreach (var child in node.Children)
            {
                entryCount += 1 + GetLfnEntryCount(child.Name);
            }
            var directoryBytes = entryCount * 32;
            var total = Math.Max(1L, (directoryBytes + clusterSize - 1) / clusterSize);
            foreach (var child in node.Children)
                total += EstimateUsedClusters(child, clusterSize);
            return total;
        }

        private void AssignShortNames(Node directory)
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var child in directory.Children)
            {
                child.ShortName = CreateShortName(child.Name, used);
                child.NeedsLongName = NeedsLongName(child.Name, child.ShortName);
            }
            foreach (var child in directory.Children)
            {
                if (child.IsDirectory)
                    AssignShortNames(child);
            }
        }

        private static byte[] CreateShortName(string name, HashSet<string> used)
        {
            var stem = Path.GetFileNameWithoutExtension(name) ?? string.Empty;
            var extension = Path.GetExtension(name) ?? string.Empty;
            if (extension.StartsWith(".", StringComparison.Ordinal))
                extension = extension.Substring(1);
            var cleanStem = CleanShortPart(stem);
            var cleanExtension = CleanShortPart(extension);
            if (cleanStem.Length == 0)
                cleanStem = "FILE";

            if (cleanStem.Length <= 8 && cleanExtension.Length <= 3 &&
                IsSimpleShortName(name, cleanStem, cleanExtension))
            {
                var direct = PackShortName(cleanStem, cleanExtension);
                var directKey = Encoding.ASCII.GetString(direct);
                if (used.Add(directKey))
                    return direct;
            }

            for (var number = 1; number < 1000000; number++)
            {
                var suffix = "~" + number.ToString();
                var prefixLength = Math.Max(1, 8 - suffix.Length);
                var aliasStem = cleanStem.Substring(0, Math.Min(prefixLength, cleanStem.Length)) + suffix;
                var aliasExtension = cleanExtension.Substring(0, Math.Min(3, cleanExtension.Length));
                var packed = PackShortName(aliasStem, aliasExtension);
                var key = Encoding.ASCII.GetString(packed);
                if (used.Add(key))
                    return packed;
            }
            throw new IOException("Cannot create a unique FAT short name for " + name);
        }

        private static bool IsSimpleShortName(
            string original,
            string stem,
            string extension)
        {
            var expected = extension.Length == 0 ? stem : stem + "." + extension;
            // FAT short names are uppercase on disk even when Windows stores
            // the same 8.3 name in lowercase. Do not turn bin/autoexec.bat or
            // wizcfg.com into BIN~1/AUTOEX~1.BAT/WIZCFG~1.COM.
            return string.Equals(original, expected, StringComparison.OrdinalIgnoreCase);
        }

        private static string CleanShortPart(string value)
        {
            var result = new StringBuilder();
            foreach (var character in value.ToUpperInvariant())
            {
                if ((character >= 'A' && character <= 'Z') ||
                    (character >= '0' && character <= '9') ||
                    character == '$' || character == '%' || character == '\'' ||
                    character == '-' || character == '_' || character == '@' ||
                    character == '~' || character == '`' || character == '!' ||
                    character == '(' || character == ')' || character == '{' ||
                    character == '}' || character == '^' || character == '#' ||
                    character == '&')
                {
                    result.Append(character);
                }
                else if (character != ' ' && character != '.')
                {
                    result.Append('_');
                }
            }
            return result.ToString();
        }

        private static byte[] PackShortName(string stem, string extension)
        {
            var result = new byte[11];
            for (var index = 0; index < result.Length; index++)
                result[index] = 0x20;
            Encoding.ASCII.GetBytes(stem, 0, Math.Min(8, stem.Length), result, 0);
            Encoding.ASCII.GetBytes(extension, 0, Math.Min(3, extension.Length), result, 8);
            return result;
        }

        private static bool NeedsLongName(string original, byte[] shortName)
        {
            var stem = Encoding.ASCII.GetString(shortName, 0, 8).TrimEnd();
            var extension = Encoding.ASCII.GetString(shortName, 8, 3).TrimEnd();
            var display = extension.Length == 0 ? stem : stem + "." + extension;
            return !string.Equals(original, display, StringComparison.Ordinal);
        }

        private void CalculateClusterRequirements(Node node)
        {
            if (node.IsDirectory)
            {
                var entryCount = node.Parent == null ? 1 : 3;
                foreach (var child in node.Children)
                    entryCount += 1 + (child.NeedsLongName ? GetLfnEntryCount(child.Name) : 0);
                node.DirectoryLength = checked((int)(entryCount * 32));
                node.ClusterCount = Math.Max(1U,
                    (uint)((node.DirectoryLength + m_clusterSize - 1) / m_clusterSize));
                foreach (var child in node.Children)
                    CalculateClusterRequirements(child);
            }
            else
            {
                node.ClusterCount = node.Length == 0 ? 0U :
                    (uint)((node.Length + m_clusterSize - 1) / m_clusterSize);
            }
        }

        private void AllocateTree(Node node, ref uint nextCluster, bool directories)
        {
            if (node.IsDirectory == directories && node.ClusterCount > 0)
            {
                node.StartCluster = nextCluster;
                m_allocations.Add(new Allocation(node, nextCluster, node.ClusterCount));
                nextCluster += node.ClusterCount;
            }
            foreach (var child in node.Children)
                AllocateTree(child, ref nextCluster, directories);
            if (node.Parent == null && directories)
                AllocateTree(node, ref nextCluster, false);
        }

        private void BuildDirectoryData(Node node)
        {
            if (!node.IsDirectory)
                return;
            node.DirectoryData = new byte[checked((int)(node.ClusterCount * m_clusterSize))];
            var offset = 0;
            if (node.Parent != null)
            {
                WriteDotEntry(node.DirectoryData, ref offset, ".", node.StartCluster);
                WriteDotEntry(
                    node.DirectoryData,
                    ref offset,
                    "..",
                    node.Parent.Parent == null ? node.Parent.StartCluster : node.Parent.StartCluster);
            }
            foreach (var child in node.Children)
            {
                if (child.NeedsLongName)
                    WriteLongNameEntries(node.DirectoryData, ref offset, child.Name, child.ShortName);
                WriteShortEntry(node.DirectoryData, ref offset, child);
            }
            foreach (var child in node.Children)
                BuildDirectoryData(child);
        }

        private static void WriteDotEntry(
            byte[] data,
            ref int offset,
            string name,
            uint cluster)
        {
            var packed = new byte[11];
            for (var index = 0; index < packed.Length; index++)
                packed[index] = 0x20;
            for (var index = 0; index < name.Length; index++)
                packed[index] = (byte)name[index];
            Buffer.BlockCopy(packed, 0, data, offset, 11);
            data[offset + 11] = 0x10;
            WriteUInt16(data, offset + 20, (ushort)(cluster >> 16));
            WriteUInt16(data, offset + 26, (ushort)cluster);
            offset += 32;
        }

        private static void WriteShortEntry(byte[] data, ref int offset, Node node)
        {
            Buffer.BlockCopy(node.ShortName, 0, data, offset, 11);
            data[offset + 11] = node.IsDirectory ? (byte)0x10 : (byte)0x21;
            WriteFatDateTime(data, offset, node.CreationTime, 14, 16);
            WriteFatDateTime(data, offset, node.LastWriteTime, 22, 24);
            WriteUInt16(data, offset + 20, (ushort)(node.StartCluster >> 16));
            WriteUInt16(data, offset + 26, (ushort)node.StartCluster);
            WriteUInt32(data, offset + 28, node.IsDirectory ? 0U : (uint)node.Length);
            offset += 32;
        }

        private static void WriteLongNameEntries(
            byte[] data,
            ref int offset,
            string name,
            byte[] shortName)
        {
            var count = GetLfnEntryCount(name);
            var checksum = ShortNameChecksum(shortName);
            for (var sequence = count; sequence >= 1; sequence--)
            {
                var entryOffset = offset;
                data[entryOffset] = (byte)sequence;
                if (sequence == count)
                    data[entryOffset] |= 0x40;
                data[entryOffset + 11] = 0x0F;
                data[entryOffset + 12] = 0;
                data[entryOffset + 13] = checksum;
                WriteUInt16(data, entryOffset + 26, 0);
                var nameStart = (sequence - 1) * 13;
                var positions = new[] { 1, 3, 5, 7, 9, 14, 16, 18, 20, 22, 24, 28, 30 };
                for (var index = 0; index < positions.Length; index++)
                {
                    var characterIndex = nameStart + index;
                    ushort value;
                    if (characterIndex < name.Length)
                        value = name[characterIndex];
                    else if (characterIndex == name.Length)
                        value = 0;
                    else
                        value = 0xFFFF;
                    WriteUInt16(data, entryOffset + positions[index], value);
                }
                offset += 32;
            }
        }

        private void FillSector(long sectorIndex, byte[] sector)
        {
            Array.Clear(sector, 0, sector.Length);
            if (sectorIndex == 0 || sectorIndex == 6)
            {
                FillBootSector(sector);
                return;
            }
            if (sectorIndex == 1 || sectorIndex == 7)
            {
                FillFsInfoSector(sector);
                return;
            }
            if (sectorIndex >= ReservedSectors &&
                sectorIndex < ReservedSectors + (long)FatCount * m_sectorsPerFat)
            {
                FillFatSector(
                    (uint)((sectorIndex - ReservedSectors) % m_sectorsPerFat),
                    sector);
                return;
            }
            if (sectorIndex < m_dataStartSector)
                return;

            var relativeSector = sectorIndex - m_dataStartSector;
            var cluster = (uint)(relativeSector / m_sectorsPerCluster) + 2;
            if (cluster >= m_clusterCount + 2)
                return;
            var allocation = FindAllocation(cluster);
            if (allocation == null)
                return;
            var clusterOffset = (long)(cluster - allocation.StartCluster) * m_clusterSize;
            var withinCluster = (relativeSector % m_sectorsPerCluster) * SectorSize;
            var sourceOffset = clusterOffset + withinCluster;
            if (allocation.Node.IsDirectory)
            {
                if (sourceOffset < allocation.Node.DirectoryData.Length)
                {
                    Buffer.BlockCopy(
                        allocation.Node.DirectoryData,
                        (int)sourceOffset,
                        sector,
                        0,
                        Math.Min(SectorSize,
                            allocation.Node.DirectoryData.Length - (int)sourceOffset));
                }
                return;
            }
            ReadHostFileSector(allocation.Node, sourceOffset, sector);
        }

        private void FillBootSector(byte[] sector)
        {
            sector[0] = 0xEB;
            sector[1] = 0x58;
            sector[2] = 0x90;
            WriteAscii(sector, 3, 8, "ZXMAK2  ");
            WriteUInt16(sector, 11, SectorSize);
            sector[13] = (byte)m_sectorsPerCluster;
            WriteUInt16(sector, 14, ReservedSectors);
            sector[16] = FatCount;
            WriteUInt16(sector, 17, 0);
            WriteUInt16(sector, 19, 0);
            sector[21] = 0xF8;
            WriteUInt16(sector, 22, 0);
            WriteUInt16(sector, 24, 63);
            WriteUInt16(sector, 26, 255);
            WriteUInt32(sector, 28, 0);
            WriteUInt32(sector, 32, (uint)(m_length / SectorSize));
            WriteUInt32(sector, 36, m_sectorsPerFat);
            WriteUInt16(sector, 40, 0);
            WriteUInt16(sector, 42, 0);
            WriteUInt32(sector, 44, 2);
            WriteUInt16(sector, 48, 1);
            WriteUInt16(sector, 50, 6);
            sector[64] = 0x80;
            sector[66] = 0x29;
            WriteUInt32(sector, 67, 0x5A584D32);
            WriteAscii(sector, 71, 11, "ZX HOST SD ");
            WriteAscii(sector, 82, 8, "FAT32   ");
            sector[510] = 0x55;
            sector[511] = 0xAA;
        }

        private void FillFsInfoSector(byte[] sector)
        {
            WriteUInt32(sector, 0, 0x41615252);
            WriteUInt32(sector, 484, 0x61417272);
            var used = m_allocations.Count == 0 ? 0U :
                m_allocations[m_allocations.Count - 1].EndCluster - 1;
            WriteUInt32(sector, 488, m_clusterCount > used ? m_clusterCount - used : 0);
            WriteUInt32(sector, 492, used + 2 < m_clusterCount + 2 ? used + 2 : 0xFFFFFFFF);
            WriteUInt32(sector, 508, 0xAA550000);
        }

        private void FillFatSector(uint fatSector, byte[] sector)
        {
            var firstEntry = fatSector * (SectorSize / 4U);
            for (var index = 0U; index < SectorSize / 4U; index++)
            {
                var cluster = firstEntry + index;
                uint value;
                if (cluster == 0)
                    value = 0x0FFFFFF8;
                else if (cluster == 1)
                    value = EndOfChain;
                else
                    value = GetFatValue(cluster);
                WriteUInt32(sector, (int)(index * 4), value);
            }
        }

        private uint GetFatValue(uint cluster)
        {
            var allocation = FindAllocation(cluster);
            if (allocation == null)
                return 0;
            return cluster == allocation.EndCluster ? EndOfChain : cluster + 1;
        }

        private Allocation FindAllocation(uint cluster)
        {
            var low = 0;
            var high = m_allocations.Count - 1;
            while (low <= high)
            {
                var middle = low + ((high - low) / 2);
                var allocation = m_allocations[middle];
                if (cluster < allocation.StartCluster)
                    high = middle - 1;
                else if (cluster > allocation.EndCluster)
                    low = middle + 1;
                else
                    return allocation;
            }
            return null;
        }

        private void ReadHostFileSector(Node node, long fileOffset, byte[] sector)
        {
            if (fileOffset >= node.Length)
                return;
            try
            {
                if (!File.Exists(node.FullPath))
                    throw new FileNotFoundException(
                        "The host file disappeared after the folder was mounted.",
                        node.FullPath);
                var stream = GetCachedFile(node.FullPath);
                if (stream.Length != node.Length)
                    throw new IOException("The file size changed after the folder was mounted.");
                stream.Position = fileOffset;
                var wanted = (int)Math.Min(SectorSize, node.Length - fileOffset);
                var offset = 0;
                while (offset < wanted)
                {
                    var read = stream.Read(sector, offset, wanted - offset);
                    if (read == 0)
                        throw new EndOfStreamException("The host file ended unexpectedly.");
                    offset += read;
                }
            }
            catch (Exception ex)
            {
                CloseCachedFile();
                lock (m_reportedReadErrors)
                {
                    if (m_reportedReadErrors.Add(node.FullPath))
                    {
                        Logger.Error(
                            ex,
                            "Host Folder SD cannot read '{0}'. Zero-filled sectors will be returned until refresh.",
                            node.FullPath);
                    }
                }
                Array.Clear(sector, 0, sector.Length);
            }
        }

        private FileStream GetCachedFile(string path)
        {
            if (m_cachedFile != null && string.Equals(
                    m_cachedFilePath, path, StringComparison.OrdinalIgnoreCase))
            {
                return m_cachedFile;
            }
            CloseCachedFile();
            m_cachedFile = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                4096,
                FileOptions.RandomAccess);
            m_cachedFilePath = path;
            return m_cachedFile;
        }

        private void CloseCachedFile()
        {
            var stream = m_cachedFile;
            m_cachedFile = null;
            m_cachedFilePath = null;
            if (stream != null)
                stream.Dispose();
        }

        private void AddWarning(string warning)
        {
            m_warnings.Add(warning);
            Logger.Warn("Host Folder SD: {0}", warning);
        }

        private static bool IsWindowsJunk(string name)
        {
            return string.Equals(name, "desktop.ini", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "Thumbs.db", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "System Volume Information", StringComparison.OrdinalIgnoreCase);
        }

        private static DateTime SafeDate(DateTime value)
        {
            if (value.Year < 1980)
                return new DateTime(1980, 1, 1);
            if (value.Year > 2107)
                return new DateTime(2107, 12, 31, 23, 59, 58);
            return value;
        }

        private static int GetLfnEntryCount(string name)
        {
            return Math.Max(1, (name.Length + 12) / 13);
        }

        private static byte ShortNameChecksum(byte[] shortName)
        {
            byte sum = 0;
            for (var index = 0; index < 11; index++)
                sum = (byte)(((sum & 1) != 0 ? 0x80 : 0) + (sum >> 1) + shortName[index]);
            return sum;
        }

        private static void WriteFatDateTime(
            byte[] data,
            int entryOffset,
            DateTime value,
            int timeOffset,
            int dateOffset)
        {
            var time = (ushort)((value.Hour << 11) | (value.Minute << 5) | (value.Second / 2));
            var date = (ushort)(((value.Year - 1980) << 9) | (value.Month << 5) | value.Day);
            WriteUInt16(data, entryOffset + timeOffset, time);
            WriteUInt16(data, entryOffset + dateOffset, date);
        }

        private static void WriteAscii(
            byte[] data,
            int offset,
            int length,
            string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            Buffer.BlockCopy(bytes, 0, data, offset, Math.Min(length, bytes.Length));
        }

        private static void WriteUInt16(byte[] data, int offset, int value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
        }

        private static void WriteUInt32(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16);
            data[offset + 3] = (byte)(value >> 24);
        }

        private sealed class Node
        {
            public readonly List<Node> Children = new List<Node>();
            public Node Parent;
            public string Name;
            public string FullPath;
            public bool IsDirectory;
            public long Length;
            public DateTime CreationTime;
            public DateTime LastWriteTime;
            public byte[] ShortName;
            public bool NeedsLongName;
            public uint StartCluster;
            public uint ClusterCount;
            public int DirectoryLength;
            public byte[] DirectoryData;
        }

        private sealed class Allocation
        {
            public Allocation(Node node, uint startCluster, uint clusterCount)
            {
                Node = node;
                StartCluster = startCluster;
                ClusterCount = clusterCount;
            }

            public readonly Node Node;
            public readonly uint StartCluster;
            public readonly uint ClusterCount;
            public uint EndCluster { get { return StartCluster + ClusterCount - 1; } }
        }
    }
}
