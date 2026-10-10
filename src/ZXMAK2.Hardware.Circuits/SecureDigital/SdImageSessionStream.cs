using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ZXMAK2.Hardware.Circuits.SecureDigital
{
    /// <summary>
    /// Writable sector overlay for a mounted SD image. The original image is
    /// updated only after the user accepts the pending session.
    /// </summary>
    public sealed class SdImageSessionStream : Stream
    {
        private const int SectorSize = 512;
        private const string Magic = "ZXISD02";
        private Stream m_source;
        private string m_imagePath;
        public string ImagePath { get { return m_imagePath; } }
        private string m_journalPath;
        private string m_applyingPath;
        private string m_fingerprint;
        private readonly Dictionary<long, byte[]> m_changed =
            new Dictionary<long, byte[]>();
        private FileStream m_journal;
        private long m_validJournalLength;
        private long m_position;

        public bool WasRecovered { get; set; }

        public SdImageSessionStream(Stream source, string imagePath)
        {
            if (source == null || !source.CanRead ||
                !source.CanSeek || source.Length % SectorSize != 0)
                throw new ArgumentException("The SD image stream is invalid.");
            m_source = source;
            m_imagePath = Path.GetFullPath(imagePath);
            m_fingerprint = Fingerprint();
            SetJournalPaths();
            if (File.Exists(m_journalPath)) Recover();
            else if (File.Exists(m_applyingPath)) File.Delete(m_applyingPath);
        }

        private void SetJournalPaths()
        {
            var folder = SdSessionStorage.GetDirectory("Images");
            PortablePaths.RequireInternal(folder);
            Directory.CreateDirectory(folder);
            var name = Hash(PortablePaths.Store(m_imagePath).ToUpperInvariant());
            m_journalPath = Path.Combine(folder, name + ".journal");
            m_applyingPath = Path.Combine(folder, name + ".applying");
        }

        public bool HasChanges { get { return m_changed.Count != 0; } }
        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return true; } }
        public override bool CanWrite { get { return true; } }
        public override long Length { get { return m_source.Length; } }
        public override long Position
        {
            get { return m_position; }
            set
            {
                if (value < 0 || value > Length)
                    throw new ArgumentOutOfRangeException("value");
                m_position = value;
            }
        }

        private static string Hash(string text)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(
                    Encoding.UTF8.GetBytes(text))).Replace("-", string.Empty);
        }

        private string Fingerprint()
        {
            var info = new FileInfo(m_imagePath);
            return Hash(PortablePaths.Store(m_imagePath).ToUpperInvariant() + "|" +
                info.Length + "|" + info.LastWriteTimeUtc.Ticks);
        }

        private void Recover()
        {
            using (var file = new FileStream(m_journalPath, FileMode.Open,
                FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new BinaryReader(file, Encoding.UTF8))
            {
                if (reader.ReadString() != Magic ||
                    reader.ReadString() != PortablePaths.Store(m_imagePath) ||
                    reader.ReadString() != m_fingerprint &&
                        !File.Exists(m_applyingPath) ||
                    reader.ReadInt64() != Length)
                    throw new InvalidDataException(
                        "The SD image changed while an unfinished session exists. " +
                        "The session remains at " + m_journalPath);
                while (file.Position + sizeof(long) + SectorSize <= file.Length)
                {
                    var index = reader.ReadInt64();
                    var data = reader.ReadBytes(SectorSize);
                    if (index < 0 || index >= Length / SectorSize ||
                        data.Length != SectorSize)
                        throw new InvalidDataException("Invalid SD image session journal.");
                    m_changed[index] = data;
                }
                m_validJournalLength = file.Position;
            }
            if (File.Exists(m_applyingPath) && PortablePaths.IsInternal(m_imagePath))
            {
                // An interrupted save is idempotent. Complete it before the
                // guest can read the image again.
                ApplyChangedSectors();
                ClearJournal();
                m_fingerprint = Fingerprint();
            }
            else if (HasChanges)
            {
                WasRecovered = true;
                Logger.Warn("Recovered unfinished SD image session: {0}",
                    m_imagePath);
            }
        }

        private void ReadSector(long index, byte[] sector)
        {
            byte[] changed;
            if (m_changed.TryGetValue(index, out changed))
            {
                Buffer.BlockCopy(changed, 0, sector, 0, SectorSize);
                return;
            }
            m_source.Position = index * SectorSize;
            var offset = 0;
            while (offset < SectorSize)
            {
                var read = m_source.Read(sector, offset, SectorSize - offset);
                if (read == 0)
                    throw new EndOfStreamException();
                offset += read;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");
            if (offset < 0 || count < 0 || offset > buffer.Length - count)
                throw new ArgumentOutOfRangeException();
            var remaining = (int)Math.Min(count, Length - m_position);
            if (remaining > 0 && !HasChanges)
            {
                m_source.Position = m_position;
                var direct = m_source.Read(buffer, offset, remaining);
                m_position += direct;
                return direct;
            }
            var completed = 0;
            var sector = new byte[SectorSize];
            while (completed < remaining)
            {
                var index = m_position / SectorSize;
                var within = (int)(m_position % SectorSize);
                ReadSector(index, sector);
                var take = Math.Min(remaining - completed, SectorSize - within);
                Buffer.BlockCopy(sector, within, buffer, offset + completed, take);
                completed += take;
                m_position += take;
            }
            return completed;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");
            if (offset < 0 || count < 0 || offset > buffer.Length - count)
                throw new ArgumentOutOfRangeException();
            if (m_position > Length - count)
                throw new IOException("Write past the end of the SD image.");
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
                    Append(index, sector);
                    m_changed[index] = (byte[])sector.Clone();
                }
                m_position += take;
                offset += take;
                count -= take;
            }
        }

        private void Append(long index, byte[] sector)
        {
            if (m_journal == null)
            {
                var first = !File.Exists(m_journalPath);
                m_journal = new FileStream(m_journalPath,
                    first ? FileMode.CreateNew : FileMode.Open,
                    FileAccess.ReadWrite, FileShare.Read);
                if (first)
                {
                    var header = new BinaryWriter(m_journal, Encoding.UTF8);
                    header.Write(Magic);
                    header.Write(PortablePaths.Store(m_imagePath));
                    header.Write(m_fingerprint);
                    header.Write(Length);
                    header.Flush();
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
            record.Write(sector);
            record.Flush();
            m_journal.Flush();
        }

        private void ApplyChangedSectors()
        {
            foreach (var item in m_changed)
            {
                m_source.Position = item.Key * SectorSize;
                m_source.Write(item.Value, 0, SectorSize);
            }
            m_source.Flush();
        }

        public void CommitChanges()
        {
            if (!HasChanges)
                return;
            if (!string.Equals(Fingerprint(), m_fingerprint,
                    StringComparison.Ordinal))
                throw new IOException("The source SD image changed outside the emulator.");
            Flush();
            if (!PortablePaths.IsInternal(m_imagePath))
            {
                var target = PortablePaths.NewSavePath(Path.ChangeExtension(m_imagePath, ".img"), "SD-Images");
                var position = m_position;
                try
                {
                    PortablePaths.WriteAtomic(target, output =>
                    {
                        m_position = 0;
                        var buffer = new byte[65536];
                        int count;
                        while ((count = Read(buffer, 0, buffer.Length)) != 0) output.Write(buffer, 0, count);
                    });
                }
                finally { m_position = position; }
                var replacement = File.Open(target, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                m_source.Dispose();
                m_source = replacement;
                ClearJournal();
                m_imagePath = target;
                SetJournalPaths();
                m_fingerprint = Fingerprint();
                Logger.Info("External SD image saved locally: {0}", target);
                return;
            }
            File.WriteAllText(m_applyingPath, m_imagePath);
            ApplyChangedSectors();
            ClearJournal();
            m_fingerprint = Fingerprint();
        }

        public void DiscardChanges()
        {
            ClearJournal();
        }

        private void ClearJournal()
        {
            if (m_journal != null)
            {
                m_journal.Dispose();
                m_journal = null;
            }
            if (File.Exists(m_journalPath))
                File.Delete(m_journalPath);
            if (File.Exists(m_applyingPath))
                File.Delete(m_applyingPath);
            m_changed.Clear();
        }

        public override void Flush()
        {
            if (m_journal != null)
                m_journal.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            Position = origin == SeekOrigin.Begin ? offset :
                origin == SeekOrigin.Current ? m_position + offset : Length + offset;
            return m_position;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException("The SD image has a fixed capacity.");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (m_journal != null)
                {
                    m_journal.Dispose();
                    m_journal = null;
                }
                m_source.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
