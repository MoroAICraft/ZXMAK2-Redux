using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ZXMAK2.Hardware.Circuits.SecureDigital
{
    public sealed partial class HostFolderFat32Stream
    {
        private const string JournalMagic = "ZXHSD03";
        private readonly Dictionary<long, byte[]> m_changedSectors =
            new Dictionary<long, byte[]>();
        private string m_journalPath;
        private FileStream m_journal;
        // Kept for API compatibility; folder sessions are never recovered.
        public bool WasRecovered { get { return false; } set { } }

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
            // Always build from the current PC folder. A copied or crashed
            // journal must never overlay it or block a new mount.
            m_journalPath = Path.Combine(SdSessionStorage.GetDirectory("HostFolders"),
                Guid.NewGuid().ToString("N") + ".journal");
        }

        private void AppendJournalSector(long index, byte[] data)
        {
            if (m_journal == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(m_journalPath));
                m_journal = new FileStream(m_journalPath, FileMode.CreateNew,
                    FileAccess.ReadWrite, FileShare.Read);
                var writer = new BinaryWriter(m_journal, Encoding.UTF8);
                writer.Write(JournalMagic);
                writer.Write(m_sourceFolder);
                writer.Write(m_length);
                writer.Flush();
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
