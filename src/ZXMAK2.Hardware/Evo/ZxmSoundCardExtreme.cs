using System;
using System.Collections.Generic;
using System.Xml;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Hardware.Circuits.Sound;

namespace ZXMAK2.Hardware.Evo
{
    /// <summary>
    /// Mick's Extreme Rev.03: Rev.02 schematic, Rev.01 V0100 CPLD.
    /// Rev.03 changes mounting only. Decode is from dd2.tdf, NOT MultiSound.
    /// PA/PB and IRQ are unconnected; no MIDI, GS, OPL3 or sample ROM.
    /// </summary>
    public sealed class ZxmSoundCardExtreme : SoundDeviceBase,
        IAdditionalSoundRenderers, ISoundMixerConfiguration
    {
        // Both YM chips have the same A-left, B-centre, C-right wiring.
        private readonly TsFmPsgRenderer[] m_psg = {
            new TsFmPsgRenderer("Extreme YM2203 1 SSG", PanType.Abc,
                new uint[] { 100, 0, 51, 51, 0, 100 }),
            new TsFmPsgRenderer("Extreme YM2203 2 SSG", PanType.Abc,
                new uint[] { 100, 0, 51, 51, 0, 100 })
        };
        private readonly Ym2203FmRenderer m_fm = new Ym2203FmRenderer();
        private readonly TsFmSaa1099Renderer m_saa = new TsFmSaa1099Renderer();
        private readonly byte[] m_register = new byte[2];
        private readonly byte[] m_prescale = { 6, 6 };
        private readonly byte[] m_dac = { 128, 128, 128, 128 };
        private readonly ISoundRenderer[] m_renderers;
        private MemoryPentEvo m_memory;
        private byte m_configuration;
        private byte m_clockControl;
        private bool m_ramRead;

        public ZxmSoundCardExtreme()
        {
            Name = "ZXM-SoundCard Extreme Rev.03";
            Category = BusDeviceCategory.Music;
            Description = "MickLab Extreme Rev.03: 2 x YM2203, SAA1099, " +
                "SounDrive TLC7226. #FFFC switches 3.5/2/4 MHz. " +
                "JP1 disables YM ports; JP2 selects PentEvo bus mode; " +
                "JP3 selects legacy/new SAA ports. No GS/MIDI/OPL3.";
            m_renderers = new ISoundRenderer[] { m_psg[0], m_psg[1], m_fm, m_saa };
            PentEvoJumperClosed = true;
            IoDosInputHigh = true; // BaseConf leaves IODOS open; R39 pulls up.
            ResetBoard();
        }

        public bool PentEvoJumperClosed { get; set; }
        public bool YmPortsDisabled { get; set; }
        public bool LegacySaaPorts { get; set; }
        internal bool IoDosInputHigh { get; set; }
        public bool RejectDc { get { return true; } }
        public int InputClock { get { return (m_clockControl & 1) == 0 ?
            3500000 : (m_clockControl & 2) == 0 ? 2000000 : 4000000; } }
        private int Chip { get { return (m_clockControl & 0x80) != 0 ? 0 : m_configuration & 1; } }
        public IEnumerable<ISoundRenderer> SoundRenderers { get { return m_renderers; } }

        public override void BusInit(IBusManager bmgr)
        {
            // Reject conflicting profiles too, not just interactive slot edits.
            if (bmgr.FindDevice<ZxOmniSoundDevice>() != null ||
                bmgr.FindDevice<ZxMultiSoundDevice>() != null ||
                bmgr.FindDevice<ZxMultiSoundMaxDevice>() != null)
                throw new InvalidOperationException("Extreme shares YM/SAA/SounDrive ports " +
                    "with MultiSound/Max/OmniSound. Choose one of these boards.");
            if (!YmPortsDisabled && (bmgr.FindDevice<AYCHRV>() != null ||
                bmgr.FindDevice<TurboSoundFmPro>() != null))
                throw new InvalidOperationException("Extreme YM ports require Internal Music: None " +
                    "(or close JP1 to disable Extreme YM ports).");
            var tsfm = bmgr.FindDevice<TurboSoundFmPro>();
            if (tsfm != null && tsfm.MultiSoundSaaPortCompatibility)
                throw new InvalidOperationException("Disable TSFM's MultiSound SAA port compatibility " +
                    "when using Extreme: both would respond to #xxFF.");
            base.BusInit(bmgr);
            m_memory = bmgr.FindDevice<MemoryPentEvo>();
            foreach (BusDeviceBase renderer in m_renderers) renderer.BusInit(bmgr);
            bmgr.Events.SubscribeWrIo(0xC0FF, 0xC0FD, WriteAddress);
            bmgr.Events.SubscribeWrIo(0xC0FF, 0x80FD, WriteData);
            bmgr.Events.SubscribeRdIo(0xC0FF, 0xC0FD, ReadYm);
            bmgr.Events.SubscribeWrIo(0xF0FF, 0xF0FC, WriteClock);
            bmgr.Events.SubscribeWrIo(0xC0FF, 0x00FF, WriteSaa);
            bmgr.Events.SubscribeWrIo(0x00AF, 0x000F, WriteDac);
            bmgr.Events.SubscribeWrIo(0x00FF, 0x003F, WriteDac);
            bmgr.Events.SubscribeWrIo(0x00FF, 0x00FB, WriteDac);
            // STRB_ADDR.clk=MREQ|RD: end of EVERY memory read, not only M1.
            bmgr.Events.SubscribeRdMemM1(0, 0, TrackMemoryRead);
            bmgr.Events.SubscribeRdMem(0, 0, TrackMemoryRead);
            bmgr.Events.SubscribeReset(ResetBoard);
            ResetBoard();
        }

        public override void BusConnect()
        {
            base.BusConnect();
            foreach (BusDeviceBase renderer in m_renderers) renderer.BusConnect();
        }
        public override void BusDisconnect()
        {
            foreach (BusDeviceBase renderer in m_renderers) renderer.BusDisconnect();
            base.BusDisconnect();
        }
        public override void ResetState() { ResetBoard(); }

        private void ResetBoard()
        {
            m_configuration = 0x0E; // SEL_chip clear; GET/FM_disable/SAA_disable preset.
            m_clockControl = 0;
            m_ramRead = false;
            Array.Clear(m_register, 0, m_register.Length);
            m_prescale[0] = m_prescale[1] = 6;
            foreach (var psg in m_psg) psg.ResetChip();
            m_fm.ResetChip();
            m_fm.SetInputClock(3500000);
            m_saa.ClockEnabled = false;
            // SAA1099 and TLC7226 have NO /RESET pin. CPU reset must not
            // erase their registers. Constructors supply deterministic power-on.
        }

        private void TrackMemoryRead(ushort address, ref byte value)
        { m_ramRead = (address & 0xC000) != 0; }

        private bool PeripheralPortsAvailable(ushort address)
        {
            return PentEvoJumperClosed ? m_ramRead : IoDosInputHigh &&
                (m_memory == null || m_memory.GetZxBusDosN(address));
        }

        private void WriteAddress(ushort address, byte value, ref bool handled)
        {
            // JP1 gates CS0/CS1/EBUF only: board configuration still responds.
            if ((value & 0xF0) == 0xF0)
            {
                m_configuration = (byte)(value & 15);
                m_fm.Enabled = (value & 4) == 0;
                m_saa.ClockEnabled = (value & 8) == 0;
                return;
            }
            if (YmPortsDisabled) return;
            int chip = Chip;
            m_register[chip] = value;
            m_psg[chip].RegAddr = value;
            m_fm.SetAddress(chip, value);
            if (value == 0x2D) m_prescale[chip] = 6;
            else if (value == 0x2E && m_prescale[chip] == 6) m_prescale[chip] = 3;
            else if (value == 0x2F) m_prescale[chip] = 2;
            UpdateSsgClock(chip);
        }
        private void WriteData(ushort address, byte value, ref bool handled)
        {
            if (YmPortsDisabled) return;
            int chip = Chip, reg = m_register[chip];
            m_fm.SetRegister(chip, reg, value);
            if (reg < 16) m_psg[chip].SetReg(reg, value);
        }
        private void ReadYm(ushort address, ref byte value, ref bool handled)
        {
            if (handled || YmPortsDisabled) return;
            handled = true;
            int chip = Chip, reg = m_register[chip];
            if ((m_configuration & 2) == 0) { value = m_fm.GetStatus(chip); return; }
            // Unconnected YM IO pins read high in input mode, output latch otherwise.
            if ((reg == 14 || reg == 15) &&
                (m_psg[chip].GetReg(7) & (reg == 14 ? 64 : 128)) == 0)
                value = 255;
            else value = reg < 16 ? m_psg[chip].GetReg(reg) : (byte)0;
        }
        private void WriteClock(ushort address, byte value, ref bool handled)
        {
            m_clockControl = (byte)(value & 0x83);
            m_fm.SetInputClock((uint)InputClock);
            UpdateSsgClock(0); UpdateSsgClock(1);
        }
        private void UpdateSsgClock(int chip)
        {
            // ymfm YM2203 effective SSG clock: input*2 / (prescale*2/3).
            // Integer divider matters: prescale=2 is input*2, not input*1.5.
            m_psg[chip].SetChipFrequency(InputClock * 2 / (m_prescale[chip] * 2 / 3));
        }
        private void WriteSaa(ushort address, byte value, ref bool handled)
        {
            if (!PeripheralPortsAvailable(address) ||
                (!LegacySaaPorts && (address & 0x0E00) != 0x0400)) return;
            if ((address & 0x0100) != 0) m_saa.RegAddr = value;
            else m_saa.SetData(value); // Bus writable even with clock stopped.
        }
        private void WriteDac(ushort address, byte value, ref bool handled)
        {
            if (!PeripheralPortsAvailable(address)) return;
            m_dac[((address >> 5) & 2) | ((address >> 4) & 1)] = value;
            UpdateDacs();
        }
        internal bool RespondsToDacWrite(ushort address)
        {
            return PeripheralPortsAvailable(address) &&
                ((address & 0xAF) == 0x0F || (address & 255) == 0x3F ||
                 (address & 255) == 0xFB);
        }
        private void UpdateDacs()
        {
            // A+B left, C+D right. Leave headroom for the shared output mixer.
            var left = (short)((m_dac[0] + m_dac[1] - 256) * 64 * Volume / 100);
            var right = (short)((m_dac[2] + m_dac[3] - 256) * 64 * Volume / 100);
            UpdateDac(GetFrameTime(), left, right);
        }
        protected override void OnBeginFrame() { base.OnBeginFrame(); UpdateDacs(); }
        protected override void OnProcessConfigChange()
        {
            base.OnProcessConfigChange();
            if (m_renderers != null)
                foreach (var renderer in m_renderers) renderer.Volume = Volume;
        }
        protected override void OnConfigLoad(XmlNode node)
        {
            base.OnConfigLoad(node);
            PentEvoJumperClosed = Utils.GetXmlAttributeAsBool(node, "pentEvoJumper", true);
            YmPortsDisabled = Utils.GetXmlAttributeAsBool(node, "disableYmPorts", false);
            LegacySaaPorts = Utils.GetXmlAttributeAsBool(node, "legacySaaPorts", false);
        }
        protected override void OnConfigSave(XmlNode node)
        {
            base.OnConfigSave(node);
            Utils.SetXmlAttribute(node, "pentEvoJumper", PentEvoJumperClosed);
            Utils.SetXmlAttribute(node, "disableYmPorts", YmPortsDisabled);
            Utils.SetXmlAttribute(node, "legacySaaPorts", LegacySaaPorts);
        }
    }
}
