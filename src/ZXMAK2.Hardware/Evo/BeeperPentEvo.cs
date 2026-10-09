using System;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Attributes;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Engine.Cpu;
using ZXMAK2.Hardware.General;


namespace ZXMAK2.Hardware.Evo
{
    /// <summary>
    /// ZX-Evo BaseConf one-bit sound output.  The AVR config0.D3 mux selects
    /// port-FE D4 (beeper) or D3 (tape-out), exactly as sound/sound.v r1364.
    /// </summary>
    public sealed class BeeperPentEvo : SoundDeviceBase
    {
        private CmosPentEvo m_cmos;
        private byte m_lastData;
        private bool m_outputHigh;
        private ushort m_dacHigh;
        private ushort m_currentDac;
        private CpuUnit m_sourceCpu;
        private MemoryPentEvo m_memory;
        private TapeDevice m_tape;
        private bool m_bcTapeWrite;
        private static readonly byte[] BcRealEdge =
            { 0x79, 0x2F, 0x4F, 0xE6, 0x1F, 0xF6, 0x08, 0xD3, 0xFE, 0x37, 0xC9 };
        private static readonly byte[] BcRealBridge =
            { 0x78, 0x32, 0xF1, 0x3C, 0xE5, 0xC5, 0xD5, 0xED, 0x73, 0xF8, 0x3C };

        public BeeperPentEvo()
        {
            Name = "BEEPER PENTEVO";
            Description = "ZX-Evo BaseConf beeper/tape-out mux";
            Volume = 50;
        }

        [HardwareValue("DATA", Description = "Last value written to low byte FE")]
        public byte LastData { get { return m_lastData; } }

        [HardwareValue("OUT", Description = "Selected D4/D3 one-bit output")]
        public bool OutputHigh { get { return m_outputHigh; } }

        [HardwareValue("SOURCE", Description = "Selected port-FE source bit")]
        public int SelectedBit
        {
            get { return m_cmos != null && m_cmos.BeeperTapeOutSelected ? 3 : 4; }
        }

        public override void BusInit(IBusManager bmgr)
        {
            base.BusInit(bmgr);
            m_sourceCpu = bmgr.CPU;
            m_memory = bmgr.FindDevice<MemoryPentEvo>();
            m_tape = bmgr.FindDevice<TapeDevice>();
            if (m_tape != null)
                m_tape.ConfigChanged += TapeConfigChanged;
            m_cmos = bmgr.FindDevice<CmosPentEvo>();
            if (m_cmos != null)
                m_cmos.BeeperMuxChanged += RefreshOutput;
            bmgr.Events.SubscribeWrIo(0x00FF, 0x00FE, WritePortFe);
            bmgr.Events.SubscribeReset(Reset);
        }

        public override void BusDisconnect()
        {
            if (m_cmos != null)
                m_cmos.BeeperMuxChanged -= RefreshOutput;
            if (m_tape != null)
                m_tape.ConfigChanged -= TapeConfigChanged;
            base.BusDisconnect();
        }

        protected override void OnProcessConfigChange()
        {
            base.OnProcessConfigChange();
            var coefficient = Math.Max(0D, Math.Min(1D, Volume / 100D));
            m_dacHigh = (ushort)Math.Floor(ushort.MaxValue * coefficient);
            if (m_sourceCpu != null)
                RefreshOutput();
        }

        private void Reset()
        {
            m_lastData = 0;
            m_bcTapeWrite = false;
            SetOutput(false);
        }

        private void WritePortFe(ushort address, byte value, ref bool handled)
        {
            m_lastData = value;
            // Only BC's identified REAL edge bridge uses Tape volume. ROM
            // loading returns through #053F, whose next FE write is low;
            // game sound, errors, BREAK and reset resume the ordinary mux.
            m_bcTapeWrite = IsBcRealEdge();
            RefreshOutput();
        }

        private bool IsBcRealEdge()
        {
            if (m_sourceCpu == null || m_memory == null || m_tape == null ||
                m_sourceCpu.regs.PC != 0x05FF)
                return false;
            if (m_memory.RDMEM_DBG(0x05E9) != 0xC3 ||
                m_memory.RDMEM_DBG(0x05EA) != 0xC2 ||
                m_memory.RDMEM_DBG(0x05EB) != 0x04)
                return false;
            for (int i = 0; i < BcRealEdge.Length; i++)
                if (m_memory.RDMEM_DBG((ushort)(0x05F6 + i)) != BcRealEdge[i])
                    return false;
            for (int i = 0; i < BcRealBridge.Length; i++)
                if (m_memory.RDMEM_DBG((ushort)(0x04C2 + i)) != BcRealBridge[i])
                    return false;
            return true;
        }

        private void TapeConfigChanged(object sender, EventArgs e)
        {
            RefreshOutput();
        }

        private void RefreshOutput()
        {
            SetOutput((m_lastData & (1 << SelectedBit)) != 0);
        }

        private void SetOutput(bool high)
        {
            ushort gain = m_bcTapeWrite && m_tape != null
                ? (ushort)Math.Floor(ushort.MaxValue * Math.Max(0D, Math.Min(1D, m_tape.Volume / 100D)))
                : m_dacHigh;
            ushort dac = high ? gain : (ushort)0;
            if (high == m_outputHigh && dac == m_currentDac)
                return;
            m_outputHigh = high;
            m_currentDac = dac;
            UpdateDac(dac, dac);
        }
    }
}
