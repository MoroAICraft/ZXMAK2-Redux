using System;
using System.Runtime.InteropServices;
using ZXMAK2.Engine.Cpu;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Engine.Interfaces;

namespace ZXMAK2.Hardware.Evo
{
    /// <summary>
    /// UR4QBP schematic V1.0: DD2/DD3 decode xxC4-xxC7 on ordinary I/O
    /// cycles (/M1 high). No DOS gate, jumpers, PCM or IRQ connection.
    /// DOAB feeds the single YAC512; DOCD is not connected.
    /// </summary>
    public sealed class ZxOpl3Ur4qbpDevice : SoundDeviceBase, ISoundMixerConfiguration
    {
        private const long InputClock = 14318180;
        private const long BusClock = 28000000; // PentEvo CPU.Tact is always 28 MHz.
        private const double NativeRate = InputClock / 288D;
        private CpuUnit m_cpu;
        private IntPtr m_core;
        private long m_lastTact, m_clockRemainder;
        private short[] m_nativeBuffer = new short[0];
        private int m_frameSamples, m_renderSample;
        private bool m_rendering;
        private double m_phase;
        private int m_previousLeft, m_previousRight, m_currentLeft, m_currentRight;

        public ZxOpl3Ur4qbpDevice()
        {
            Name = "ZX OPL3 (UR4QBP)";
            Category = BusDeviceCategory.Music;
            Description = "ZX OPL3 Sound Card V1.0 by UR4QBP: YMF262 " +
                "14.31818 MHz + YAC512 stereo, ports #xxC4-#xxC7. " +
                "No OPL4/PCM ROM or RAM. Compatible with ordinary MultiSound.";
        }
        public bool RejectDc { get { return true; } }

        public override void BusInit(IBusManager bmgr)
        {
            if (bmgr.FindDevice<UlaPentEvo>() == null)
                throw new InvalidOperationException("ZX OPL3 (UR4QBP) requires a ZX Evolution ZXBUS slot.");
            if (bmgr.FindDevice<ZxmMoonSoundDevice>() != null ||
                bmgr.FindDevice<ZxMultiSoundMaxDevice>() != null ||
                bmgr.FindDevice<ZxOmniSoundDevice>() != null)
                throw new InvalidOperationException("ZX OPL3 (UR4QBP) shares #C4-#C7 with MoonSound, " +
                    "MultiSound Max and OmniSound. Use ordinary MultiSound or another non-conflicting board.");
            base.BusInit(bmgr);
            m_cpu = bmgr.CPU;
            m_core = Native.Create();
            if (m_core == IntPtr.Zero)
                throw new InvalidOperationException("Cannot initialize ZX OPL3 (UR4QBP) YMF262 core.");
            ResetChip();
            bmgr.Events.SubscribeWrIo(0x00FC, 0x00C4, WritePort);
            bmgr.Events.SubscribeRdIo(0x00FC, 0x00C4, ReadPort);
            bmgr.Events.SubscribeReset(ResetChip);
            // Interrupt acknowledge has its own bus event, not RdIo.
        }
        public override void BusDisconnect()
        {
            m_rendering = false;
            if (m_core != IntPtr.Zero) { Native.Destroy(m_core); m_core = IntPtr.Zero; }
            base.BusDisconnect();
        }
        public override void ResetState() { ResetChip(); }
        private void ResetChip()
        {
            RenderToCurrentTime();
            if (m_core != IntPtr.Zero) Native.Reset(m_core);
            m_lastTact = m_cpu == null ? 0 : m_cpu.Tact;
            m_clockRemainder = 0;
            m_phase = 0;
            m_previousLeft = m_previousRight = m_currentLeft = m_currentRight = 0;
            // Subsequent host PCM comes from the reset core/resampler. No
            // separate DAC transition is needed for an already sampled stream.
        }
        private void AdvanceTimers()
        {
            if (m_cpu == null || m_core == IntPtr.Zero) return;
            long now = m_cpu.Tact, delta = now - m_lastTact;
            m_lastTact = now;
            if (delta < 0) { m_clockRemainder = 0; return; }
            // Split seconds to avoid delta*InputClock overflow on long sessions.
            long scaled = delta % BusClock * InputClock + m_clockRemainder;
            ulong clocks = (ulong)(delta / BusClock * InputClock + scaled / BusClock);
            m_clockRemainder = scaled % BusClock;
            if (clocks != 0) Native.Advance(m_core, clocks);
        }
        private void WritePort(ushort address, byte value, ref bool handled)
        {
            RenderToCurrentTime();
            AdvanceTimers();
            Native.Write(m_core, (uint)(address & 3), value);
            handled = true;
        }
        private void ReadPort(ushort address, ref byte value, ref bool handled)
        {
            if (handled) return;
            RenderToCurrentTime();
            AdvanceTimers();
            // YMF262 status is readable only at A1:A0=00. C6 is NOT an
            // OPL4 ID/status/remapping read; all other offsets return FF.
            value = Native.Read(m_core, (uint)(address & 3));
            handled = true;
        }
        protected override void OnBeginFrame()
        {
            base.OnBeginFrame();
            m_frameSamples = Math.Max(1, AudioBuffer.Length);
            m_renderSample = 0;
            m_rendering = true;
        }
        protected override bool UsesPcmBuffer { get { return true; } }
        protected override void OnEndFrame()
        {
            RenderTo(m_frameSamples);
            AdvanceTimers();
            m_rendering = false;
            base.OnEndFrame();
        }
        private void RenderToCurrentTime()
        {
            if (m_rendering)
                RenderTo((int)(Math.Max(0D, Math.Min(1D, GetFrameTime())) * m_frameSamples));
        }
        private void RenderTo(int target)
        {
            target = Math.Max(m_renderSample, Math.Min(m_frameSamples, target));
            int count = target - m_renderSample;
            if (count <= 0 || m_core == IntPtr.Zero) return;
            double ratio = NativeRate / Math.Max(1, SampleRate);
            int required = (int)Math.Floor(m_phase + count * ratio + 1e-10);
            if (m_nativeBuffer.Length < required * 2) m_nativeBuffer = new short[required * 2];
            if (required > 0) Native.Generate(m_core, m_nativeBuffer, (uint)required);
            int nativeIndex = 0;
            double gain = Volume / 100D;
            while (m_renderSample < target)
            {
                m_phase += ratio;
                while (m_phase >= 1D - 1e-10)
                {
                    m_phase = Math.Max(0D, m_phase - 1D);
                    m_previousLeft = m_currentLeft; m_previousRight = m_currentRight;
                    m_currentLeft = m_nativeBuffer[nativeIndex * 2];
                    m_currentRight = m_nativeBuffer[nativeIndex * 2 + 1];
                    nativeIndex++;
                }
                // Causal interpolation: no future chip samples across writes.
                var left = (short)Math.Round((m_previousLeft + (m_currentLeft - m_previousLeft) * m_phase) * gain);
                var right = (short)Math.Round((m_previousRight + (m_currentRight - m_previousRight) * m_phase) * gain);
                WritePcmSample(m_renderSample, left, right);
                m_renderSample++;
            }
        }
        private static class Native
        {
            private const string Dll = "ymfm_opl3.dll";
            [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ymfm_opl3_create")]
            internal static extern IntPtr Create();
            [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ymfm_opl3_destroy")]
            internal static extern void Destroy(IntPtr p);
            [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ymfm_opl3_reset")]
            internal static extern void Reset(IntPtr p);
            [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ymfm_opl3_advance")]
            internal static extern void Advance(IntPtr p, ulong clocks);
            [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ymfm_opl3_read")]
            internal static extern byte Read(IntPtr p, uint offset);
            [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ymfm_opl3_write")]
            internal static extern void Write(IntPtr p, uint offset, byte value);
            [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ymfm_opl3_generate")]
            internal static extern void Generate(IntPtr p, [Out] short[] buffer, uint count);
        }
    }
}
