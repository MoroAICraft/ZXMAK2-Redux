using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Engine.Interfaces;


namespace ZXMAK2.Hardware.Evo
{
    /// <summary>
    /// ZXM-MoonSound Rev.01, CPLD firmware 1.01 compatibility.
    ///
    /// The board has one YMF278B/OPL4 at 33.8688 MHz, a 2 MiB YRW801-M
    /// instrument ROM and 1 MiB SRAM.  Rev.01 decodes the low byte only:
    /// #7E/#7F PCM after reset, #C2/#C3 after reading #C6 (CPLD 1.01),
    /// and #C4-#C7 FM. JP1 "PentEvo" disables the external /DOS gate;
    /// PCM remapping in firmware 1.01 is latched by reading #C6.
    /// </summary>
    public sealed class ZxmMoonSoundDevice : SoundDeviceBase,
        ISoundMixerConfiguration
    {
        private const int RomSize = 2 * 1024 * 1024;
        private const int RamSize = 1024 * 1024;
        private static readonly string[] RomNames =
        {
            "YRW801-M - Yamaha - 1993.rom",
            "yrw801.rom",
        };

        private IntPtr m_core;
        private short[] m_nativeBuffer = new short[0];
        private int m_frameSamples;
        private int m_renderSample;
        private bool m_rendering;
        private string m_loadedRomPath = string.Empty;
        private string m_flashPath = string.Empty;
        private bool m_pcmRemapped;
        private MemoryPentEvo m_hostMemory;
        // Rev.01 JP1 "PentEvo" grounds C_PEVO. Rev.C ZXBUS B20 is
        // unconnected; board R5 pulls C_IODOS high. Keep the pin-level
        // ENDOS gate, rather than confusing it with the internal DOS latch.
        internal bool PentEvoJumperClosed { get; set; }
        internal bool IoDosInputHigh { get; set; }
        private double m_resamplePhase;
        private int m_resampleRate;
        private int m_previousLeft, m_previousRight;
        private int m_currentLeft, m_currentRight;

        public ZxmMoonSoundDevice()
        {
            Name = "ZXM-MoonSound Rev.01";
            Description =
                "Mick Laboratory ZXM-MoonSound Rev.01, CPLD 1.01: " +
                "YMF278B/OPL4 33.8688 MHz, YRW801-M 2 MB ROM and 1 MB SRAM. " +
                "MX29F016 flash; PCM #7E/#7F, switches to #C2/#C3 on " +
                "reading #C6; FM #C4-#C7.";
            Category = BusDeviceCategory.Music;
            ResampleHostOutput = true;
            PentEvoJumperClosed = true;
            IoDosInputHigh = true;
        }

        public bool RejectDc { get { return true; } }
        public bool CoreAvailable { get { return m_core != IntPtr.Zero; } }
        public string LoadedRomPath { get { return m_loadedRomPath; } }
        // The physical clock is independent of the host's output sample rate.
        // The 44.1 kHz path remains bit-identical.
        public bool ResampleHostOutput { get; set; }
        // OmniSound shares the Max OPL3 and MoonSound OPL4 FM port group.
        // Select Max's decoder/reset status without duplicating FM voices.
        internal bool MaxOpl3Interface { get; set; }

        public override void BusInit(IBusManager bmgr)
        {
            base.BusInit(bmgr);
            m_hostMemory = bmgr.FindDevice<MemoryPentEvo>();
            InitializeCore();
            bmgr.Events.SubscribeWrIo(0x00FE, 0x007E, WriteWavePort);
            bmgr.Events.SubscribeRdIo(0x00FE, 0x007E, ReadWavePort);
            bmgr.Events.SubscribeWrIo(0x00FE, 0x00C2, WriteWavePort);
            bmgr.Events.SubscribeRdIo(0x00FE, 0x00C2, ReadWavePort);
            bmgr.Events.SubscribeWrIo(0x00FC, 0x00C4, WriteFmPort);
            bmgr.Events.SubscribeRdIo(0x00FC, 0x00C4, ReadFmPort);
            bmgr.Events.SubscribeReset(ResetChip);
        }

        public override void BusConnect()
        {
            base.BusConnect();
        }

        public override void BusDisconnect()
        {
            m_rendering = false;
            ReleaseCore();
            base.BusDisconnect();
        }

        public override void ResetState()
        {
            ResetChip();
        }

        protected override void OnBeginFrame()
        {
            base.OnBeginFrame();
            m_frameSamples = Math.Max(1, AudioBuffer.Length);
            int required = m_frameSamples * 2;
            if (m_nativeBuffer.Length != required)
                m_nativeBuffer = new short[required];
            m_renderSample = 0;
            m_rendering = true;
        }

        protected override void OnEndFrame()
        {
            RenderTo(m_frameSamples);
            m_rendering = false;
            base.OnEndFrame();
        }

        private void InitializeCore()
        {
            ReleaseCore();
            byte[] rom = LoadInstrumentRom();
            if (rom == null)
            {
                Logger.Error(
                    "ZXM-MoonSound: 2 MB YRW801-M ROM not found in " +
                    "ROMS.PAK, beside ZXMAK2.exe or in its roms folder");
                // The chip and SRAM still exist with a blank/unpopulated ROM.
                rom = new byte[RomSize];
                for (int index = 0; index < rom.Length; index++)
                    rom[index] = 0xFF;
            }

            try
            {
                if (rom.Length != RomSize)
                    throw new InvalidDataException(
                        "YRW801-M ROM must be exactly 2097152 bytes");
                m_pcmRemapped = false;
                m_flashPath = GetFlashPath(rom);
                if (File.Exists(m_flashPath))
                {
                    try
                    {
                        byte[] savedFlash = File.ReadAllBytes(m_flashPath);
                        if (savedFlash.Length != RomSize)
                            Logger.Error("MoonSound saved flash has invalid size; using instrument ROM");
                        else
                            rom = savedFlash;
                    }
                    catch (IOException ex)
                    {
                        Logger.Error("MoonSound saved flash unavailable; using instrument ROM");
                        Logger.Error(ex);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        Logger.Error("MoonSound saved flash inaccessible; using instrument ROM");
                        Logger.Error(ex);
                    }
                }
                m_core = NativeMethods.Create(rom, (uint)rom.Length, RamSize);
                if (m_core == IntPtr.Zero)
                    throw new InvalidOperationException(
                        "ymfm could not create a YMF278B instance");
                if (MaxOpl3Interface)
                    NativeMethods.Read(m_core, 0);
                ResetResampler();
            }
            catch (Exception ex)
            {
                m_core = IntPtr.Zero;
                Logger.Error(ex);
            }
        }

        private static string FindRomPath()
        {
            string appFolder = Utils.GetAppFolder();
            foreach (string name in RomNames)
            {
                string direct = Path.Combine(appFolder, name);
                if (File.Exists(direct))
                    return direct;
                string inRoms = Path.Combine(appFolder, "roms", name);
                if (File.Exists(inRoms))
                    return inRoms;
            }
            return string.Empty;
        }

        private void ReleaseCore()
        {
            if (m_core == IntPtr.Zero)
                return;
            SaveFlash();
            NativeMethods.Destroy(m_core);
            m_core = IntPtr.Zero;
        }

        private void ResetChip()
        {
            RenderToCurrentTime();
            m_pcmRemapped = false;
            if (m_core != IntPtr.Zero)
            {
                NativeMethods.Reset(m_core);
                if (MaxOpl3Interface)
                    NativeMethods.Read(m_core, 0);
            }
            ResetResampler();
        }

        private static string GetFlashPath(byte[] rom)
        {
            // Keep the supplied Yamaha image untouched. Each portable install
            // and factory ROM has its own persistent virtual flash.
            using (var hash = SHA256.Create())
            {
                var sourceId = BitConverter.ToString(hash.ComputeHash(rom)).Replace("-", "");
                var installId = BitConverter.ToString(hash.ComputeHash(
                    System.Text.Encoding.UTF8.GetBytes(Utils.GetAppFolder().ToUpperInvariant())))
                    .Replace("-", "").Substring(0, 16);
                return Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData), "ZXMAK2",
                    "MoonSoundFlash", installId + "-" + sourceId + ".bin");
            }
        }

        private void SaveFlash()
        {
            if (string.IsNullOrEmpty(m_flashPath) || !NativeMethods.FlashDirty(m_core))
                return;
            string pending = m_flashPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var data = new byte[RomSize];
                NativeMethods.CopyFlash(m_core, data, (uint)data.Length);
                Directory.CreateDirectory(Path.GetDirectoryName(m_flashPath));
                File.WriteAllBytes(pending, data);
                if (File.Exists(m_flashPath))
                    File.Replace(pending, m_flashPath, m_flashPath + ".bak");
                else
                    File.Move(pending, m_flashPath);
            }
            catch (Exception ex)
            {
                Logger.Error("MoonSound flash could not be saved: " + m_flashPath);
                Logger.Error(ex);
            }
        }

        private byte[] LoadInstrumentRom()
        {
            m_loadedRomPath = FindRomPath();
            if (!string.IsNullOrEmpty(m_loadedRomPath))
                return File.ReadAllBytes(m_loadedRomPath);
            foreach (string name in RomNames)
            {
                try
                {
                    byte[] data = ZXMAK2.Hardware.RomPack.GetRomData(name);
                    m_loadedRomPath = "ROMS.PAK/" + name;
                    return data;
                }
                catch (FileNotFoundException)
                {
                }
            }
            return null;
        }

        private void ResetResampler()
        {
            m_resamplePhase = 0;
            m_resampleRate = SampleRate;
            m_previousLeft = m_previousRight = 0;
            m_currentLeft = m_currentRight = 0;
        }

        private void WriteWavePort(ushort address, byte value,
            ref bool handled)
        {
            if (!AreExpansionPortsAvailable(address) || !IsActiveWavePort(address))
                return;
            RenderToCurrentTime();
            if (m_core != IntPtr.Zero)
                NativeMethods.Write(m_core,
                    (uint)((address & 1) == 0 ? 4 : 5), value);
            handled = true;
        }

        private void ReadWavePort(ushort address, ref byte value,
            ref bool handled)
        {
            if (!AreExpansionPortsAvailable(address) || !IsActiveWavePort(address))
                return;
            RenderToCurrentTime();
            value = m_core != IntPtr.Zero && (address & 1) != 0
                ? NativeMethods.Read(m_core, 5)
                : (byte)0xFF;
            handled = true;
        }

        private void WriteFmPort(ushort address, byte value,
            ref bool handled)
        {
            if (!MaxOpl3Interface && !AreExpansionPortsAvailable(address))
                return;
            RenderToCurrentTime();
            if (m_core != IntPtr.Zero)
                NativeMethods.Write(m_core, (uint)(address & 3), value);
            handled = true;
        }

        private void ReadFmPort(ushort address, ref byte value,
            ref bool handled)
        {
            if ((MaxOpl3Interface && handled) ||
                (!MaxOpl3Interface && !AreExpansionPortsAvailable(address)))
                return;
            RenderToCurrentTime();
            // The CPLD latch is triggered by IN #C6, not OUT to the bank-1
            // address register. Plain OPL3 writes must not break legacy PCM.
            if ((address & 0xFF) == 0xC6)
                m_pcmRemapped = true;
            // Both even ports expose the common YMF278B status.  OPL3 data
            // ports are write-only on the physical chip.
            value = m_core != IntPtr.Zero && (address & 1) == 0
                ? NativeMethods.Read(m_core, 0)
                : (byte)0xFF;
            handled = true;
        }

        private bool IsActiveWavePort(ushort address)
        {
            return (address & 0xFE) == (m_pcmRemapped ? 0xC2 : 0x7E);
        }

        /// <summary>
        /// CPLD: ENDOS = (!C_DOS & C_PEVO) | !C_IODOS.
        /// C_DOS is the external /DOS (ROMPG1) pin, NOT Memory.DOSEN.
        /// With the PentEvo jumper closed, C_PEVO=0; Rev.C leaves IODOS
        /// open/pulled high. Thus CPM/DOSEN does not disable this board.
        /// The actual pin gate remains testable for a driven IODOS or open JP1.
        /// </summary>
        private bool AreExpansionPortsAvailable(ushort address)
        {
            return IoDosInputHigh && (m_hostMemory == null ||
                PentEvoJumperClosed || m_hostMemory.GetZxBusDosN(address));
        }

        private void RenderToCurrentTime()
        {
            if (!m_rendering)
                return;
            double time = Math.Max(0D, Math.Min(1D, GetFrameTime()));
            RenderTo((int)(time * m_frameSamples));
        }

        private void RenderTo(int target)
        {
            target = Math.Max(m_renderSample, Math.Min(m_frameSamples, target));
            int count = target - m_renderSample;
            if (count <= 0)
                return;

            if (m_core == IntPtr.Zero)
            {
                while (m_renderSample < target)
                {
                    UpdateDac((double)m_renderSample / m_frameSamples, 0, 0);
                    m_renderSample++;
                }
                return;
            }

            if (ResampleHostOutput && SampleRate != 44100)
            {
                RenderResampled(count);
                return;
            }
            NativeMethods.Generate(m_core, m_nativeBuffer, (uint)count);
            for (int index = 0; index < count; ++index)
            {
                UpdateDac((double)m_renderSample / m_frameSamples,
                    m_nativeBuffer[index * 2],
                    m_nativeBuffer[index * 2 + 1]);
                m_renderSample++;
            }
        }

        private void RenderResampled(int count)
        {
            if (m_resampleRate != SampleRate)
                ResetResampler();
            var ratio = 44100D / Math.Max(1, SampleRate);
            var required = (int)Math.Floor(m_resamplePhase + count * ratio + 1e-10);
            if (m_nativeBuffer.Length < required * 2)
                m_nativeBuffer = new short[required * 2];
            if (required > 0)
                NativeMethods.Generate(m_core, m_nativeBuffer, (uint)required);
            var nativeIndex = 0;
            for (var index = 0; index < count; index++)
            {
                m_resamplePhase += ratio;
                while (m_resamplePhase >= 1D - 1e-10)
                {
                    m_resamplePhase = Math.Max(0D, m_resamplePhase - 1D);
                    m_previousLeft = m_currentLeft;
                    m_previousRight = m_currentRight;
                    m_currentLeft = m_nativeBuffer[nativeIndex * 2];
                    m_currentRight = m_nativeBuffer[nativeIndex * 2 + 1];
                    nativeIndex++;
                }
                // Causal interpolation: never generate future chip samples
                // across a register write. State continues across frame chunks.
                var left = (short)Math.Round(m_previousLeft +
                    (m_currentLeft - m_previousLeft) * m_resamplePhase);
                var right = (short)Math.Round(m_previousRight +
                    (m_currentRight - m_previousRight) * m_resamplePhase);
                UpdateDac((double)m_renderSample / m_frameSamples, left, right);
                m_renderSample++;
            }
        }

        private static class NativeMethods
        {
            private const string LibraryName = "ymfm_opl4.dll";

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "ymfm_opl4_create")]
            internal static extern IntPtr Create(
                [In] byte[] rom, uint romSize, uint ramSize);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "ymfm_opl4_destroy")]
            internal static extern void Destroy(IntPtr instance);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "ymfm_opl4_reset")]
            internal static extern void Reset(IntPtr instance);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "ymfm_opl4_read")]
            internal static extern byte Read(IntPtr instance, uint offset);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "ymfm_opl4_write")]
            internal static extern void Write(
                IntPtr instance, uint offset, byte value);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "ymfm_opl4_generate")]
            internal static extern void Generate(
                IntPtr instance, [Out] short[] output, uint samples);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "ymfm_opl4_flash_dirty")]
            [return: MarshalAs(UnmanagedType.I1)]
            internal static extern bool FlashDirty(IntPtr instance);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "ymfm_opl4_copy_flash")]
            internal static extern void CopyFlash(
                IntPtr instance, [Out] byte[] output, uint size);
        }
    }
}
