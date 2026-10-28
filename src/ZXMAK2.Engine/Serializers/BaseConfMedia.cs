using System;
using System.IO;
using System.Linq;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Host.Entities;
using ZXMAK2.Host.Interfaces;
using ZXMAK2.Model.Disk;
using ZXMAK2.Serializers.DiskSerializers;

namespace ZXMAK2.Serializers
{
    // Host opening policy, not an extension of the guest ROM file browser.
    public static class BaseConfMedia
    {
        public const string Filter = "ZX Evolution BaseConf (*.trd;*.scl;*.tap;*.sna)|*.trd;*.scl;*.tap;*.sna";
        public static bool IsActive(Spectrum machine)
        { return string.Equals(machine.BusManager.Name, "ZX-Evo BSconf", StringComparison.OrdinalIgnoreCase); }
        public static bool Supports(string path)
        { return new[] { ".trd", ".scl", ".tap", ".sna" }.Contains(Path.GetExtension(path).ToLowerInvariant()); }

        // Validate and buffer before touching live media; the PC file may be
        // replaced while a drive/save dialog is open.
        public static byte[] ReadValidated(string path)
        {
            using (var input = new FileStream(PortablePaths.IoPath(path), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (!Supports(path) || input.Length == 0 || input.Length > 32 * 1024 * 1024)
                    throw new InvalidDataException("Unsupported or empty BaseConf file.");
                var bytes = new byte[(int)input.Length];
                var offset = 0;
                while (offset < bytes.Length)
                {
                    var count = input.Read(bytes, offset, bytes.Length - offset);
                    if (count == 0) throw new EndOfStreamException();
                    offset += count;
                }
                Validate(path, bytes);
                return bytes;
            }
        }

        public static void Validate(string path, byte[] data)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".trd")
            {
                if (data.Length < 4096 || data.Length > 86 * 8192 || data.Length % 256 != 0)
                    throw new InvalidDataException("Invalid TRD size (expected sector-aligned floppy image).");
            }
            else if (ext == ".scl")
            {
                if (data.Length < 9 || System.Text.Encoding.ASCII.GetString(data, 0, 8) != "SINCLAIR" ||
                    data[8] > 128 || data.Length < 9 + 14 * data[8])
                    throw new InvalidDataException("Invalid SCL directory.");
                int sectors = 0;
                for (int i = 0; i < data[8]; i++) sectors += data[9 + 14 * i + 13];
                int size = 9 + 14 * data[8] + sectors * 256;
                if (sectors > 2544 || (data.Length != size && data.Length != size + 4))
                    throw new InvalidDataException("Truncated or oversized SCL image.");
                if (data.Length == size + 4)
                {
                    uint sum = 0;
                    for (int i = 0; i < size; i++) sum += data[i];
                    if (BitConverter.ToUInt32(data, size) != sum)
                        throw new InvalidDataException("SCL checksum mismatch.");
                }
            }
            else if (ext == ".tap")
            {
                if (data.Length == 0) throw new InvalidDataException("Empty TAP file.");
                long periods = 0;
                for (int p = 0; p < data.Length; )
                {
                    if (p + 2 > data.Length) throw new InvalidDataException("Truncated TAP block header.");
                    int length = data[p] | (data[p + 1] << 8);
                    p += 2;
                    if (length < 2 || p + length > data.Length) throw new InvalidDataException("Truncated TAP block.");
                    // TAP expands into pulse lists. Tiny repeated blocks can
                    // exhaust a 32-bit process despite a small input file.
                    periods += 16L * length + (data[p] < 4 ? 8064 : 3220) + 3;
                    if (periods > 32L * 1024 * 1024)
                        throw new InvalidDataException("TAP is too large for safe pulse buffering. Split the tape into smaller images.");
                    p += length; // Do not reject intentional nonstandard tape checksums.
                }
            }
            else if (ext == ".sna")
            {
                if (data.Length != 49179 && data.Length != 131103)
                    throw new InvalidDataException("Supported SNA sizes: 49179 (48K) or 131103 (128K) bytes.");
                if (data[25] > 2) throw new InvalidDataException("Invalid SNA interrupt mode.");
                int sp = data[23] | (data[24] << 8);
                if (data.Length == 49179 && (sp < 0x4000 || sp == 0xFFFF))
                    throw new InvalidDataException("48K SNA return address is outside saved RAM.");
                if (data.Length == 131103 && ((data[49181] & 7) == 2 || (data[49181] & 7) == 5))
                    throw new InvalidDataException("This SNA uses a duplicated RAM bank variant not supported by this loader.");
            }
            else throw new InvalidDataException("BaseConf Open supports TRD, SCL, TAP and SNA only.");
        }

        private sealed class DriveChoice
        {
            public int Index;
            public string Label;
            public override string ToString() { return Label; }
        }

        // Called with VM stopped. Decode into a detached disk first so a bad
        // file/cancel cannot destroy the currently mounted disk.
        public static bool OpenDisk(Spectrum machine, string path, byte[] data, bool readOnly,
            IUserQuery query, IUserMessage message)
        {
            var controller = machine.BusManager.FindDevice<IBetaDiskDevice>();
            if (controller == null) throw new InvalidOperationException("No floppy controller in this machine.");
            var visibility = controller as IHostFloppyVisibility;
            var choices = Enumerable.Range(0, Math.Min(4, controller.FDD.Length))
                .Where(i => visibility == null || visibility.IsHostDriveVisible(i))
                .Select(i => new DriveChoice { Index = i, Label = ((char)('A' + i)) + ": " +
                    (controller.FDD[i].Present ? Path.GetFileName(controller.FDD[i].FileName) + " (replace)" : "Empty") }).ToArray();
            if (choices.Length == 0) throw new InvalidOperationException("All physical drives are overridden by BaseConf virtual drives. Change Virtual Drive in EVO Service first.");
            var choice = choices.FirstOrDefault(c => c.Index == 0 && !controller.FDD[0].Present);
            if (choice == null) choice = query.ObjectSelector(choices.Cast<object>().ToArray(), "Insert disk — select drive") as DriveChoice;
            if (choice == null) return false;

            var prepared = new DiskImage();
            prepared.Init(700000);
            FormatSerializer loader = Path.GetExtension(path).Equals(".scl", StringComparison.OrdinalIgnoreCase)
                ? (FormatSerializer)new SclSerializer(prepared) : new TrdSerializer(prepared);
            using (var input = new MemoryStream(data, false)) loader.Deserialize(input);
            using (var transfer = new MemoryStream())
            {
                new UdiSerializer(prepared).Serialize(transfer);
                var disk = controller.FDD[choice.Index];
                if (disk.ModifyFlag != ModifyFlag.None)
                {
                    var actions = query as IActionUserQuery;
                    var result = actions != null ? actions.ShowActions("The selected disk has unsaved changes.",
                        "Replace disk", "Save & Replace", "Discard & Replace", "Cancel") :
                        query.Show("Save changes before replacing this disk?", "Replace disk", DlgButtonSet.YesNoCancel, DlgIcon.Question);
                    if (result == DlgResult.Cancel || result == DlgResult.None) return false;
                    if (result == DlgResult.Yes)
                    {
                        // UDI preserves nonstandard tracks too; external source stays untouched.
                        var save = PortablePaths.NewSavePath(Path.ChangeExtension(Path.GetFileName(disk.FileName.Length == 0 ? "disk" : disk.FileName), ".udi"), "Floppy");
                        controller.LoadManagers[choice.Index].SaveFileName(save);
                        message.Info("Disk changes saved:\n" + save);
                    }
                }
                transfer.Position = 0;
                new UdiSerializer(disk).Deserialize(transfer);
                disk.FileName = PortablePaths.Store(path);
                disk.IsWP = readOnly;
                disk.ModifyFlag = ModifyFlag.None;
                disk.Present = true;
            }
            return true;
        }
    }
}
