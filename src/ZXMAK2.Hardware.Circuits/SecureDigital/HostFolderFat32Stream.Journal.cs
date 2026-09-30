using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ZXMAK2.Hardware.Circuits.SecureDigital
{
    public sealed partial class HostFolderFat32Stream
    {
        private const string JournalMagic = "ZXHSD02";
        private readonly Dictionary<long, byte[]> m_changedSectors =
            new Dictionary<long, byte[]>();
        private string m_journalPath;
        private string m_snapshotFingerprint;
        private FileStream m_journal;
        private long m_validJournalLength;
        public bool WasRecovered { get; set; }

        public bool HasChanges { get { return m_changedSectors.Count != 0; } }

        private void ReadSector(long index, byte[] destination)
        {
            byte[] changed;
            if (m_changedSectors.TryGetValue(index, out changed))
                Buffer.BlockCopy(changed, 0, destination, 0, SectorSize);
            else
                FillSector(index, destination);
        }

        private void InitializeJournal()
        {
            m_snapshotFingerprint = CalculateFingerprint();
            var location = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(location))
                location = Path.GetTempPath();
            var journalFolder = Path.Combine(location, "ZXMAK2", "HostFolderSD");
            Directory.CreateDirectory(journalFolder);
            m_journalPath = Path.Combine(journalFolder,
                HashText(m_sourceFolder.ToUpperInvariant()) + ".journal");
            if (!File.Exists(m_journalPath))
                return;

            using (var input = new FileStream(m_journalPath, FileMode.Open,
                FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new BinaryReader(input, Encoding.UTF8))
            {
                if (reader.ReadString() != JournalMagic ||
                    reader.ReadString() != m_snapshotFingerprint ||
                    reader.ReadInt64() != m_length)
                    throw new InvalidDataException(
                        "An unfinished SD session exists, but the host folder changed. " +
                        "The session was kept at " + m_journalPath);
                while (input.Position + sizeof(long) + SectorSize <= input.Length)
                {
                    var index = reader.ReadInt64();
                    var data = reader.ReadBytes(SectorSize);
                    if (index < 0 || index >= m_length / SectorSize ||
                        data.Length != SectorSize)
                        throw new InvalidDataException("Invalid Host Folder SD journal.");
                    m_changedSectors[index] = data;
                }
                m_validJournalLength = input.Position;
            }
            if (HasChanges)
            {
                WasRecovered = true;
                Logger.Warn("Recovered unfinished Host Folder SD session: {0}",
                    m_sourceFolder);
            }
        }

        private string CalculateFingerprint()
        {
            var description = new StringBuilder();
            description.Append(m_length).Append('|');
            AppendFingerprint(m_root, description);
            return HashText(description.ToString());
        }

        private static void AppendFingerprint(Node node, StringBuilder description)
        {
            description.Append(node.Name).Append('|')
                .Append(node.IsDirectory ? 'D' : 'F').Append('|')
                .Append(node.Length).Append('|')
                .Append(node.LastWriteTime.Ticks).Append('|')
                .Append(node.StartCluster).Append('|');
            foreach (var child in node.Children)
                AppendFingerprint(child, description);
        }

        private static string HashText(string value)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }

        private void AppendJournalSector(long index, byte[] data)
        {
            if (m_journal == null)
            {
                var first = !File.Exists(m_journalPath);
                m_journal = new FileStream(m_journalPath,
                    first ? FileMode.CreateNew : FileMode.Open,
                    FileAccess.ReadWrite, FileShare.Read);
                if (first)
                {
                    var writer = new BinaryWriter(m_journal, Encoding.UTF8);
                    writer.Write(JournalMagic);
                    writer.Write(m_snapshotFingerprint);
                    writer.Write(m_length);
                    writer.Flush();
                }
                else if (m_validJournalLength > 0 &&
                    m_validJournalLength < m_journal.Length)
                {
                    m_journal.SetLength(m_validJournalLength);
                }
                m_journal.Position = m_journal.Length;
            }
            var record = new BinaryWriter(m_journal, Encoding.UTF8);
            record.Write(index);
            record.Write(data);
            record.Flush();
            m_journal.Flush();
        }

        private void CloseJournal()
        {
            var journal = m_journal;
            m_journal = null;
            if (journal != null)
                journal.Dispose();
        }

        public void DiscardChanges()
        {
            CloseJournal();
            if (File.Exists(m_journalPath))
                File.Delete(m_journalPath);
            m_changedSectors.Clear();
        }
    }
}
