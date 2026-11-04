using System;
using System.Runtime.InteropServices;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Interfaces;

namespace ZXMAK2.Hardware.Evo
{
    /// <summary>
    /// Shared YM2203 FM path. The two SSG sections are rendered by the
    /// existing PSG devices, while native ymfm supplies the OPN FM/timer core.
    /// </summary>
    internal sealed class Ym2203FmRenderer : SoundDeviceBase, ITsFmRenderer
    {
        private readonly IntPtr[] m_chip = new IntPtr[2];
        private readonly byte[][] m_registers =
        {
            new byte[256], new byte[256]
        };
        private short[][] m_samples = { new short[0], new short[0] };
        private int m_frameSamples;
        private int m_renderSample;
        private bool m_rendering;
        private bool m_enabled;
        private uint m_inputClock = 3500000;

        public Ym2203FmRenderer()
        {
            Name = "YM2203 FM (ymfm)";
            Description = "Two independent 3.5 MHz YM2203 OPN FM cores";
        }

        public bool Enabled
        {
            get { return m_enabled; }
            set
            {
                if (m_enabled == value)
                    return;
                RenderToCurrentTime();
                m_enabled = value;
            }
        }

        public void SetInputClock(uint frequency)
        {
            if (frequency < 1000000 || frequency > 8000000)
                throw new ArgumentOutOfRangeException("frequency");
            if (m_inputClock == frequency) return;
            RenderToCurrentTime();
            m_inputClock = frequency;
            foreach (var core in m_chip)
                if (core != IntPtr.Zero) Native.SetClock(core, frequency);
        }

        public override void BusInit(IBusManager bmgr)
        {
            base.BusInit(bmgr);
            try
            {
                for (int i = 0; i < m_chip.Length; i++)
                {
                    m_chip[i] = Native.Create();
                    if (m_chip[i] == IntPtr.Zero)
                        throw new InvalidOperationException(
                            "ymfm could not create a YM2203 instance");
                    if (m_inputClock != 3500000)
                        Native.SetClock(m_chip[i], m_inputClock);
                }
            }
            catch (DllNotFoundException ex)
            {
                ReleaseCore();
                throw new InvalidOperationException(
                    "YM2203 requires omni_opn.dll in Lib/Native (or the legacy executable folder)", ex);
            }
            catch
            {
                ReleaseCore();
                throw;
            }
            ResetChip();
        }

        public override void BusDisconnect()
        {
            m_rendering = false;
            ReleaseCore();
            base.BusDisconnect();
        }

        public void ResetChip()
        {
            RenderToCurrentTime();
            foreach (var registers in m_registers)
                Array.Clear(registers, 0, registers.Length);
            foreach (var core in m_chip)
                if (core != IntPtr.Zero)
                    Native.Reset(core);
            m_enabled = false;
        }

        public void SetAddress(int chip, byte address)
        {
            RenderToCurrentTime();
            IntPtr core = m_chip[chip & 1];
            if (core != IntPtr.Zero)
                Native.Address(core, address);
        }

        public void SetRegister(int chip, int index, byte value)
        {
            RenderToCurrentTime();
            chip &= 1;
            index &= 0xFF;
            m_registers[chip][index] = value;
            IntPtr core = m_chip[chip];
            if (core != IntPtr.Zero)
            {
                Native.Address(core, (byte)index);
                Native.Data(core, value);
            }
        }

        public byte GetRegister(int chip, int index)
        {
            // Only SSG data is readable on the YM2203. FM writes must not
            // become a software-defined readback feature of the host board.
            return (index & 0xFF) < 0x10
                ? m_registers[chip & 1][index & 0xFF] : (byte)0;
        }

        public byte GetStatus(int chip)
        {
            RenderToCurrentTime();
            IntPtr core = m_chip[chip & 1];
            return core == IntPtr.Zero ? (byte)0 : Native.Status(core);
        }

        protected override void OnBeginFrame()
        {
            base.OnBeginFrame();
            m_frameSamples = Math.Max(1, AudioBuffer.Length);
            for (int i = 0; i < m_samples.Length; i++)
                if (m_samples[i].Length < m_frameSamples)
                    m_samples[i] = new short[m_frameSamples];
            m_renderSample = 0;
            m_rendering = true;
        }

        protected override bool UsesPcmBuffer { get { return true; } }

        protected override void OnEndFrame()
        {
            RenderTo(m_frameSamples);
            m_rendering = false;
            base.OnEndFrame();
        }

        private void RenderToCurrentTime()
        {
            if (!m_rendering)
                return;
            double frameTime = Math.Max(0D, Math.Min(1D, GetFrameTime()));
            RenderTo((int)(frameTime * m_frameSamples));
        }

        private void RenderTo(int target)
        {
            target = Math.Max(m_renderSample, Math.Min(m_frameSamples, target));
            int count = target - m_renderSample;
            if (count == 0)
                return;
            for (int i = 0; i < m_chip.Length; i++)
                if (m_chip[i] != IntPtr.Zero)
                    Native.Generate(m_chip[i], m_samples[i], (uint)count,
                        (uint)SampleRate);
            for (int i = 0; i < count; i++)
            {
                int mixed = m_enabled ? m_samples[0][i] + m_samples[1][i] : 0;
                int scaled = mixed * Volume / 100;
                short output = (short)Math.Max(short.MinValue,
                    Math.Min(short.MaxValue, scaled));
                WritePcmSample(m_renderSample + i,
                    output, output);
            }
            m_renderSample = target;
        }

        private void ReleaseCore()
        {
            for (int i = 0; i < m_chip.Length; i++)
            {
                if (m_chip[i] == IntPtr.Zero)
                    continue;
                Native.Destroy(m_chip[i]);
                m_chip[i] = IntPtr.Zero;
            }
        }

        private static class Native
        {
            private const string Library = "omni_opn.dll";

            [DllImport(Library, EntryPoint = "omni_opn_set_clock",
                CallingConvention = CallingConvention.Cdecl)]
            public static extern void SetClock(IntPtr instance, uint frequency);

            [DllImport(Library, EntryPoint = "omni_opn_create",
                CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr Create();

            [DllImport(Library, EntryPoint = "omni_opn_destroy",
                CallingConvention = CallingConvention.Cdecl)]
            public static extern void Destroy(IntPtr instance);

            [DllImport(Library, EntryPoint = "omni_opn_reset",
                CallingConvention = CallingConvention.Cdecl)]
            public static extern void Reset(IntPtr instance);

            [DllImport(Library, EntryPoint = "omni_opn_address",
                CallingConvention = CallingConvention.Cdecl)]
            public static extern void Address(IntPtr instance, byte address);

            [DllImport(Library, EntryPoint = "omni_opn_data",
                CallingConvention = CallingConvention.Cdecl)]
            public static extern void Data(IntPtr instance, byte data);

            [DllImport(Library, EntryPoint = "omni_opn_status",
                CallingConvention = CallingConvention.Cdecl)]
            public static extern byte Status(IntPtr instance);

            [DllImport(Library, EntryPoint = "omni_opn_generate",
                CallingConvention = CallingConvention.Cdecl)]
            public static extern void Generate(IntPtr instance,
                [Out] short[] output, uint samples, uint hostRate);
        }
    }
}
