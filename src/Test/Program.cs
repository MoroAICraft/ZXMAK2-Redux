using System;
using System.IO;
using System.Xml;
using System.Reflection;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using ZXMAK2.Host.Interfaces;
using ZXMAK2.Host.Entities;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Engine.Cpu;
using ZXMAK2.Hardware.Circuits.SecureDigital;
using System.Threading;


namespace Test
{
	public class Program
	{
		static void Main(string[] args)
		{
            PortableBootstrap.Initialize();
            Run(args);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void Run(string[] args)
        {
            ZXMAK2.PortablePaths.AcquireInstance();
            try { RunTests(args); }
            finally { ZXMAK2.PortablePaths.ReleaseInstance(); }
        }

        private static void RunTests(string[] args)
        {
            if (args.Length >= 1 && args[0].ToLower() == "/zexall")
			{
				runZexall();
				return;
			}
            if (args.Length >= 1 && args[0].ToLower() == "/perf")
            {
                runPerf();
                return;
            }
            if (args.Length >= 1 && args[0].ToLower() == "/cpu")
            {
                runCpu();
                return;
            }
            if (args.Length >= 1 && args[0].ToLower() == "/tsfm")
            {
                TestTurboSoundFmPro();
                return;
            }
            if (args.Length >= 1 && args[0].ToLower() == "/multisound")
            {
                TestZxMultiSound();
                return;
            }
            if (args.Length >= 1 && args[0].ToLower() == "/multisoundmax")
            {
                TestZxMultiSoundMax();
                return;
            }
            if (args.Length >= 1 && args[0].ToLower() == "/omnisound")
            {
                TestZxOmniSound();
                TestZxOmniSound(48000);
                TestZxOmniSound(96000);
                return;
            }
            if (args.Length >= 1 && args[0].ToLower() == "/midisynth")
            {
                TestMultiSoundMidi();
                return;
            }
            if (args.Length >= 1 && args[0].ToLower() == "/omni-opl")
            {
                TestOmniMaxOplCompatibility();
                return;
            }
            if (args.Length >= 1 && args[0].ToLower() == "/evo-display")
            {
                TestEvoDisplayModes();
                return;
            }
            if (args.Length >= 1 && args[0].ToLower() == "/master-volume")
            {
                TestMasterVolume();
                return;
            }
            if (args.Length >= 1 && args[0].ToLower() == "/moonsound")
            {
                TestZxmMoonSound();
                TestZxmMoonSound(48000);
                TestZxmMoonSound(96000);
                TestMoonFlashPersistence();
                return;
            }
            if (args.Length >= 2 && args[0].ToLower() == "/moonservice")
            {
                TestMoonService(args[1]);
                return;
            }
            if (args.Length >= 1 && args[0].ToLower() == "/zxnetusb")
            {
                TestZxNetUsb();
                return;
            }
            if (args.Length >= 1 && args[0].ToLower() == "/hostfoldersd")
            {
                TestHostFolderSd();
                if (args.Length >= 2)
                    AuditNedoOsHostFolder(args[1]);
                return;
            }

			SanityUla("NOP        ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48_Early(), new byte[] { 0x00 }, s_patternUla48_Early_NOP);
			SanityUla("DJNZ       ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48_Early(), new byte[] { 0x10, 0x00 }, s_patternUla48_Early_DJNZ);
			SanityUla("IN A,(#FE) ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48_Early(), new byte[] { 0xDB, 0xFE }, s_patternUla48_Early_INAFE);
			SanityUla("OUT (#FE),A", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48_Early(), new byte[] { 0xD3, 0xFE }, s_patternUla48_Early_OUTAFE);
			SanityUla("LD A,(HL)  ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48_Early(), new byte[] { 0x7E }, s_patternUla48_Early_LDAHL);
			SanityUla("IN A,(C)   ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48_Early(), new byte[] { 0xED, 0x78 }, s_patternUla48_Early_INAC);
			SanityUla("OUT (C),A  ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48_Early(), new byte[] { 0xED, 0x79 }, s_patternUla48_Early_OUTCA);
			SanityUla("BIT 7,A    ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48_Early(), new byte[] { 0xCB, 0x7F }, s_patternUla48_Early_BIT7A);
			SanityUla("BIT 7,(HL) ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48_Early(), new byte[] { 0xCB, 0x7E }, s_patternUla48_Early_BIT7HL);
			SanityUla("SET 7,(HL) ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48_Early(), new byte[] { 0xCB, 0xFE }, s_patternUla48_Early_SET7HL);

			SanityUla("NOP        ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48(), new byte[] { 0x00 }, s_patternUla48_Late_NOP);
			SanityUla("DJNZ       ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48(), new byte[] { 0x10, 0x00 }, s_patternUla48_Late_DJNZ);
			SanityUla("IN A,(#FE) ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48(), new byte[] { 0xDB, 0xFE }, s_patternUla48_Late_INAFE);
			SanityUla("OUT (#FE),A", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48(), new byte[] { 0xD3, 0xFE }, s_patternUla48_Late_OUTAFE);
			SanityUla("LD A,(HL)  ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48(), new byte[] { 0x7E }, s_patternUla48_Late_LDAHL);
			SanityUla("IN A,(C)   ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48(), new byte[] { 0xED, 0x78 }, s_patternUla48_Late_INAC);
			SanityUla("OUT (C),A  ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48(), new byte[] { 0xED, 0x79 }, s_patternUla48_Late_OUTCA);
			SanityUla("BIT 7,A    ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48(), new byte[] { 0xCB, 0x7F }, s_patternUla48_Late_BIT7A);
			SanityUla("BIT 7,(HL) ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48(), new byte[] { 0xCB, 0x7E }, s_patternUla48_Late_BIT7HL);
			SanityUla("SET 7,(HL) ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48(), new byte[] { 0xCB, 0xFE }, s_patternUla48_Late_SET7HL);

			SanityUla("NOP        ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum128(), new byte[] { 0x00 }, s_patternUla128_NOP);
			SanityUla("INC HL     ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum128(), new byte[] { 0x23 }, s_patternUla128_INCHL);
			SanityUla("LD A,(HL)  ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum128(), new byte[] { 0x7E }, s_patternUla128_LDA_HL_);
			SanityUla("LD (HL),A  ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum128(), new byte[] { 0x77 }, s_patternUla128_LDA_HL_); // pattern the same as ld a,(hl)
			SanityUla("OUT (C),A  ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum128(), new byte[] { 0xED, 0x79, 0x03 }, s_patternUla128_OUTCA);
			SanityUla("IN A,(C)   ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum128(), new byte[] { 0xED, 0x78, 0x03 }, s_patternUla128_OUTCA); // pattern the same as out (c),a

			Console.WriteLine("=====Engine Performance Benchmark (500 frames rendering time)=====");
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            int frameCount = 50 * 10;
			ExecTests("testVideo.z80", frameCount);
			ExecTests("testVideo.z80", frameCount);
			ExecTests("testVideo.z80", frameCount);
			ExecTests("zexall.sna", frameCount);
			ExecTests("zexall.sna", frameCount);
			ExecTests("zexall.sna", frameCount);
			ExecTests("testOutFe.z80", frameCount);
			ExecTests("testOutFe.z80", frameCount);
			ExecTests("testOutFe.z80", frameCount);
            ExecLightTests("zexall.sna", frameCount);
            ExecLightTests("zexall.sna", frameCount);
            ExecLightTests("zexall.sna", frameCount);
            ExecLightTests("zexall.sna", frameCount);
            ExecLightTests("zexall.sna", frameCount);
            ExecCpuTests("zexall.sna", frameCount);
            ExecCpuTests("zexall.sna", frameCount);
            ExecCpuTests("zexall.sna", frameCount);
            ExecCpuTests("zexall.sna", frameCount);
            ExecCpuTests("zexall.sna", frameCount);
        }

		private static void SanityUla(string name, IUlaDevice ula, byte[] opcode, int[] pattern)
		{
			IMemoryDevice mem = new ZXMAK2.Hardware.Spectrum.MemorySpectrum48();// MemoryPentagon128();
            var p128 = GetTestMachine(Resources.machines_test);
			p128.BusManager.Disconnect();
			p128.BusManager.Clear();
			p128.BusManager.Add((BusDeviceBase)mem);
			p128.BusManager.Add((BusDeviceBase)ula);
			p128.BusManager.Connect();
			p128.IsRunning = true;
			p128.DebugReset();
			p128.ExecuteFrame();
			p128.IsRunning = false;

			ushort offset = 0x4000;
			for (int i = 0; i < pattern.Length; i++)
			{
				for (int j = 0; j < opcode.Length; j++)
					mem.WRMEM_DBG(offset++, opcode[j]);
			}
			p128.CPU.regs.PC = 0x4000;
			p128.CPU.regs.IR = 0x4000;
			p128.CPU.regs.SP = 0x4000;
			p128.CPU.regs.AF = 0x4000;
			p128.CPU.regs.HL = 0x4000;
			p128.CPU.regs.DE = 0x4000;
			p128.CPU.regs.BC = 0x4000;
			p128.CPU.regs.IX = 0x4000;
			p128.CPU.regs.IY = 0x4000;
			p128.CPU.regs._AF = 0x4000;
			p128.CPU.regs._HL = 0x4000;
			p128.CPU.regs._DE = 0x4000;
			p128.CPU.regs._BC = 0x4000;
			p128.CPU.regs.MW = 0x4000;
			p128.CPU.IFF1 = p128.CPU.IFF2 = false;
			p128.CPU.IM = 2;
			p128.CPU.BINT = false;
			p128.CPU.FX = CpuModeIndex.None;
			p128.CPU.XFX = CpuModeEx.None;

			long needsTact = pattern[0];
			long frameTact = p128.CPU.Tact % ula.FrameTactCount;
			long deltaTact = needsTact - frameTact;
			if (deltaTact < 0)
				deltaTact += ula.FrameTactCount;
			p128.CPU.Tact += deltaTact;

			//if (pattern == s_patternUla48_Late_LDAHL)
			//    p128.Loader.SaveFileName("TEST-LDAHL-48-LATE.SZX");
			//if (pattern == s_patternUla48_Early_LDAHL)
			//    p128.Loader.SaveFileName("TEST-LDAHL-48-EARLY.SZX");

			for (int i = 0; i < pattern.Length - 1; i++)
			{
				p128.DebugStepInto();
				frameTact = p128.CPU.Tact % ula.FrameTactCount;
				if (frameTact != pattern[i + 1])
				{
					ConsoleColor tmp = Console.ForegroundColor;
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine(
						"Sanity ULA {0} [{1}]:\tfailed @ {2}->{3} (should be {2}->{4})",
						ula.GetType().Name,
						name,
						pattern[i],
						frameTact,
						pattern[i + 1]);
					Console.ForegroundColor = tmp;
					return;
				}
			}
			p128.BusManager.Disconnect();
			ConsoleColor tmp2 = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine("Sanity ULA {0} [{1}]:\tpassed", ula.GetType().Name, name);
			Console.ForegroundColor = tmp2;
		}

        private static Spectrum GetTestMachine(string configXml)
        {
            var config = new XmlDocument();
            config.LoadXml(configXml);
            var machine = new Spectrum();
            try
            {
                // Spectrum.Init loads the portable profile's default machine.
                // The benchmark must start from its embedded MainTest profile,
                // not reuse devices (and their configuration) from that default.
                machine.Init();
                machine.BusManager.Disconnect();
                machine.BusManager.Clear();
                machine.BusManager.LoadConfigXml(config.DocumentElement);
                //var sxml = new XmlDocument();
                //var node = sxml.AppendChild(sxml.CreateElement("Bus"));
                //machine.BusManager.SaveConfigXml(node);
                //sxml.Save("AAAA.xml");
                return machine;
            }
            catch
            {
                machine.Dispose();
                throw;
            }
        }

        private static void TestTurboSoundFmPro()
        {
            IMemoryDevice memory = new ZXMAK2.Hardware.Spectrum.MemorySpectrum48();
            var ula = new ZXMAK2.Hardware.Spectrum.UlaSpectrum48();
            var board = new ZXMAK2.Hardware.Evo.TurboSoundFmPro();
            var machine = GetTestMachine(Resources.machines_test);

            machine.BusManager.Disconnect();
            machine.BusManager.Clear();
            machine.BusManager.Add((BusDeviceBase)memory);
            machine.BusManager.Add((BusDeviceBase)ula);
            machine.BusManager.Add(board);
            machine.BusManager.Connect();

            const ushort programAddress = 0x4000;
            const ushort chip0ReadAddress = 0x4300;
            const ushort chip1ReadAddress = 0x4301;
            const ushort statusAddress = 0x4302;
            var program = new System.Collections.Generic.List<byte>();

            // D1 and D2 are selected by the low bit of the official
            // 1111xxxx CPLD configuration byte.
            AddPortWrite(program, 0xFFFD, 0xFE);
            AddAyWrite(program, 0x00, 0x20);
            AddAyWrite(program, 0x01, 0x01);
            AddAyWrite(program, 0x07, 0x3E);
            AddAyWrite(program, 0x08, 0x04);
            AddPortWrite(program, 0xFFFD, 0x00);
            AddPortReadAndStore(program, 0xFFFD, chip0ReadAddress);

            AddPortWrite(program, 0xFFFD, 0xFF);
            AddAyWrite(program, 0x00, 0x40);
            AddAyWrite(program, 0x01, 0x01);
            AddAyWrite(program, 0x07, 0x3E);
            AddAyWrite(program, 0x08, 0x08);
            AddPortWrite(program, 0xFFFD, 0x00);
            AddPortReadAndStore(program, 0xFFFD, chip1ReadAddress);

            // FM enabled, register read disabled: #FFFD returns YM status.
            AddPortWrite(program, 0xFFFD, 0xF9);
            AddAyWrite(program, 0xA0, 0x69);
            AddAyWrite(program, 0xA4, 0x22);
            AddAyWrite(program, 0xB0, 0x07);
            AddAyWrite(program, 0x30, 0x01);
            AddAyWrite(program, 0x34, 0x01);
            AddAyWrite(program, 0x38, 0x01);
            AddAyWrite(program, 0x3C, 0x01);
            AddAyWrite(program, 0x50, 0x1F);
            AddAyWrite(program, 0x54, 0x1F);
            AddAyWrite(program, 0x58, 0x1F);
            AddAyWrite(program, 0x5C, 0x1F);
            AddAyWrite(program, 0x28, 0xF0);
            AddPortReadAndStore(program, 0xFFFD, statusAddress);

            // SAA is selected by bit 3=0.  Its register and data cycles must
            // use the same #FFFD/#BFFD pair, not the unrelated #1FF/#FF pair.
            AddPortWrite(program, 0xFFFD, 0xF7);
            AddAyWrite(program, 0x00, 0xFF);
            AddAyWrite(program, 0x08, 0x80);
            AddAyWrite(program, 0x10, 0x03);
            AddAyWrite(program, 0x14, 0x01);
            AddAyWrite(program, 0x1C, 0x01);

            for (var i = 0; i < program.Count; i++)
                memory.WRMEM_DBG((ushort)(programAddress + i), program[i]);

            machine.IsRunning = false;
            machine.DebugReset();
            machine.CPU.regs.PC = programAddress;
            while (machine.CPU.regs.PC < programAddress + program.Count)
                machine.DebugStepInto();
            machine.ExecuteFrame();

            bool hasAudio = false;
            foreach (uint sample in machine.BusManager.SoundFrame.GetBuffer())
            {
                if (sample != 0)
                {
                    hasAudio = true;
                    break;
                }
            }

            var sourceAudio = new System.Collections.Generic.List<bool>();
            var sourceClipped = new System.Collections.Generic.List<int>();
            var sourcePeaks = new System.Collections.Generic.List<int>();
            var sourceLengths = new System.Collections.Generic.List<int>();
            foreach (var renderer in board.SoundRenderers)
            {
                bool sourceHasAudio = false;
                int clipped = 0;
                int peak = 0;
                foreach (uint sample in renderer.AudioBuffer)
                {
                    short left = GetLeft(sample);
                    short right = GetRight(sample);
                    int absLeft = Math.Abs((int)left);
                    int absRight = Math.Abs((int)right);
                    if (left != 0 || right != 0)
                        sourceHasAudio = true;
                    if (left == short.MinValue || left == short.MaxValue)
                        clipped++;
                    if (right == short.MinValue || right == short.MaxValue)
                        clipped++;
                    peak = Math.Max(peak, Math.Max(absLeft, absRight));
                }
                sourceAudio.Add(sourceHasAudio);
                sourceClipped.Add(clipped);
                sourcePeaks.Add(peak);
                sourceLengths.Add(renderer.AudioBuffer.Length);
            }

            var gainMethod = typeof(ZXMAK2.Hardware.Evo.TurboSoundFmPro)
                .GetMethod(
                    "ApplyOutputGain",
                    BindingFlags.Static | BindingFlags.NonPublic);
            var gainSamples = new uint[]
            {
                PackStereo(1000, -1000),
                PackStereo(30000, -30000),
                PackStereo(short.MinValue, short.MaxValue),
            };
            gainMethod.Invoke(null, new object[] { gainSamples });
            var gainPassed =
                GetLeft(gainSamples[0]) == 1500 &&
                GetRight(gainSamples[0]) == -1500 &&
                GetLeft(gainSamples[1]) == short.MaxValue &&
                GetRight(gainSamples[1]) == short.MinValue &&
                GetLeft(gainSamples[2]) == short.MinValue &&
                GetRight(gainSamples[2]) == short.MaxValue;

            byte chip0 = memory.RDMEM_DBG(chip0ReadAddress);
            byte chip1 = memory.RDMEM_DBG(chip1ReadAddress);
            byte status = memory.RDMEM_DBG(statusAddress);
            var psgHeadroomPassed =
                sourceClipped[0] < sourceLengths[0] * 2 &&
                sourceClipped[1] < sourceLengths[1] * 2;
            int[] saaCoveragePeaks;
            var saaCoveragePassed = TestSaa1099Coverage(
                board, machine, out saaCoveragePeaks);
            var multiSoundPortCompatibility =
                TestTurboSoundMultiSoundSaaCompatibility();
            machine.BusManager.Disconnect();
            machine.Dispose();

            bool passed = chip0 == 0x20 && chip1 == 0x40 &&
                status == 0x00 && hasAudio && gainPassed &&
                psgHeadroomPassed && saaCoveragePassed &&
                multiSoundPortCompatibility;
            Console.ForegroundColor = passed ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine(
                "TSFM Rev. C: D1=#{0:X2}, D2=#{1:X2}, status=#{2:X2}, audio={3}: {4}",
                chip0,
                chip1,
                status,
                hasAudio,
                passed ? "PASS" : "FAIL");
            Console.WriteLine(
                "Sources: D1={0}, D2={1}, FM={2}, SAA={3}",
                sourceAudio[0],
                sourceAudio[1],
                sourceAudio[2],
                sourceAudio[3]);
            Console.WriteLine(
                "Source peaks/clipped: D1={0}/{1}, D2={2}/{3}, FM={4}/{5}, SAA={6}/{7}",
                sourcePeaks[0], sourceClipped[0],
                sourcePeaks[1], sourceClipped[1],
                sourcePeaks[2], sourceClipped[2],
                sourcePeaks[3], sourceClipped[3]);
            Console.WriteLine("Output gain +50% with saturation: {0}",
                gainPassed ? "PASS" : "FAIL");
            Console.WriteLine("AY/YM SSG native headroom: {0}",
                psgHeadroomPassed ? "PASS" : "FAIL");
            Console.WriteLine(
                "SAA1099 six tones/noise/envelope peaks: {0}: {1}",
                string.Join(",", saaCoveragePeaks),
                saaCoveragePassed ? "PASS" : "FAIL");
            Console.WriteLine(
                "Optional MultiSound #01FF/#00FF SAA ports: {0}",
                multiSoundPortCompatibility ? "PASS" : "FAIL");
            Console.ResetColor();
            if (!passed)
                Environment.ExitCode = 1;
        }

        private static bool TestTurboSoundMultiSoundSaaCompatibility()
        {
            var nativeDefault =
                !new ZXMAK2.Hardware.Evo.TurboSoundFmPro()
                    .MultiSoundSaaPortCompatibility;
            IMemoryDevice memory =
                new ZXMAK2.Hardware.Spectrum.MemorySpectrum48();
            var ula = new ZXMAK2.Hardware.Spectrum.UlaSpectrum48();
            var board = new ZXMAK2.Hardware.Evo.TurboSoundFmPro
            {
                MultiSoundSaaPortCompatibility = true,
            };
            var config = new XmlDocument();
            var configNode = config.AppendChild(config.CreateElement("Device"));
            board.SaveConfigXml(configNode);
            var loadedBoard = new ZXMAK2.Hardware.Evo.TurboSoundFmPro();
            loadedBoard.LoadConfigXml(configNode);
            var configRoundTrip =
                loadedBoard.MultiSoundSaaPortCompatibility;
            var machine = GetTestMachine(Resources.machines_test);
            machine.BusManager.Disconnect();
            machine.BusManager.Clear();
            machine.BusManager.Add((BusDeviceBase)memory);
            machine.BusManager.Add((BusDeviceBase)ula);
            machine.BusManager.Add(board);
            machine.BusManager.Connect();

            const ushort programAddress = 0x4000;
            var program = new System.Collections.Generic.List<byte>();
            AddPortWrite(program, 0x01FF, 0x00);
            AddPortWrite(program, 0x00FF, 0xFF);
            AddPortWrite(program, 0x01FF, 0x08);
            AddPortWrite(program, 0x00FF, 0x80);
            AddPortWrite(program, 0x01FF, 0x10);
            AddPortWrite(program, 0x00FF, 0x03);
            AddPortWrite(program, 0x01FF, 0x14);
            AddPortWrite(program, 0x00FF, 0x01);
            AddPortWrite(program, 0x01FF, 0x1C);
            AddPortWrite(program, 0x00FF, 0x01);
            for (var i = 0; i < program.Count; i++)
                memory.WRMEM_DBG((ushort)(programAddress + i), program[i]);

            machine.IsRunning = false;
            machine.DebugReset();
            machine.CPU.regs.PC = programAddress;
            while (machine.CPU.regs.PC < programAddress + program.Count)
                machine.DebugStepInto();
            machine.ExecuteFrame();

            var renderers =
                new System.Collections.Generic.List<ISoundRenderer>(
                    board.SoundRenderers);
            var audible = renderers.Count > 3 &&
                GetStereoPeak(renderers[3].AudioBuffer) > 256;
            machine.BusManager.Disconnect();
            machine.Dispose();
            return nativeDefault && configRoundTrip && audible;
        }

        private static bool TestSaa1099Coverage(
            ZXMAK2.Hardware.Evo.TurboSoundFmPro board,
            Spectrum machine,
            out int[] peaks)
        {
            ISoundRenderer saa = null;
            foreach (var renderer in board.SoundRenderers)
            {
                if (renderer.GetType().Name.IndexOf(
                    "Saa1099", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    saa = renderer;
                    break;
                }
            }
            if (saa == null)
            {
                peaks = new int[0];
                return false;
            }

            var type = saa.GetType();
            var reset = type.GetMethod("ResetChip");
            var setReg = type.GetMethod("SetReg");
            peaks = new int[8];

            for (int channel = 0; channel < 6; channel++)
            {
                reset.Invoke(saa, null);
                setReg.Invoke(saa, new object[] { channel, (byte)0xFF });
                setReg.Invoke(saa, new object[] { 0x08 + channel, (byte)0x80 });
                int octave = (channel & 1) == 0 ? 0x03 : 0x30;
                setReg.Invoke(saa, new object[]
                {
                    0x10 + channel / 2, (byte)octave,
                });
                setReg.Invoke(saa, new object[]
                {
                    0x14, (byte)(1 << channel),
                });
                setReg.Invoke(saa, new object[] { 0x1C, (byte)0x01 });
                machine.ExecuteFrame();
                peaks[channel] = GetStereoPeak(saa.AudioBuffer);
            }

            // Independent noise clock and an internally-clocked repetitive
            // envelope exercise the two paths that old SAA cores most often
            // approximate incorrectly.
            reset.Invoke(saa, null);
            setReg.Invoke(saa, new object[] { 0x00, (byte)0xFF });
            setReg.Invoke(saa, new object[] { 0x16, (byte)0x02 });
            setReg.Invoke(saa, new object[] { 0x15, (byte)0x01 });
            setReg.Invoke(saa, new object[] { 0x1C, (byte)0x01 });
            machine.ExecuteFrame();
            peaks[6] = GetStereoPeak(saa.AudioBuffer);

            reset.Invoke(saa, null);
            setReg.Invoke(saa, new object[] { 0x02, (byte)0xFF });
            setReg.Invoke(saa, new object[] { 0x09, (byte)0xF0 });
            setReg.Invoke(saa, new object[] { 0x10, (byte)0x70 });
            setReg.Invoke(saa, new object[] { 0x0A, (byte)0x80 });
            setReg.Invoke(saa, new object[] { 0x11, (byte)0x03 });
            setReg.Invoke(saa, new object[] { 0x14, (byte)0x04 });
            setReg.Invoke(saa, new object[] { 0x18, (byte)0x86 });
            setReg.Invoke(saa, new object[] { 0x1C, (byte)0x01 });
            machine.ExecuteFrame();
            peaks[7] = GetStereoPeak(saa.AudioBuffer);

            foreach (var peak in peaks)
                if (peak < 256)
                    return false;
            return true;
        }

        private static int GetStereoPeak(uint[] samples)
        {
            var peak = 0;
            foreach (var sample in samples)
            {
                peak = Math.Max(peak, Math.Abs((int)GetLeft(sample)));
                peak = Math.Max(peak, Math.Abs((int)GetRight(sample)));
            }
            return peak;
        }

        private static void TestZxmMoonSound(int sampleRate = 44100)
        {
            IMemoryDevice memory =
                new ZXMAK2.Hardware.Spectrum.MemorySpectrum48();
            var ula = new ZXMAK2.Hardware.Spectrum.UlaSpectrum48();
            var board = new ZXMAK2.Hardware.Evo.ZxmMoonSoundDevice();
            var machine = GetTestMachine(Resources.machines_test);
            machine.BusManager.Disconnect();
            machine.BusManager.Clear();
            machine.BusManager.Add((BusDeviceBase)memory);
            machine.BusManager.Add((BusDeviceBase)ula);
            machine.BusManager.Add(board);
            machine.BusManager.SampleRate = sampleRate;
            if (!machine.BusManager.Connect() || !board.CoreAvailable)
                throw new InvalidOperationException(
                    "MoonSound core/ROM is unavailable");

            const ushort start = 0x4000;
            const ushort status0Address = 0x4300;
            const ushort status1Address = 0x4301;
            const ushort ramReadAddress = 0x4302;
            var fm = new System.Collections.Generic.List<byte>();

            // Rev.01 decodes the low byte only. Deliberately use non-zero
            // high bytes for all three port groups.
            AddPortReadAndStore(fm, 0x12C4, status0Address);
            AddMoonSoundFmWrite(fm, 0x34C6, 0x05, 0x03); // NEW1 + NEW2
            AddPortReadAndStore(fm, 0x56C6, status1Address);

            // Verify the physical 1 MiB SRAM immediately after the 2 MiB
            // YRW801-M ROM through the OPL4 memory access registers.
            AddMoonSoundWaveWrite(fm, 0x12C2, 0x02, 0x01);
            AddMoonSoundWaveWrite(fm, 0x12C2, 0x03, 0x20);
            AddMoonSoundWaveWrite(fm, 0x12C2, 0x04, 0x00);
            AddMoonSoundWaveWrite(fm, 0x12C2, 0x05, 0x00);
            AddMoonSoundWaveWrite(fm, 0x12C2, 0x06, 0x5A);
            AddMoonSoundWaveWrite(fm, 0x12C2, 0x03, 0x20);
            AddMoonSoundWaveWrite(fm, 0x12C2, 0x04, 0x00);
            AddMoonSoundWaveWrite(fm, 0x12C2, 0x05, 0x00);
            AddPortWrite(fm, 0x34C2, 0x06);
            AddPortReadAndStore(fm, 0x34C3, ramReadAddress);
            AddMoonSoundWaveWrite(fm, 0x12C2, 0x02, 0x00);

            // One OPL3 voice. #C4/#C5 are the low register bank.
            AddMoonSoundWaveWrite(fm, 0x12C2, 0xF8, 0x00);
            AddMoonSoundFmWrite(fm, 0x34C4, 0x20, 0x01);
            AddMoonSoundFmWrite(fm, 0x34C4, 0x23, 0x01);
            AddMoonSoundFmWrite(fm, 0x34C4, 0x40, 0x10);
            AddMoonSoundFmWrite(fm, 0x34C4, 0x43, 0x00);
            AddMoonSoundFmWrite(fm, 0x34C4, 0x60, 0xF0);
            AddMoonSoundFmWrite(fm, 0x34C4, 0x63, 0xF0);
            AddMoonSoundFmWrite(fm, 0x34C4, 0x80, 0x77);
            AddMoonSoundFmWrite(fm, 0x34C4, 0x83, 0x77);
            AddMoonSoundFmWrite(fm, 0x34C4, 0xC0, 0x30);
            AddMoonSoundFmWrite(fm, 0x34C4, 0xA0, 0x98);
            AddMoonSoundFmWrite(fm, 0x34C4, 0xB0, 0x31);
            fm.Add(0x18); fm.Add(0xFE); // JR $
            for (var index = 0; index < fm.Count; ++index)
                memory.WRMEM_DBG((ushort)(start + index), fm[index]);

            machine.IsRunning = false;
            machine.DebugReset();
            machine.CPU.regs.PC = start;
            machine.ExecuteFrame();
            var fmPeak = GetStereoPeak(board.AudioBuffer);
            var status0 = memory.RDMEM_DBG(status0Address);
            var status1 = memory.RDMEM_DBG(status1Address);
            var ramValue = memory.RDMEM_DBG(ramReadAddress);

            // Reset and exercise wavetable channel 0 with ROM waveform 0.
            var pcm = new System.Collections.Generic.List<byte>();
            AddMoonSoundFmWrite(pcm, 0x78C6, 0x05, 0x03);
            AddMoonSoundWaveWrite(pcm, 0x9A7E, 0xF9, 0x00);
            AddMoonSoundWaveWrite(pcm, 0x9A7E, 0x08, 0x00);
            AddMoonSoundWaveWrite(pcm, 0x9A7E, 0x20, 0xFE);
            AddMoonSoundWaveWrite(pcm, 0x9A7E, 0x38, 0x07);
            AddMoonSoundWaveWrite(pcm, 0x9A7E, 0x50, 0x01);
            AddMoonSoundWaveWrite(pcm, 0x9A7E, 0x68, 0x80);
            pcm.Add(0x18); pcm.Add(0xFE);
            for (var index = 0; index < pcm.Count; ++index)
                memory.WRMEM_DBG((ushort)(start + index), pcm[index]);
            machine.DebugReset();
            machine.CPU.regs.PC = start;
            machine.ExecuteFrame();
            var pcmPeak = GetStereoPeak(board.AudioBuffer);

            // NedoOS gp/common/moonsound.asm + startup.asm detector:
            // IN #C6 remaps PCM; two idle status reads, then timer 2 overflow.
            var detector = new System.Collections.Generic.List<byte>();
            AddPortReadAndStore(detector, 0xC6, 0x5200);
            AddPortReadAndStore(detector, 0xC4, 0x5201);
            AddPortReadAndStore(detector, 0xC4, 0x5202);
            AddMoonSoundFmWrite(detector, 0xC4, 0x03, 0xFF);
            AddMoonSoundFmWrite(detector, 0xC4, 0x04, 0x42);
            detector.AddRange(new byte[] { 0x01, 0x00, 0x08, 0x0B, 0x78, 0xB1, 0x20, 0xFB });
            AddPortReadAndStore(detector, 0xC4, 0x5203);
            AddMoonSoundFmWrite(detector, 0xC4, 0x04, 0x80);
            AddMoonSoundFmWrite(detector, 0xC4, 0x04, 0x00);
            detector.Add(0x18); detector.Add(0xFE);
            for (var index = 0; index < detector.Count; index++)
                memory.WRMEM_DBG((ushort)(start + index), detector[index]);
            machine.DebugReset();
            machine.CPU.regs.PC = start;
            machine.ExecuteFrame();
            var nedoDetectPassed = memory.RDMEM_DBG(0x5200) != 0xFF &&
                memory.RDMEM_DBG(0x5201) == 0 && memory.RDMEM_DBG(0x5202) == 0 &&
                memory.RDMEM_DBG(0x5203) == 0xA0;
            var portMapPassed = TestMoonSoundPortMap(board);
            var flashIdPassed = TestMoonSoundFlashId(board);

            // Rev.01 ENDOS uses physical pins: JP1 grounds C_PEVO on
            // PentEvo, while R5 pulls the unconnected IODOS high. Exercise
            // a driven-low IODOS gate, not the unrelated DOSEN latch, without
            // replacing the Spectrum memory used by the audio program.
            var pentEvoMemory = new ZXMAK2.Hardware.Evo.MemoryPentEvo();
            typeof(ZXMAK2.Hardware.MemoryBase)
                .GetField("m_dosen",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                .SetValue(pentEvoMemory, true);
            typeof(ZXMAK2.Hardware.Evo.ZxmMoonSoundDevice)
                .GetField("m_hostMemory",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                .SetValue(board, pentEvoMemory);
            var ioDosPin = typeof(ZXMAK2.Hardware.Evo.ZxmMoonSoundDevice)
                .GetProperty("IoDosInputHigh", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
            ioDosPin.SetValue(board, false, null);
            var gatedRead = typeof(ZXMAK2.Hardware.Evo.ZxmMoonSoundDevice)
                .GetMethod("ReadWavePort",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
            var gatedArguments = new object[]
            {
                (ushort)0x127E, (byte)0xA5, false,
            };
            gatedRead.Invoke(board, gatedArguments);
            var dosGatePassed = !(bool)gatedArguments[2] &&
                (byte)gatedArguments[1] == 0xA5;
            // A driven-low IODOS must release all reads AND writes,
            // including the #C6 latch trigger and both PCM aliases.
            foreach (ushort port in new ushort[] { 0x12C2, 0x12C3, 0x12C4,
                0x12C5, 0x12C6, 0x12C7, 0x127E, 0x127F })
            {
                bool fmPort = (port & 255) >= 0xC4 && (port & 255) <= 0xC7;
                foreach (bool write in new bool[] { false, true })
                {
                    var args = new object[] { port, (byte)0xA5, false };
                    var method = (write ? "Write" : "Read") + (fmPort ? "FmPort" : "WavePort");
                    board.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(board, args);
                    dosGatePassed &= !(bool)args[2] && (byte)args[1] == 0xA5;
                }
            }
            ioDosPin.SetValue(board, true, null);
            var jumper = board.GetType().GetProperty("PentEvoJumperClosed",
                BindingFlags.Instance | BindingFlags.NonPublic);
            jumper.SetValue(board, false, null);
            var available = board.GetType().GetMethod("AreExpansionPortsAvailable",
                BindingFlags.Instance | BindingFlags.NonPublic);
            // ROMPG1 follows the selected physical page on I/O cycles.
            // Test both RAM and ROM pages with JP1 open, independently of DOSEN.
            foreach (bool rom in new bool[] { false, true })
                for (int window = 0; window < 4; window++)
                {
                    string name = "MapRead" + (window * 0x4000).ToString("X4");
                    typeof(ZXMAK2.Hardware.MemoryBase).GetField(name,
                        BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pentEvoMemory,
                            rom ? pentEvoMemory.RomPages[window] : pentEvoMemory.RamPages[window]);
                    ushort port = (ushort)((window * 0x4000) | 0xC4);
                    dosGatePassed &= (bool)available.Invoke(board, new object[] { port }) ==
                        ((window & 2) != 0);
                }
            jumper.SetValue(board, true, null);
            gatedArguments = new object[] { (ushort)0x12C4, (byte)0xA5, false };
            typeof(ZXMAK2.Hardware.Evo.ZxmMoonSoundDevice).GetMethod("ReadFmPort",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(board, gatedArguments);
            dosGatePassed &= (bool)gatedArguments[2] && (byte)gatedArguments[1] != 0xFF;

            machine.BusManager.Disconnect();
            machine.Dispose();
            var passed = status0 != 0xFF && status1 != 0xFF &&
                ramValue == 0x5A && fmPeak > 64 && pcmPeak > 64 &&
                dosGatePassed && portMapPassed && flashIdPassed && nedoDetectPassed;
            Console.ForegroundColor = passed
                ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine(
                "ZXM-MoonSound Rev.01 @{10} Hz: status=#{0:X2}/#{1:X2}, " +
                "SRAM=#{2:X2}, FM peak={3}, PCM peak={4}, " +
                "IODOS pin gate={5}, CPLD 1.01={7}, MX29F016={8}, NedoOS={9}: {6}",
                status0, status1, ramValue, fmPeak, pcmPeak,
                dosGatePassed ? "PASS" : "FAIL",
                passed ? "PASS" : "FAIL", portMapPassed, flashIdPassed, nedoDetectPassed, sampleRate);
            Console.ResetColor();
            if (!passed)
                Environment.ExitCode = 1;
        }

        private static void AddMoonSoundFmWrite(
            System.Collections.Generic.List<byte> program,
            ushort addressPort,
            byte register,
            byte value)
        {
            AddPortWrite(program, addressPort, register);
            AddPortWrite(program, (ushort)(addressPort + 1), value);
        }

        private static byte MoonRead(ZXMAK2.Hardware.Evo.ZxmMoonSoundDevice board,
            string method, ushort port, out bool handled)
        {
            var arguments = new object[] { port, (byte)0xFF, false };
            board.GetType().GetMethod(method, System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).Invoke(board, arguments);
            handled = (bool)arguments[2];
            return (byte)arguments[1];
        }

        private static void TestMoonService(string binaryPath)
        {
            IMemoryDevice memory = new ZXMAK2.Hardware.Spectrum.MemorySpectrum48();
            var board = new ZXMAK2.Hardware.Evo.ZxmMoonSoundDevice();
            var machine = GetTestMachine(Resources.machines_test);
            machine.BusManager.Disconnect();
            machine.BusManager.Clear();
            machine.BusManager.Add((BusDeviceBase)memory);
            machine.BusManager.Add(new ZXMAK2.Hardware.Spectrum.UlaSpectrum48());
            machine.BusManager.Add(board);
            if (!machine.BusManager.Connect() || !board.CoreAvailable)
                throw new InvalidOperationException("MoonSound unavailable");
            byte[] binary = File.ReadAllBytes(binaryPath);
            if (binary.Length != 10451)
                throw new InvalidDataException("Expected unmodified Micklab MoonService v0.3a binary");
            for (int index = 0; index < binary.Length; index++)
                memory.WRMEM_DBG((ushort)(0x6000 + index), binary[index]);
            machine.IsRunning = false;
            machine.DebugReset();
            machine.CPU.regs.SP = 0x5B00;
            machine.CPU.regs.IY = 0x5C3A;
            // Invoke the unchanged service routines, without its keyboard
            // menu/BASIC exit path (this fixture does not boot BASIC).
            byte[] calls = { 0xCD, 0x0F, 0x64, 0xCD, 0x7E, 0x64,
                0xCD, 0x17, 0x6F, 0x18, 0xFE };
            for (int index = 0; index < calls.Length; index++)
                memory.WRMEM_DBG((ushort)(0x9000 + index), calls[index]);
            machine.CPU.regs.PC = 0x9000;
            for (int frame = 0; frame < 150; frame++)
                machine.ExecuteFrame();
            // Symbols from the vendor's moonsound_service.lst (not patches).
            byte chipId = memory.RDMEM_DBG(0x88D0);
            byte manufacturer = memory.RDMEM_DBG(0x74F0);
            byte device = memory.RDMEM_DBG(0x74F1);
            bool passed = chipId == 0x20 && manufacturer == 0xC2 && device == 0xAD;
            Console.WriteLine("Unmodified MoonService v0.3a: chip=#{0:X2}, flash=#{1:X2}/#{2:X2}: {3}",
                chipId, manufacturer, device, passed ? "PASS" : "FAIL");
            if (!passed)
                Console.WriteLine("PC=#{0:X4}, SP=#{1:X4}, first byte=#{2:X2}",
                    machine.CPU.regs.PC, machine.CPU.regs.SP, memory.RDMEM_DBG(0x6000));
            machine.Dispose();
            if (!passed)
                Environment.ExitCode = 1;
        }

        private static void TestMoonFlashPersistence()
        {
            // Use an isolated synthetic chip. Never program the user's Yamaha
            // image or the normal per-install flash file during a smoke test.
            var board = new ZXMAK2.Hardware.Evo.ZxmMoonSoundDevice();
            const BindingFlags hidden = BindingFlags.NonPublic | BindingFlags.Instance;
            var boardType = board.GetType();
            var native = boardType.GetNestedType("NativeMethods", BindingFlags.NonPublic);
            var coreField = boardType.GetField("m_core", hidden);
            Func<string, object[], object> call = (name, arguments) =>
                native.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, arguments);
            var rom = new byte[0x200000];
            for (int index = 0; index < rom.Length; index++) rom[index] = 0xFF;
            var folder = Path.Combine(ZXMAK2.PortablePaths.DataDirectory("Temp"), "ZXMAK2-MoonFlash-" +
                Guid.NewGuid().ToString("N"));
            var path = Path.Combine(folder, "flash.bin");
            boardType.GetField("m_flashPath", hidden).SetValue(board, path);
            IntPtr chip = (IntPtr)call("Create", new object[] { rom, (uint)rom.Length, (uint)0x100000 });
            coreField.SetValue(board, chip);
            try
            {
                if (chip == IntPtr.Zero) throw new InvalidOperationException("Synthetic MoonSound unavailable");
                Action<int, byte> reg = (index, value) =>
                {
                    MoonWrite(board, "WriteWavePort", 0x7E, (byte)index);
                    MoonWrite(board, "WriteWavePort", 0x7F, value);
                };
                Action<int, byte> store = (address, value) =>
                {
                    reg(3, (byte)(address >> 16)); reg(4, (byte)(address >> 8));
                    reg(5, (byte)address); reg(6, value);
                };
                MoonWrite(board, "WriteFmPort", 0xC6, 5);
                MoonWrite(board, "WriteFmPort", 0xC7, 3);
                reg(2, 0x11);
                store(0x555, 0xAA); store(0x2AA, 0x55); store(0x555, 0xA0);
                store(0x1234, 0x5A);
                call("Generate", new object[] { chip, new short[4], (uint)2 });
                boardType.GetMethod("SaveFlash", hidden).Invoke(board, null);
                if (File.ReadAllBytes(path)[0x1234] != 0x5A)
                    throw new InvalidOperationException("MoonSound flash writeback failed");
                store(0x555, 0xAA); store(0x2AA, 0x55); store(0x555, 0xA0);
                store(0x1234, 0x50);
                call("Generate", new object[] { chip, new short[4], (uint)2 });
                boardType.GetMethod("SaveFlash", hidden).Invoke(board, null);
                byte[] saved = File.ReadAllBytes(path);
                if (saved.Length != rom.Length || saved[0x1234] != 0x50 ||
                    File.ReadAllBytes(path + ".bak")[0x1234] != 0x5A)
                    throw new InvalidOperationException("MoonSound atomic flash replace/backup failed");
                call("Destroy", new object[] { chip });
                coreField.SetValue(board, IntPtr.Zero);
                chip = (IntPtr)call("Create", new object[] { saved, (uint)saved.Length, (uint)0x100000 });
                coreField.SetValue(board, chip);
                MoonWrite(board, "WriteFmPort", 0xC6, 5);
                MoonWrite(board, "WriteFmPort", 0xC7, 3);
                reg(2, 0x11); reg(3, 0); reg(4, 0x12); reg(5, 0x34);
                MoonWrite(board, "WriteWavePort", 0x7E, 6);
                bool handled;
                if (MoonRead(board, "ReadWavePort", 0x7F, out handled) != 0x50 || !handled)
                    throw new InvalidOperationException("MoonSound saved flash reload failed");
                Console.WriteLine("MoonSound flash: atomic save, backup and reload of synthetic ROM: PASS");
            }
            finally
            {
                if (chip != IntPtr.Zero) call("Destroy", new object[] { chip });
                coreField.SetValue(board, IntPtr.Zero);
                // Only the two explicitly named fixture files; no recursive delete.
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
                if (Directory.Exists(folder)) Directory.Delete(folder);
            }
        }

        private static void MoonWrite(ZXMAK2.Hardware.Evo.ZxmMoonSoundDevice board,
            string method, ushort port, byte value)
        {
            var arguments = new object[] { port, value, false };
            board.GetType().GetMethod(method, System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).Invoke(board, arguments);
        }

        private static bool TestMoonSoundPortMap(ZXMAK2.Hardware.Evo.ZxmMoonSoundDevice board)
        {
            bool handled;
            board.ResetState();
            MoonRead(board, "ReadWavePort", 0x117F, out handled);
            bool passed = handled;
            MoonRead(board, "ReadWavePort", 0x22C3, out handled);
            passed &= !handled;
            // OUT #C6 selects FM bank 1 but must leave PCM in legacy mode.
            MoonWrite(board, "WriteFmPort", 0x33C6, 0x05);
            MoonWrite(board, "WriteFmPort", 0x44C7, 0x03);
            MoonRead(board, "ReadWavePort", 0x557F, out handled);
            passed &= handled;
            MoonRead(board, "ReadFmPort", 0x66C6, out handled);
            passed &= handled;
            MoonWrite(board, "WriteWavePort", 0x77C2, 0x02);
            passed &= (MoonRead(board, "ReadWavePort", 0x88C3, out handled) & 0xE0) == 0x20;
            passed &= handled;
            MoonRead(board, "ReadWavePort", 0x997F, out handled);
            passed &= !handled;
            // Status polling at #C4 must not switch back to #7E.
            MoonRead(board, "ReadFmPort", 0xAAC4, out handled);
            MoonRead(board, "ReadWavePort", 0xBBC3, out handled);
            passed &= handled;
            board.ResetState();
            MoonRead(board, "ReadWavePort", 0xCCC3, out handled);
            passed &= !handled;
            MoonRead(board, "ReadWavePort", 0xDD7F, out handled);
            return passed && handled;
        }

        private static void MoonFlashAddress(ZXMAK2.Hardware.Evo.ZxmMoonSoundDevice board,
            int address)
        {
            for (int register = 3; register <= 5; register++)
            {
                MoonWrite(board, "WriteWavePort", 0xC2, (byte)register);
                MoonWrite(board, "WriteWavePort", 0xC3,
                    (byte)(address >> ((5 - register) * 8)));
            }
            MoonWrite(board, "WriteWavePort", 0xC2, 6);
        }

        private static bool TestMoonSoundFlashId(ZXMAK2.Hardware.Evo.ZxmMoonSoundDevice board)
        {
            bool handled;
            MoonWrite(board, "WriteFmPort", 0xC6, 5);
            MoonWrite(board, "WriteFmPort", 0xC7, 3);
            MoonRead(board, "ReadFmPort", 0xC6, out handled);
            MoonWrite(board, "WriteWavePort", 0xC2, 2);
            MoonWrite(board, "WriteWavePort", 0xC3, 0x11);
            int[] addresses = { 0x555, 0x2AA, 0x555 };
            byte[] values = { 0xAA, 0x55, 0x90 };
            for (int index = 0; index < addresses.Length; index++)
            {
                MoonFlashAddress(board, addresses[index]);
                MoonWrite(board, "WriteWavePort", 0xC3, values[index]);
            }
            MoonFlashAddress(board, 0);
            byte manufacturer = MoonRead(board, "ReadWavePort", 0xC3, out handled);
            byte device = MoonRead(board, "ReadWavePort", 0xC3, out handled);
            MoonFlashAddress(board, 0);
            MoonWrite(board, "WriteWavePort", 0xC3, 0xF0);
            board.ResetState();
            return manufacturer == 0xC2 && device == 0xAD;
        }

        private static void TestZxNetUsb()
        {
            TestZxNetUsbPortDecode();
            var board = new ZXMAK2.Hardware.Evo.ZxNetUsbDevice();
            TcpListener listener = null;
            TcpClient peer = null;
            try
            {
                board.BusConnect();
                ZxNetWritePort(board, 0x82AB, 0x10);

                byte idHigh = ZxNetReadRegister(board, 0x00FE);
                byte idLow = ZxNetReadRegister(board, 0x00FF);
                if (idHigh != 0x53 || idLow != 0x00)
                    throw new InvalidOperationException(
                        "ZXNetUSB did not expose W5300 ID #5300");

                listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                const int socket0 = 0x200;
                ZxNetWriteRegister(board, socket0 + 0x01, 0x01);
                ZxNetWriteWord(board, socket0 + 0x0A, 49152);
                ZxNetWriteRegister(board, socket0 + 0x14, 127);
                ZxNetWriteRegister(board, socket0 + 0x15, 0);
                ZxNetWriteRegister(board, socket0 + 0x16, 0);
                ZxNetWriteRegister(board, socket0 + 0x17, 1);
                ZxNetWriteWord(board, socket0 + 0x12, port);
                ZxNetWriteRegister(board, socket0 + 0x03, 0x01);
                ZxNetWriteRegister(board, socket0 + 0x03, 0x04);

                DateTime deadline = DateTime.UtcNow.AddSeconds(3);
                while (DateTime.UtcNow < deadline &&
                    ZxNetReadRegister(board, socket0 + 0x09) != 0x17)
                    Thread.Sleep(5);
                if (ZxNetReadRegister(board, socket0 + 0x09) != 0x17)
                    throw new InvalidOperationException(
                        "W5300 TCP socket did not connect");
                peer = listener.AcceptTcpClient();
                peer.ReceiveTimeout = 2000;

                byte[] request = System.Text.Encoding.ASCII.GetBytes("PING");
                for (int index = 0; index < request.Length; index++)
                    ZxNetWriteRegister(board,
                        socket0 + 0x2E + (index & 1), request[index]);
                ZxNetWriteWord(board, socket0 + 0x22, request.Length);
                ZxNetWriteRegister(board, socket0 + 0x03, 0x20);
                byte[] received = new byte[request.Length];
                int receivedCount = peer.GetStream().Read(received, 0,
                    received.Length);
                if (receivedCount != request.Length ||
                    System.Text.Encoding.ASCII.GetString(received) != "PING")
                    throw new InvalidOperationException(
                        "W5300 TCP transmit path failed");

                byte[] response = System.Text.Encoding.ASCII.GetBytes("PONG");
                peer.GetStream().Write(response, 0, response.Length);
                deadline = DateTime.UtcNow.AddSeconds(3);
                while (DateTime.UtcNow < deadline &&
                    ZxNetReadWord(board, socket0 + 0x2A) == 0)
                    Thread.Sleep(5);
                int packetLength = (ZxNetReadRegister(board,
                    socket0 + 0x30) << 8) |
                    ZxNetReadRegister(board, socket0 + 0x31);
                byte[] reply = new byte[packetLength];
                for (int index = 0; index < reply.Length; index++)
                    reply[index] = ZxNetReadRegister(board,
                        socket0 + 0x30 + (index & 1));
                ZxNetWriteRegister(board, socket0 + 0x03, 0x40);
                if (System.Text.Encoding.ASCII.GetString(reply) != "PONG")
                    throw new InvalidOperationException(
                        "W5300 TCP receive path failed");

                // NedoOS normally obtains its address through DHCP.  Verify
                // the built-in host-side DHCP reply without using the PC's
                // privileged UDP/68 service.
                const int socket1 = 0x240;
                ZxNetWriteRegister(board, socket1 + 0x01, 0x02);
                ZxNetWriteWord(board, socket1 + 0x0A, 68);
                ZxNetWriteRegister(board, socket1 + 0x14, 255);
                ZxNetWriteRegister(board, socket1 + 0x15, 255);
                ZxNetWriteRegister(board, socket1 + 0x16, 255);
                ZxNetWriteRegister(board, socket1 + 0x17, 255);
                ZxNetWriteWord(board, socket1 + 0x12, 67);
                ZxNetWriteRegister(board, socket1 + 0x03, 0x01);
                byte[] discover = new byte[244];
                discover[0] = 1; discover[1] = 1; discover[2] = 6;
                discover[4] = 0x12; discover[5] = 0x34;
                discover[6] = 0x56; discover[7] = 0x78;
                discover[236] = 0x63; discover[237] = 0x82;
                discover[238] = 0x53; discover[239] = 0x63;
                discover[240] = 53; discover[241] = 1;
                discover[242] = 1; discover[243] = 255;
                for (int index = 0; index < discover.Length; index++)
                    ZxNetWriteRegister(board,
                        socket1 + 0x2E + (index & 1), discover[index]);
                ZxNetWriteWord(board, socket1 + 0x22, discover.Length);
                ZxNetWriteRegister(board, socket1 + 0x03, 0x20);
                int udpLength = ZxNetReadWord(board, socket1 + 0x2A);
                byte[] udpHeader = new byte[8];
                for (int index = 0; index < udpHeader.Length; index++)
                    udpHeader[index] = ZxNetReadRegister(board,
                        socket1 + 0x30 + (index & 1));
                int dhcpLength = (udpHeader[6] << 8) | udpHeader[7];
                byte[] offer = new byte[dhcpLength];
                for (int index = 0; index < offer.Length; index++)
                    offer[index] = ZxNetReadRegister(board,
                        socket1 + 0x30 + (index & 1));
                bool dhcpPassed = udpLength >= dhcpLength + 8 &&
                    offer.Length > 19 && offer[0] == 2 &&
                    offer[16] == 10 && offer[17] == 0 &&
                    offer[18] == 2 && offer[19] == 15;
                if (!dhcpPassed)
                    throw new InvalidOperationException(
                        "ZXNetUSB DHCP reply failed");

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(
                    "ZXNetUSB Rev.C: Z80 I/O=PASS, W5300 ID=#5300, " +
                    "TCP TX/RX=PASS, DHCP=10.0.2.15 PASS");
                Console.ResetColor();
            }
            finally
            {
                if (peer != null)
                    peer.Close();
                if (listener != null)
                    listener.Stop();
                board.BusDisconnect();
            }
        }

        private static void TestZxNetUsbPortDecode()
        {
            IMemoryDevice memory =
                new ZXMAK2.Hardware.Spectrum.MemorySpectrum48();
            var ula = new ZXMAK2.Hardware.Spectrum.UlaSpectrum48();
            var board = new ZXMAK2.Hardware.Evo.ZxNetUsbDevice();
            var machine = GetTestMachine(Resources.machines_test);
            try
            {
                machine.BusManager.Disconnect();
                machine.BusManager.Clear();
                machine.BusManager.Add((BusDeviceBase)memory);
                machine.BusManager.Add((BusDeviceBase)ula);
                machine.BusManager.Add(board);
                machine.BusManager.Connect();

                const ushort programAddress = 0x4000;
                const ushort resultAddress = 0x4300;
                var program = new System.Collections.Generic.List<byte>();
                AddPortWrite(program, 0x82AB, 0x10);
                AddPortWrite(program, 0x81AB, 0x03);
                AddPortReadAndStore(program, 0x3EAB, resultAddress);
                AddPortReadAndStore(program, 0x3FAB,
                    (ushort)(resultAddress + 1));
                for (int index = 0; index < program.Count; index++)
                    memory.WRMEM_DBG((ushort)(programAddress + index),
                        program[index]);

                machine.IsRunning = false;
                machine.DebugReset();
                machine.CPU.regs.PC = programAddress;
                while (machine.CPU.regs.PC <
                    programAddress + program.Count)
                    machine.DebugStepInto();

                if (memory.RDMEM_DBG(resultAddress) != 0x53 ||
                    memory.RDMEM_DBG((ushort)(resultAddress + 1)) != 0x00)
                    throw new InvalidOperationException(
                        "ZXNetUSB Z80 I/O decode did not expose W5300 ID");
            }
            finally
            {
                machine.BusManager.Disconnect();
                machine.Dispose();
            }
        }

        private static void ZxNetWriteWord(
            ZXMAK2.Hardware.Evo.ZxNetUsbDevice board, int address,
            int value)
        {
            ZxNetWriteRegister(board, address, (byte)(value >> 8));
            ZxNetWriteRegister(board, address + 1, (byte)value);
        }

        private static int ZxNetReadWord(
            ZXMAK2.Hardware.Evo.ZxNetUsbDevice board, int address)
        {
            return (ZxNetReadRegister(board, address) << 8) |
                ZxNetReadRegister(board, address + 1);
        }

        private static void ZxNetWriteRegister(
            ZXMAK2.Hardware.Evo.ZxNetUsbDevice board, int address,
            byte value)
        {
            ZxNetWritePort(board, 0x81AB, (byte)(address >> 6));
            // Use the lower mirror exactly like the current NedoOS driver.
            ushort port = (ushort)((address & 0x3F) << 8 | 0xAB);
            ZxNetWritePort(board, port, value);
        }

        private static byte ZxNetReadRegister(
            ZXMAK2.Hardware.Evo.ZxNetUsbDevice board, int address)
        {
            ZxNetWritePort(board, 0x81AB, (byte)(address >> 6));
            ushort port = (ushort)((address & 0x3F) << 8 | 0xAB);
            return ZxNetReadPort(board, port);
        }

        private static void ZxNetWritePort(
            ZXMAK2.Hardware.Evo.ZxNetUsbDevice board, ushort port,
            byte value)
        {
            string name = port == 0x81AB ? "WriteAddress" :
                port == 0x82AB ? "WriteControl" :
                port == 0x83AB ? "WriteInterrupt" : "WriteW5300";
            MethodInfo method = board.GetType().GetMethod(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            object[] arguments = { port, value, false };
            method.Invoke(board, arguments);
            if (!(bool)arguments[2])
                throw new InvalidOperationException(
                    "ZXNetUSB did not handle write to #" +
                    port.ToString("X4"));
        }

        private static byte ZxNetReadPort(
            ZXMAK2.Hardware.Evo.ZxNetUsbDevice board, ushort port)
        {
            string name = port == 0x81AB ? "ReadAddress" :
                port == 0x82AB ? "ReadControl" :
                port == 0x83AB ? "ReadInterrupt" : "ReadW5300";
            MethodInfo method = board.GetType().GetMethod(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            object[] arguments = { port, (byte)0xFF, false };
            method.Invoke(board, arguments);
            if (!(bool)arguments[2])
                throw new InvalidOperationException(
                    "ZXNetUSB did not handle read from #" +
                    port.ToString("X4"));
            return (byte)arguments[1];
        }

        private static void AddMoonSoundWaveWrite(
            System.Collections.Generic.List<byte> program,
            ushort addressPort,
            byte register,
            byte value)
        {
            AddPortWrite(program, addressPort, register);
            AddPortWrite(program, (ushort)(addressPort + 1), value);
        }

        private static void TestZxMultiSound()
        {
            IMemoryDevice memory =
                new ZXMAK2.Hardware.Spectrum.MemorySpectrum48();
            var ula = new ZXMAK2.Hardware.Spectrum.UlaSpectrum48();
            var board = new ZXMAK2.Hardware.Evo.ZxMultiSoundDevice();
            var machine = GetTestMachine(Resources.machines_test);

            machine.BusManager.Disconnect();
            machine.BusManager.Clear();
            machine.BusManager.Add((BusDeviceBase)memory);
            machine.BusManager.Add((BusDeviceBase)ula);
            machine.BusManager.Add(board);
            if (!machine.BusManager.Connect())
                throw new InvalidOperationException("MultiSound bus connect failed");

            const ushort programAddress = 0x4000;
            const ushort ymReadAddress = 0x4300;
            const ushort gsStatusAddress = 0x4301;
            const ushort gsReadyStatusAddress = 0x4302;
            const ushort gsReadyProgramAddress = 0x4400;
            var program = new System.Collections.Generic.List<byte>();

            // SAA address/data are #1FF/#FF, unlike the socket TSFM board.
            // The real CPLD keeps these writes active while saa_clk_en is
            // false.  Preload the complete voice before #F7 so a regression
            // that silently discards stopped-clock writes cannot pass.
            AddPortWrite(program, 0x01FF, 0x00);
            AddPortWrite(program, 0x00FF, 0xFF);
            AddPortWrite(program, 0x01FF, 0x08);
            AddPortWrite(program, 0x00FF, 0x80);
            AddPortWrite(program, 0x01FF, 0x10);
            AddPortWrite(program, 0x00FF, 0x03);
            AddPortWrite(program, 0x01FF, 0x14);
            AddPortWrite(program, 0x00FF, 0x01);
            AddPortWrite(program, 0x01FF, 0x1C);
            AddPortWrite(program, 0x00FF, 0x01);

            // Rev.A2 uses partial YM decoding. These aliases deliberately are
            // not the canonical #FFFD/#BFFD pair. Configure both physical
            // YM2203 SSG sections with independent tones; a register-only
            // check used by the older probe could pass with one silent chip.
            AddPortWrite(program, 0xD00D, 0xF6);
            AddPortWrite(program, 0xD00D, 0x00);
            AddPortWrite(program, 0x900D, 0x34);
            AddPortWrite(program, 0xD00D, 0x01);
            AddPortWrite(program, 0x900D, 0x01);
            AddPortWrite(program, 0xD00D, 0x07);
            AddPortWrite(program, 0x900D, 0x3E);
            AddPortWrite(program, 0xD00D, 0x08);
            AddPortWrite(program, 0x900D, 0x0F);

            AddPortWrite(program, 0xD00D, 0xF7);
            AddPortWrite(program, 0xD00D, 0x00);
            AddPortWrite(program, 0x900D, 0x62);
            AddPortWrite(program, 0xD00D, 0x01);
            AddPortWrite(program, 0x900D, 0x02);
            AddPortWrite(program, 0xD00D, 0x07);
            AddPortWrite(program, 0x900D, 0x3E);
            AddPortWrite(program, 0xD00D, 0x08);
            AddPortWrite(program, 0x900D, 0x0F);

            // Return to D1 with the SAA clock enabled, then select register 0
            // and verify the D1 value through another partial port alias.
            AddPortWrite(program, 0xD00D, 0xF6);
            AddPortWrite(program, 0xD00D, 0x00);
            AddPortReadAndStore(program, 0xE00D, ymReadAddress);

            // Four physical DAC aliases and a GS command/status handshake.
            AddPortWrite(program, 0x000F, 0x20);
            AddPortWrite(program, 0x001F, 0x60);
            AddPortWrite(program, 0x004F, 0xA0);
            AddPortWrite(program, 0x005F, 0xE0);
            AddPortWrite(program, 0x00BB, 0x00);
            AddPortReadAndStore(program, 0x00BB, gsStatusAddress);

            for (var i = 0; i < program.Count; i++)
                memory.WRMEM_DBG((ushort)(programAddress + i), program[i]);

            machine.IsRunning = false;
            machine.DebugReset();
            machine.CPU.regs.PC = programAddress;
            while (machine.CPU.regs.PC < programAddress + program.Count)
                machine.DebugStepInto();
            machine.ExecuteFrame();

            var ym = memory.RDMEM_DBG(ymReadAddress);
            var gsStatus = memory.RDMEM_DBG(gsStatusAddress);
            var saaAudio = false;
            var soundRenderers = new System.Collections.Generic.List<ISoundRenderer>(board.SoundRenderers);
            var ymD1Audio = soundRenderers.Count > 0 &&
                GetStereoPeak(soundRenderers[0].AudioBuffer) > 0;
            var ymD2Audio = soundRenderers.Count > 1 &&
                GetStereoPeak(soundRenderers[1].AudioBuffer) > 0;
            foreach (var renderer in soundRenderers)
            {
                if (renderer.GetType().Name.IndexOf("Saa1099",
                    StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                foreach (var sample in renderer.AudioBuffer)
                    saaAudio |= sample != 0;
            }
            var dacAudio = false;
            foreach (var sample in board.AudioBuffer)
                dacAudio |= sample != 0;

            // Rev.A2 remembers whether the last host opcode fetch was in the
            // lower 16K and locks SAA/SounDrive in that state.  Exercise the
            // write guard directly so this CPLD quirk cannot regress silently.
            var boardType = typeof(ZXMAK2.Hardware.Evo.ZxMultiSoundCore);
            var romLockField = boardType.GetField(
                "m_hostRomM1Access",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var dacSampleField = boardType.GetField(
                "m_dacSample",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var writeSoundDrive = boardType.GetMethod(
                "WriteSoundDrive",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var beforeLockedWrite = ((byte[])dacSampleField.GetValue(board))[0];
            romLockField.SetValue(board, true);
            var lockedWriteArgs = new object[]
            {
                (ushort)0x000F, (byte)(beforeLockedWrite ^ 0xFF), false,
            };
            writeSoundDrive.Invoke(board, lockedWriteArgs);
            var romLockPassed =
                ((byte[])dacSampleField.GetValue(board))[0] == beforeLockedWrite;
            romLockField.SetValue(board, false);

            // Keep the host Z80 in a harmless loop while the independent GS
            // Z80 completes the official ROM RAM test and consumes command 0.
            memory.WRMEM_DBG(0x4500, 0x18);
            memory.WRMEM_DBG(0x4501, 0xFE);
            machine.CPU.regs.PC = 0x4500;
            for (var frame = 1; frame < 200; frame++)
                machine.ExecuteFrame();

            var readyProgram = new System.Collections.Generic.List<byte>();
            AddPortReadAndStore(
                readyProgram, 0x00BB, gsReadyStatusAddress);
            for (var i = 0; i < readyProgram.Count; i++)
                memory.WRMEM_DBG(
                    (ushort)(gsReadyProgramAddress + i), readyProgram[i]);
            machine.CPU.regs.PC = gsReadyProgramAddress;
            while (machine.CPU.regs.PC <
                gsReadyProgramAddress + readyProgram.Count)
                machine.DebugStepInto();

            var gsReadyStatus = memory.RDMEM_DBG(gsReadyStatusAddress);

            var auto = new ZXMAK2.Hardware.Evo.ZxMultiSoundDevice();
            auto.ResolveConfiguration(true, true);
            var autoPolicy = !auto.EffectiveGeneralSoundEnabled &&
                !auto.EffectiveYmEnabled && auto.EffectiveSaaEnabled &&
                auto.EffectiveSoundDriveEnabled;
            var manualRejected = false;
            var manual = new ZXMAK2.Hardware.Evo.ZxMultiSoundDevice();
            manual.AutomaticConfiguration = false;
            try
            {
                manual.ResolveConfiguration(true, false);
            }
            catch (InvalidOperationException)
            {
                manualRejected = true;
            }

            int gsDacTransitions;
            int soundDriveTransitions;
            var gsDacTimeline = TestMultiSoundGsDacTimeline(
                out gsDacTransitions);
            var soundDriveTimeline = TestMultiSoundSoundDriveTimeline(
                out soundDriveTransitions);
            var tsFmPortCompatibility =
                TestMultiSoundTsFmSaaCompatibility();

            machine.BusManager.Disconnect();
            machine.Dispose();
            var passed = ym == 0x34 && ymD1Audio && ymD2Audio &&
                (gsStatus & 1) != 0 &&
                (gsReadyStatus & 1) == 0 &&
                saaAudio && dacAudio && romLockPassed &&
                autoPolicy && manualRejected && gsDacTimeline &&
                soundDriveTimeline && tsFmPortCompatibility;
            Console.ForegroundColor = passed
                ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine(
                "ZX-MultiSound A2: YM=#{0:X2}, D1={1}, D2={2}, " +
                "GSSTAT=#{3:X2}->#{4:X2}, SAA-PRELOAD={5}, DAC={6}, " +
                "ROM-LOCK={7}, AUTO={8}, MANUAL-GUARD={9}: {10}",
                ym, ymD1Audio, ymD2Audio, gsStatus, gsReadyStatus,
                saaAudio, dacAudio, romLockPassed, autoPolicy, manualRejected,
                passed ? "PASS" : "FAIL");
            Console.WriteLine(
                "GS DAC timeline transitions={0}: {1}; " +
                "SounDrive transitions={2}: {3}",
                gsDacTransitions, gsDacTimeline ? "PASS" : "FAIL",
                soundDriveTransitions,
                soundDriveTimeline ? "PASS" : "FAIL");
            Console.WriteLine(
                "Optional TSFM #FFFD/#BFFD SAA ports: {0}",
                tsFmPortCompatibility ? "PASS" : "FAIL");
            Console.ResetColor();
            if (!passed)
                Environment.ExitCode = 1;
        }

        private static double s_omniFmFrequency;
        private static double s_omniYmFmFrequency;

        private static void TestZxOmniSound(int sampleRate = 44100)
        {
            TestOmniPsgEnvelopes();
            var board = new ZXMAK2.Hardware.Evo.ZxOmniSoundDevice();
            board.SdImageFileName = "omnisound-test.img";
            var xml = new XmlDocument();
            var node = xml.AppendChild(xml.CreateElement("Device"));
            board.SaveConfigXml(node);
            var restored = new ZXMAK2.Hardware.Evo.ZxOmniSoundDevice();
            restored.LoadConfigXml(node);
            var configOk = restored.SdImageFileName ==
                "omnisound-test.img" && restored.TsFmSaaPortCompatibility &&
                !restored.GeneralSoundEnabled;

            IMemoryDevice memory =
                new ZXMAK2.Hardware.Spectrum.MemorySpectrum48();
            var ula = new ZXMAK2.Hardware.Spectrum.UlaSpectrum48();
            var machine = GetTestMachine(Resources.machines_test);
            machine.BusManager.Disconnect();
            machine.BusManager.Clear();
            machine.BusManager.Add((BusDeviceBase)memory);
            machine.BusManager.Add((BusDeviceBase)ula);
            machine.BusManager.Add(board);
            machine.BusManager.SampleRate = sampleRate;
            if (!machine.BusManager.Connect())
                throw new InvalidOperationException("OmniSound connect failed");

            // v76: the composite board must use the same corrected CPLD/flash
            // as standalone MoonSound, without a second implementation.
            if (!TestMoonSoundPortMap(board.MoonSound) || !TestMoonSoundFlashId(board.MoonSound))
                throw new InvalidOperationException("OmniSound MoonSound port/flash integration failed");
            board.ResetState();
            Console.WriteLine("OmniSound shared MoonSound CPLD 1.01 / MX29F016 C2/AD: PASS");

            var renderers = new System.Collections.Generic.List<ISoundRenderer>(
                board.SoundRenderers);
            var core = typeof(ZXMAK2.Hardware.Evo.ZxMultiSoundCore);
            var gsRam = (byte[])core.GetField("m_gsRam",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(board);
            var ownershipOk = renderers.Count == 7 &&
                gsRam.Length == 0 && !board.EffectiveGeneralSoundEnabled &&
                board.EffectiveYmEnabled && board.EffectiveSaaEnabled &&
                board.EffectiveSoundDriveEnabled && board.MoonSound.CoreAvailable;
            var mixerGains = (int[])typeof(FrameSound).GetField("_sourceGains",
                BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(machine.BusManager.SoundFrame);
            ownershipOk &= mixerGains.Length == 8;
            foreach (var gain in mixerGains) ownershipOk &= gain == 140;

            const ushort start = 0x4000;
            var program = new System.Collections.Generic.List<byte>();
            // Both independent YM2203 SSG sections must produce audio.
            AddPortWrite(program, 0xFFFD, 0xF6);
            AddAyWrite(program, 0x00, 0x34);
            AddAyWrite(program, 0x01, 0x01);
            AddAyWrite(program, 0x07, 0x3E);
            AddAyWrite(program, 0x08, 0x0F);
            AddPortWrite(program, 0xFFFD, 0xF7);
            AddAyWrite(program, 0x00, 0x62);
            AddAyWrite(program, 0x01, 0x02);
            AddAyWrite(program, 0x07, 0x3E);
            AddAyWrite(program, 0x08, 0x0F);
            // Preload SAA through the native MultiSound #1FF/#FF ports.
            AddPortWrite(program, 0x01FF, 0x00);
            AddPortWrite(program, 0x00FF, 0xFF);
            AddPortWrite(program, 0x01FF, 0x08);
            AddPortWrite(program, 0x00FF, 0x80);
            AddPortWrite(program, 0x01FF, 0x10);
            AddPortWrite(program, 0x00FF, 0x03);
            AddPortWrite(program, 0x01FF, 0x14);
            AddPortWrite(program, 0x00FF, 0x01);
            AddPortWrite(program, 0x01FF, 0x1C);
            AddPortWrite(program, 0x00FF, 0x01);
            // SoundDrive's four DACs are owned by the MultiSound half.
            AddPortWrite(program, 0x000F, 0x20);
            AddPortWrite(program, 0x001F, 0x60);
            AddPortWrite(program, 0x004F, 0xA0);
            AddPortWrite(program, 0x005F, 0xE0);
            // The OPL4 FM port must remain available without a second OPL3.
            AddMoonSoundFmWrite(program, 0x34C4, 0x20, 0x01);
            AddMoonSoundFmWrite(program, 0x34C4, 0x23, 0x01);
            AddMoonSoundFmWrite(program, 0x34C4, 0x40, 0x10);
            AddMoonSoundFmWrite(program, 0x34C4, 0x43, 0x00);
            AddMoonSoundFmWrite(program, 0x34C4, 0x60, 0xF0);
            AddMoonSoundFmWrite(program, 0x34C4, 0x63, 0xF0);
            AddMoonSoundFmWrite(program, 0x34C4, 0x80, 0x77);
            AddMoonSoundFmWrite(program, 0x34C4, 0x83, 0x77);
            AddMoonSoundFmWrite(program, 0x34C4, 0xC0, 0x30);
            AddMoonSoundFmWrite(program, 0x34C4, 0xA0, 0x98);
            AddMoonSoundFmWrite(program, 0x34C4, 0xB0, 0x31);
            program.Add(0x18);
            program.Add(0xFE);
            for (var i = 0; i < program.Count; i++)
                memory.WRMEM_DBG((ushort)(start + i), program[i]);
            machine.IsRunning = false;
            machine.DebugReset();
            machine.CPU.regs.PC = start;
            machine.ExecuteFrame();
            var ym0Peak = GetStereoPeak(renderers[0].AudioBuffer);
            var ym1Peak = GetStereoPeak(renderers[1].AudioBuffer);
            var directSaaPeak = GetStereoPeak(renderers[3].AudioBuffer);
            var drivePeak = GetStereoPeak(board.AudioBuffer);
            var oplPeak = GetStereoPeak(board.MoonSound.AudioBuffer);
            var fmSamples = new System.Collections.Generic.List<short>();
            for (var frame = 0; frame < 100; frame++)
            {
                machine.ExecuteFrame();
                foreach (var sample in board.MoonSound.AudioBuffer)
                    fmSamples.Add(GetLeft(sample));
            }
            var fmFrequency = MeasureToneFrequency(fmSamples, sampleRate);
            if (sampleRate == 44100) s_omniFmFrequency = fmFrequency;
            var fmRateOk = Math.Abs(fmFrequency - s_omniFmFrequency) < 1;

            // A fresh reset proves the TSFM alias works independently of
            // the preceding native SAA writes.
            program.Clear();
            AddPortWrite(program, 0xFFFD, 0xF7);
            AddAyWrite(program, 0x00, 0xFF);
            AddAyWrite(program, 0x08, 0x80);
            AddAyWrite(program, 0x10, 0x03);
            AddAyWrite(program, 0x14, 0x01);
            AddAyWrite(program, 0x1C, 0x01);
            program.Add(0x18);
            program.Add(0xFE);
            for (var i = 0; i < program.Count; i++)
                memory.WRMEM_DBG((ushort)(start + i), program[i]);
            machine.DebugReset();
            machine.CPU.regs.PC = start;
            machine.ExecuteFrame();
            var tsFmSaaPeak = GetStereoPeak(renderers[3].AudioBuffer);

            program.Clear();
            AddMoonSoundFmWrite(program, 0x78C6, 0x05, 0x03);
            AddMoonSoundWaveWrite(program, 0x9A7E, 0xF9, 0x00);
            AddMoonSoundWaveWrite(program, 0x9A7E, 0x08, 0x00);
            AddMoonSoundWaveWrite(program, 0x9A7E, 0x20, 0xFE);
            AddMoonSoundWaveWrite(program, 0x9A7E, 0x38, 0x07);
            AddMoonSoundWaveWrite(program, 0x9A7E, 0x50, 0x01);
            AddMoonSoundWaveWrite(program, 0x9A7E, 0x68, 0x80);
            program.Add(0x18); program.Add(0xFE);
            for (var i = 0; i < program.Count; i++)
                memory.WRMEM_DBG((ushort)(start + i), program[i]);
            machine.DebugReset();
            machine.CPU.regs.PC = start;
            machine.ExecuteFrame();
            var pcmPeak = GetStereoPeak(board.MoonSound.AudioBuffer);

            program.Clear();
            AddPortWrite(program, 0xFFFD, 0xF7);
            AddAyWrite(program, 0x00, 0xFF);
            AddAyWrite(program, 0x08, 0x80);
            AddAyWrite(program, 0x10, 0x03);
            AddAyWrite(program, 0x14, 0x01);
            AddAyWrite(program, 0x1C, 0x01);
            program.Add(0x18); program.Add(0xFE);
            for (var i = 0; i < program.Count; i++)
                memory.WRMEM_DBG((ushort)(start + i), program[i]);
            machine.DebugReset();
            machine.CPU.regs.PC = start;
            for (var frame = 0; frame < 10; frame++) machine.ExecuteFrame();
            var toneSamples = new System.Collections.Generic.List<short>();
            for (var frame = 0; frame < 100; frame++)
            {
                machine.ExecuteFrame();
                foreach (var sample in renderers[3].AudioBuffer)
                    toneSamples.Add(GetLeft(sample));
            }
            long toneSum = 0;
            foreach (var sample in toneSamples) toneSum += sample;
            var mean = (double)toneSum / toneSamples.Count;
            var edges = 0;
            for (var i = 1; i < toneSamples.Count; i++)
                if (toneSamples[i - 1] < mean && toneSamples[i] >= mean) edges++;
            var saaFrequency = (double)edges * renderers[3].SampleRate / toneSamples.Count;
            var expectedSaaFrequency = 8000000D * 8 / (512D * (511 - 128));
            var frequencyOk = Math.Abs(saaFrequency - expectedSaaFrequency) < 1;

            // The composed NeoGS must actually consume a host command,
            // not merely exist as a silent renderer in the device list.
            for (var frame = 0; frame < 200; frame++)
                machine.ExecuteFrame();
            program.Clear();
            AddPortWrite(program, 0x00BB, 0x00);
            AddPortReadAndStore(program, 0x00BB, 0x4700);
            for (var i = 0; i < program.Count; i++)
                memory.WRMEM_DBG((ushort)(0x4600 + i), program[i]);
            machine.CPU.regs.PC = 0x4600;
            while (machine.CPU.regs.PC < 0x4607)
                machine.DebugStepInto();
            // Observe the latch at the write boundary. A subsequent status
            // read is allowed to advance NeoGS and consume a fast command.
            var gsPending = ((byte)typeof(ZXMAK2.Hardware.Evo.NeoGsDevice)
                .GetField("m_status", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(board.NeoGs) & 1) != 0;
            while (machine.CPU.regs.PC < 0x4600 + program.Count)
                machine.DebugStepInto();
            memory.WRMEM_DBG(0x4800, 0x18);
            memory.WRMEM_DBG(0x4801, 0xFE);
            machine.CPU.regs.PC = 0x4800;
            for (var frame = 0; frame < 200; frame++)
                machine.ExecuteFrame();
            program.Clear();
            AddPortReadAndStore(program, 0x00BB, 0x4701);
            for (var i = 0; i < program.Count; i++)
                memory.WRMEM_DBG((ushort)(0x4600 + i), program[i]);
            machine.CPU.regs.PC = 0x4600;
            while (machine.CPU.regs.PC < 0x4600 + program.Count)
                machine.DebugStepInto();
            var gsReady = (memory.RDMEM_DBG(0x4701) & 1) == 0;
            var externalEnvelopeOk = TestSaaExternalCycles(renderers[3]) &&
                !TestSaaExternalCycles(renderers[3], true);
            int ymFm0Peak, ymFm1Peak;
            double ymFmFrequency;
            bool ymFmTimerOk;
            bool ymPrescaleOk;
            TestOmniOpn(machine, memory, renderers[2], sampleRate,
                out ymFm0Peak, out ymFm1Peak, out ymFmFrequency,
                out ymFmTimerOk, out ymPrescaleOk);
            if (sampleRate == 44100)
                s_omniYmFmFrequency = ymFmFrequency;
            var expectedYmFmFrequency = 617D * 16D * 3500000D /
                (144D * 1048576D);
            var ymFmRateOk =
                Math.Abs(ymFmFrequency - s_omniYmFmFrequency) < 1D &&
                Math.Abs(ymFmFrequency - expectedYmFmFrequency) < 1D;
            machine.BusManager.Disconnect();

            var conflictRejected = true;
            BusDeviceBase[] conflicts = {
                new ZXMAK2.Hardware.Evo.NeoGsDevice(),
                new ZXMAK2.Hardware.Evo.ZxmMoonSoundDevice(),
                new ZXMAK2.Hardware.Evo.ZxMultiSoundDevice(),
                new ZXMAK2.Hardware.Evo.ZxMultiSoundMaxDevice(),
                new ZXMAK2.Hardware.Evo.TurboSoundFmPro(),
                new ZXMAK2.Hardware.Evo.AYCHRV(),
            };
            foreach (var conflict in conflicts)
            {
                machine.BusManager.Add(conflict);
                var rejected = false;
                try { board.BusInit(machine.BusManager); }
                catch (InvalidOperationException) { rejected = true; }
                conflictRejected &= rejected;
                machine.BusManager.Remove(conflict);
            }
            machine.Dispose();

            var passed = configOk && ownershipOk && ym0Peak > 256 &&
                ym1Peak > 256 && directSaaPeak > 256 &&
                tsFmSaaPeak > 256 && drivePeak > 0 && oplPeak > 256 &&
                pcmPeak > 256 && gsPending && gsReady && conflictRejected &&
                frequencyOk && fmRateOk && externalEnvelopeOk &&
                ymFm0Peak > 256 && ymFm1Peak > 256 &&
                ymFmRateOk && ymFmTimerOk && ymPrescaleOk;
            Console.WriteLine("ZX OmniSound: config={0}, owners={1}, " +
                "YM={2}/{3}, SAA={4}/{5}, Drive={6}, OPL={7}: {8}",
                configOk, ownershipOk, ym0Peak, ym1Peak,
                directSaaPeak, tsFmSaaPeak, drivePeak, oplPeak,
                passed ? "PASS" : "FAIL");
            Console.WriteLine("OPL4 PCM={0}, NeoGS pending/ready={1}/{2}, " +
                "conflict guard={3}", pcmPeak, gsPending, gsReady,
                conflictRejected);
            Console.WriteLine("SAA frequency measured/expected={0:F3}/{1:F3} Hz",
                saaFrequency, expectedSaaFrequency);
            Console.WriteLine("Host rate={0}, OPL FM pitch={1:F3} Hz, invariant={2}",
                sampleRate, fmFrequency, fmRateOk);
            Console.WriteLine("SAA external envelope address/data cycles: {0}",
                externalEnvelopeOk ? "PASS" : "FAIL");
            Console.WriteLine(
                "YM2203 FM: D1={0}, D2={1}, pitch={2:F3} Hz, " +
                "rate invariant={3}, timer={4}, prescale={5}",
                ymFm0Peak, ymFm1Peak, ymFmFrequency,
                ymFmRateOk, ymFmTimerOk, ymPrescaleOk);
            if (!passed)
                Environment.ExitCode = 1;
        }

        private static void TestOmniOpn(Spectrum machine,
            IMemoryDevice memory, ISoundRenderer renderer, int sampleRate,
            out int firstPeak, out int secondPeak, out double frequency,
            out bool timerOk, out bool prescaleOk)
        {
            const ushort start = 0x4000;
            var peaks = new int[2];
            frequency = 0D;
            timerOk = true;
            for (int chip = 0; chip < 2; chip++)
            {
                var program = new System.Collections.Generic.List<byte>();
                // SAA disabled, FM enabled, register read enabled.
                AddPortWrite(program, 0xFFFD, (byte)(0xFA | chip));
                foreach (int slot in new int[] { 0, 4, 8, 12 })
                {
                    AddAyWrite(program, (byte)(0x30 + slot), 0x01);
                    AddAyWrite(program, (byte)(0x40 + slot), 0x30);
                    AddAyWrite(program, (byte)(0x50 + slot), 0x1F);
                    AddAyWrite(program, (byte)(0x60 + slot), 0x00);
                    AddAyWrite(program, (byte)(0x70 + slot), 0x00);
                    AddAyWrite(program, (byte)(0x80 + slot), 0x0F);
                }
                AddAyWrite(program, 0xA4, 0x22);
                AddAyWrite(program, 0xA0, 0x69);
                AddAyWrite(program, 0xB0, 0x07);
                AddAyWrite(program, 0x28, 0xF0);
                AddAyWrite(program, 0x24, 0xFF);
                AddAyWrite(program, 0x25, 0x03);
                AddAyWrite(program, 0x27, 0x05);
                program.Add(0x18); program.Add(0xFE);
                for (int i = 0; i < program.Count; i++)
                    memory.WRMEM_DBG((ushort)(start + i), program[i]);
                machine.DebugReset();
                machine.CPU.regs.PC = start;
                for (int frame = 0; frame < 5; frame++)
                    machine.ExecuteFrame();
                peaks[chip] = GetStereoPeak(renderer.AudioBuffer);
                var opn = typeof(ZXMAK2.Hardware.Evo.ZxMultiSoundCore)
                    .GetField("m_fm", BindingFlags.Instance |
                        BindingFlags.NonPublic).GetValue(
                            machine.BusManager.FindDevice<
                                ZXMAK2.Hardware.Evo.ZxOmniSoundDevice>());
                var getStatus = opn.GetType().GetMethod("GetStatus");
                byte status = (byte)getStatus.Invoke(opn, new object[] { chip });
                timerOk &= (status & 1) != 0;
                opn.GetType().GetMethod("SetRegister").Invoke(opn,
                    new object[] { chip, 0x27, (byte)0x10 });
                timerOk &= ((byte)getStatus.Invoke(opn,
                    new object[] { chip }) & 1) == 0;
                if (chip == 0)
                {
                    var audio = new System.Collections.Generic.List<short>();
                    for (int frame = 0; frame < 100; frame++)
                    {
                        machine.ExecuteFrame();
                        foreach (var sample in renderer.AudioBuffer)
                            audio.Add(GetLeft(sample));
                    }
                    frequency = MeasureToneFrequency(audio, sampleRate);
                }
            }
            firstPeak = peaks[0];
            secondPeak = peaks[1];

            var board = machine.BusManager.FindDevice<
                ZXMAK2.Hardware.Evo.ZxOmniSoundDevice>();
            var psgs = (Array)typeof(ZXMAK2.Hardware.Evo.ZxMultiSoundCore)
                .GetField("m_psg", BindingFlags.Instance |
                    BindingFlags.NonPublic).GetValue(board);
            var frequencyProperty = psgs.GetValue(0).GetType()
                .GetProperty("ChipFrequency");
            var prescaleProgram = new System.Collections.Generic.List<byte>();
            AddPortWrite(prescaleProgram, 0xFFFD, 0xFA);
            AddPortWrite(prescaleProgram, 0xFFFD, 0x2F);
            prescaleProgram.Add(0x18); prescaleProgram.Add(0xFE);
            for (int i = 0; i < prescaleProgram.Count; i++)
                memory.WRMEM_DBG((ushort)(start + i), prescaleProgram[i]);
            machine.DebugReset();
            machine.CPU.regs.PC = start;
            machine.ExecuteFrame();
            prescaleOk = (int)frequencyProperty.GetValue(psgs.GetValue(0), null)
                    == 5250000 &&
                (int)frequencyProperty.GetValue(psgs.GetValue(1), null) == 1750000;

            prescaleProgram.Clear();
            AddPortWrite(prescaleProgram, 0xFFFD, 0xFA);
            AddPortWrite(prescaleProgram, 0xFFFD, 0x2D);
            prescaleProgram.Add(0x18); prescaleProgram.Add(0xFE);
            for (int i = 0; i < prescaleProgram.Count; i++)
                memory.WRMEM_DBG((ushort)(start + i), prescaleProgram[i]);
            machine.CPU.regs.PC = start;
            machine.ExecuteFrame();
            prescaleOk &= (int)frequencyProperty.GetValue(
                psgs.GetValue(0), null) == 1750000;
        }

        private static bool TestSaaExternalCycles(ISoundRenderer renderer,
            bool repeatAddress = false)
        {
            var type = renderer.GetType();
            var native = type.GetNestedType("SaaSoundNative", BindingFlags.NonPublic);
            Func<string, object[], object> call = (name, args) =>
                native.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, args);
            var reference = (IntPtr)call("Create", new object[0]);
            var actual = (IntPtr)type.GetField("m_externalSound",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(renderer);
            try
            {
                foreach (var sound in new IntPtr[] { reference, actual })
                {
                    call("SetSoundParameters", new object[] { sound, (uint)0x3F });
                    call("SetClockRate", new object[] { sound, (uint)8000000 });
                    call("SetSampleRate", new object[] { sound, (uint)44100 });
                    call("SetOversample", new object[] { sound, (uint)6 });
                    call("SetHighpass", new object[] { sound, false });
                    call("Clear", new object[] { sound });
                }
                type.GetMethod("ResetChip").Invoke(renderer, null);
                var address = type.GetProperty("RegAddr");
                var writeData = type.GetMethod("SetData");
                var passed = true;
                // Externally clocked repeating decay on envelope channel 2.
                byte[] registers = { 2, 0x14, 0x18, 0x1C };
                byte[] values = { 0xFF, 0, 0xA6, 1 };
                for (var i = 0; i < registers.Length; i++)
                {
                    address.SetValue(renderer, registers[i], null);
                    writeData.Invoke(renderer, new object[] { values[i] });
                    call("WriteAddress", new object[] { reference, registers[i] });
                    call("WriteData", new object[] { reference, values[i] });
                }
                for (var pulse = 0; pulse < 40; pulse++)
                {
                    address.SetValue(renderer, (byte)0x18, null);
                    if (repeatAddress)
                        type.GetMethod("SetReg").Invoke(renderer,
                            new object[] { 0x18, (byte)0xA6 });
                    else
                        writeData.Invoke(renderer, new object[] { (byte)0xA6 });
                    call("WriteAddress", new object[] { reference, (byte)0x18 });
                    call("WriteData", new object[] { reference, (byte)0xA6 });
                    var expected = new byte[256];
                    var observed = new byte[256];
                    call("GenerateMany", new object[] { reference, expected, (uint)64 });
                    call("GenerateMany", new object[] { actual, observed, (uint)64 });
                    for (var i = 0; i < expected.Length; i++)
                        passed &= expected[i] == observed[i];
                }
                return passed;
            }
            finally
            {
                call("SetHighpass", new object[] { actual, true });
                call("SetSampleRate", new object[] { actual, (uint)renderer.SampleRate });
                call("Destroy", new object[] { reference });
            }
        }

        private static double MeasureToneFrequency(
            System.Collections.Generic.List<short> samples, int sampleRate)
        {
            long sum = 0;
            foreach (var sample in samples) sum += sample;
            var mean = (double)sum / samples.Count;
            var edges = 0;
            for (var i = 1; i < samples.Count; i++)
                if (samples[i - 1] < mean && samples[i] >= mean) edges++;
            return (double)edges * sampleRate / samples.Count;
        }

        private static void TestOmniPsgEnvelopes()
        {
            var chip = new ZXMAK2.Hardware.Circuits.Sound.PsgChip();
            chip.ChipFrequency = 1750000;
            chip.AmpType = ZXMAK2.Hardware.Circuits.Sound.AmpType.Ym2203;
            var envelope = chip.GetType().GetField("m_env",
                BindingFlags.Instance | BindingFlags.NonPublic);
            for (var shape = 0; shape < 16; shape++)
            {
                chip.Reset();
                chip.SetReg(7, 0x3F);
                chip.SetReg(8, 0x10);
                chip.SetReg(11, 1);
                chip.SetReg(13, (byte)shape);
                var step = 0;
                chip.UpdateHandler = delegate(double time, ushort left, ushort right)
                {
                    step++;
                    int expected;
                    if (shape < 4 || shape == 9)
                        expected = Math.Max(0, 31 - step);
                    else if (shape < 8 || shape == 15)
                        expected = step < 32 ? step : 0;
                    else if (shape == 8)
                        expected = 31 - step % 32;
                    else if (shape == 10)
                        expected = step % 64 < 32 ? 31 - step % 64 : step % 64 - 32;
                    else if (shape == 11)
                        expected = step < 32 ? 31 - step : 31;
                    else if (shape == 12)
                        expected = step % 32;
                    else if (shape == 13)
                        expected = Math.Min(31, step);
                    else
                        expected = step % 64 < 32 ? step % 64 : 63 - step % 64;
                    if ((int)envelope.GetValue(chip) != expected)
                        throw new InvalidOperationException("YM envelope shape " + shape +
                            " diverged at step " + step);
                };
                chip.Update(0, 96 * 50D * 8 / chip.ChipFrequency - 1e-12);
                if (step != 96)
                    throw new InvalidOperationException("YM envelope clock divider changed");
            }
            Console.WriteLine("YM2203 SSG: all 16 envelope shapes, 96 steps each: PASS");
        }

        private static void TestZxMultiSoundMax()
        {
            var board = new ZXMAK2.Hardware.Evo.ZxMultiSoundMaxDevice
            {
                TsFmSaaPortCompatibility = true,
            };
            var config = new XmlDocument();
            var node = config.AppendChild(config.CreateElement("Device"));
            board.SaveConfigXml(node);
            var restored = new ZXMAK2.Hardware.Evo.ZxMultiSoundMaxDevice();
            restored.LoadConfigXml(node);
            var distinct = board.GetType() !=
                typeof(ZXMAK2.Hardware.Evo.ZxMultiSoundDevice) &&
                restored.TsFmSaaPortCompatibility;

            var priorityProbe = new ZXMAK2.Hardware.Evo.ZxMultiSoundMaxDevice();
            priorityProbe.ResolveConfiguration(false, false, true);
            var priority = !priorityProbe.EffectiveYmEnabled &&
                !priorityProbe.EffectiveSaaEnabled &&
                !priorityProbe.EffectiveGeneralSoundEnabled &&
                !priorityProbe.EffectiveSoundDriveEnabled;

            IMemoryDevice memory =
                new ZXMAK2.Hardware.Spectrum.MemorySpectrum48();
            var ula = new ZXMAK2.Hardware.Spectrum.UlaSpectrum48();
            var machine = GetTestMachine(Resources.machines_test);
            machine.BusManager.Disconnect();
            machine.BusManager.Clear();
            machine.BusManager.Add((BusDeviceBase)memory);
            machine.BusManager.Add((BusDeviceBase)ula);
            machine.BusManager.Add(board);
            if (!machine.BusManager.Connect())
                throw new InvalidOperationException("MultiSound Max connect failed");

            var core = typeof(ZXMAK2.Hardware.Evo.ZxMultiSoundCore);
            var ram = (byte[])core.GetField("m_gsRam",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(board);
            var rom = (byte[])core.GetField("m_gsRom",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(board);
            var memoryOk = ram.Length == 2 * 1024 * 1024 &&
                rom[0] == 0xF3 && rom[1] == 0xC3;

            const ushort start = 0x4000;
            var program = new System.Collections.Generic.List<byte>();
            // TSFM-compatible SAA selector and voice.
            AddPortWrite(program, 0xFFFD, 0xF7);
            AddAyWrite(program, 0x00, 0xFF);
            AddAyWrite(program, 0x08, 0x80);
            AddAyWrite(program, 0x10, 0x03);
            AddAyWrite(program, 0x14, 0x01);
            AddAyWrite(program, 0x1C, 0x01);
            // One audible OPL3 FM voice on the Max-specific #C4/#C5 ports.
            AddMoonSoundFmWrite(program, 0x34C4, 0x20, 0x01);
            AddMoonSoundFmWrite(program, 0x34C4, 0x23, 0x01);
            AddMoonSoundFmWrite(program, 0x34C4, 0x40, 0x10);
            AddMoonSoundFmWrite(program, 0x34C4, 0x43, 0x00);
            AddMoonSoundFmWrite(program, 0x34C4, 0x60, 0xF0);
            AddMoonSoundFmWrite(program, 0x34C4, 0x63, 0xF0);
            AddMoonSoundFmWrite(program, 0x34C4, 0x80, 0x77);
            AddMoonSoundFmWrite(program, 0x34C4, 0x83, 0x77);
            AddMoonSoundFmWrite(program, 0x34C4, 0xC0, 0x30);
            AddMoonSoundFmWrite(program, 0x34C4, 0xA0, 0x98);
            AddMoonSoundFmWrite(program, 0x34C4, 0xB0, 0x31);
            program.Add(0x18); program.Add(0xFE);
            for (var i = 0; i < program.Count; i++)
                memory.WRMEM_DBG((ushort)(start + i), program[i]);
            machine.IsRunning = false;
            machine.DebugReset();
            machine.CPU.regs.PC = start;
            machine.ExecuteFrame();

            var renderers = new System.Collections.Generic.List<ISoundRenderer>(
                board.SoundRenderers);
            var saaPeak = GetStereoPeak(renderers[3].AudioBuffer);
            var oplPeak = GetStereoPeak(renderers[5].AudioBuffer);
            memory.WRMEM_DBG(0x4600, 0x18);
            memory.WRMEM_DBG(0x4601, 0xFE);
            machine.CPU.regs.PC = 0x4600;
            for (var frame = 0; frame < 200; frame++)
                machine.ExecuteFrame();
            var readyProgram = new System.Collections.Generic.List<byte>();
            AddPortReadAndStore(readyProgram, 0x00BB, 0x4700);
            for (var i = 0; i < readyProgram.Count; i++)
                memory.WRMEM_DBG((ushort)(0x4650 + i), readyProgram[i]);
            machine.CPU.regs.PC = 0x4650;
            while (machine.CPU.regs.PC < 0x4650 + readyProgram.Count)
                machine.DebugStepInto();
            var gsReady = (memory.RDMEM_DBG(0x4700) & 1) == 0;
            machine.BusManager.Disconnect();
            machine.Dispose();
            var passed = distinct && priority && memoryOk &&
                saaPeak > 256 && oplPeak > 256 && gsReady;
            Console.WriteLine(
                "ZX-MultiSound Max: distinct={0}, ports={1}, GS-2MB={2}, " +
                "SAA={3}, OPL3={4}, GS-ready={5}: {6}",
                distinct, priority, memoryOk, saaPeak, oplPeak, gsReady,
                passed ? "PASS" : "FAIL");
            if (!passed)
                Environment.ExitCode = 1;
        }

        private static void TestOmniMaxOplCompatibility()
        {
            for (int dos = 0; dos < 2; ++dos)
            {
                byte[] referenceStatus = null;
                uint[] referenceAudio = null;
                for (int card = 0; card < 2; ++card)
                {
                    var omni = card == 1
                        ? new ZXMAK2.Hardware.Evo.ZxOmniSoundDevice() : null;
                    ZXMAK2.Hardware.Evo.ZxMultiSoundCore board = omni != null
                        ? (ZXMAK2.Hardware.Evo.ZxMultiSoundCore)omni
                        : new ZXMAK2.Hardware.Evo.ZxMultiSoundMaxDevice();
                    IMemoryDevice memory = new ZXMAK2.Hardware.Spectrum.MemorySpectrum48();
                    var machine = GetTestMachine(Resources.machines_test);
                    machine.BusManager.Disconnect();
                    machine.BusManager.Clear();
                    machine.BusManager.Add((BusDeviceBase)memory);
                    machine.BusManager.Add(new ZXMAK2.Hardware.Spectrum.UlaSpectrum48());
                    machine.BusManager.Add(board);
                    if (!machine.BusManager.Connect())
                        throw new InvalidOperationException("OPL comparison connect failed");
                    if (omni != null)
                    {
                        var hostMemory = new ZXMAK2.Hardware.Evo.MemoryPentEvo();
                        typeof(ZXMAK2.Hardware.MemoryBase).GetField("m_dosen",
                            BindingFlags.Instance | BindingFlags.NonPublic)
                            .SetValue(hostMemory, dos != 0);
                        typeof(ZXMAK2.Hardware.Evo.ZxmMoonSoundDevice)
                            .GetField("m_hostMemory", BindingFlags.Instance | BindingFlags.NonPublic)
                            .SetValue(omni.MoonSound, hostMemory);
                    }
                    var program = new System.Collections.Generic.List<byte>();
                    AddPortReadAndStore(program, 0x12C4, 0x6000);
                    AddPortReadAndStore(program, 0x34C6, 0x6001);
                    AddPortReadAndStore(program, 0x56C5, 0x6002);
                    AddPortReadAndStore(program, 0x78C7, 0x6003);
                    // Standard OPL detection reset and OPL3 mode selection.
                    AddMoonSoundFmWrite(program, 0x12C4, 0x04, 0x60);
                    AddMoonSoundFmWrite(program, 0x12C4, 0x04, 0x80);
                    AddPortReadAndStore(program, 0x12C4, 0x6004);
                    AddMoonSoundFmWrite(program, 0x34C6, 0x05, 0x01);
                    AddPortReadAndStore(program, 0x34C6, 0x6005);
                    AddPortReadAndStore(program, 0x12C4, 0x6006);
                    // Exercise voices in both OPL3 register banks.
                    for (int bank = 0; bank < 2; ++bank)
                    {
                        ushort port = (ushort)(bank == 0 ? 0x12C4 : 0x34C6);
                        AddMoonSoundFmWrite(program, port, 0x20, 0x01);
                        AddMoonSoundFmWrite(program, port, 0x23, 0x01);
                        AddMoonSoundFmWrite(program, port, 0x40, 0x10);
                        AddMoonSoundFmWrite(program, port, 0x43, 0x00);
                        AddMoonSoundFmWrite(program, port, 0x60, 0xF0);
                        AddMoonSoundFmWrite(program, port, 0x63, 0xF0);
                        AddMoonSoundFmWrite(program, port, 0x80, 0x77);
                        AddMoonSoundFmWrite(program, port, 0x83, 0x77);
                        AddMoonSoundFmWrite(program, port, 0xC0, (byte)(bank == 0 ? 0x10 : 0x20));
                        AddMoonSoundFmWrite(program, port, 0xA0, (byte)(bank == 0 ? 0x98 : 0x62));
                        AddMoonSoundFmWrite(program, port, 0xB0, 0x31);
                    }
                    AddMoonSoundFmWrite(program, 0x12C4, 0x02, 0xFF);
                    AddMoonSoundFmWrite(program, 0x12C4, 0x04, 0x01);
                    program.Add(0x18); program.Add(0xFE);
                    for (int i = 0; i < program.Count; ++i)
                        memory.WRMEM_DBG((ushort)(0x4000 + i), program[i]);
                    machine.IsRunning = false;
                    machine.DebugReset();
                    machine.CPU.regs.PC = 0x4000;
                    var renderers = new System.Collections.Generic.List<ISoundRenderer>(board.SoundRenderers);
                    ISoundRenderer renderer = omni != null ? omni.MoonSound : renderers[5];
                    var audio = new System.Collections.Generic.List<uint>();
                    for (int frame = 0; frame < 4; ++frame)
                    {
                        machine.ExecuteFrame();
                        audio.AddRange(renderer.AudioBuffer);
                    }
                    program.Clear();
                    AddPortReadAndStore(program, 0x12C4, 0x6007);
                    AddMoonSoundFmWrite(program, 0x12C4, 0x04, 0x80);
                    AddPortReadAndStore(program, 0x34C6, 0x6008);
                    for (int i = 0; i < program.Count; ++i)
                        memory.WRMEM_DBG((ushort)(0x5000 + i), program[i]);
                    machine.CPU.regs.PC = 0x5000;
                    while (machine.CPU.regs.PC < 0x5000 + program.Count)
                        machine.DebugStepInto();
                    var status = new byte[9];
                    for (int i = 0; i < status.Length; ++i)
                        status[i] = memory.RDMEM_DBG((ushort)(0x6000 + i));
                    if (card == 0)
                    {
                        referenceStatus = status;
                        referenceAudio = audio.ToArray();
                    }
                    else
                    {
                        bool equal = status[0] == 0 && status[2] == 0xFF &&
                            status[3] == 0xFF && (status[7] & 0xC0) == 0xC0 &&
                            (status[8] & 0xE0) == 0 && audio.Count == referenceAudio.Length &&
                            GetStereoPeak(renderer.AudioBuffer) > 64;
                        for (int i = 0; i < status.Length; ++i)
                            equal &= status[i] == referenceStatus[i];
                        for (int i = 0; equal && i < audio.Count; ++i)
                            equal &= audio[i] == referenceAudio[i];
                        Console.WriteLine("Omni/Max OPL3 DOSEN={0}: status={1}, stereo PCM identical={2}: {3}",
                            dos, BitConverter.ToString(status), equal, equal ? "PASS" : "FAIL");
                        if (!equal) Environment.ExitCode = 1;
                    }
                    machine.BusManager.Disconnect();
                    machine.Dispose();
                }
            }
        }

        private static void TestMultiSoundMidi()
        {
            for (int card = 0; card < 3; ++card)
            {
                ZXMAK2.Hardware.Evo.ZxMultiSoundCore board = card == 0
                    ? (ZXMAK2.Hardware.Evo.ZxMultiSoundCore)
                        new ZXMAK2.Hardware.Evo.ZxMultiSoundDevice()
                    : card == 1
                        ? (ZXMAK2.Hardware.Evo.ZxMultiSoundCore)
                            new ZXMAK2.Hardware.Evo.ZxMultiSoundMaxDevice()
                        : new ZXMAK2.Hardware.Evo.ZxOmniSoundDevice();
                IMemoryDevice memory =
                    new ZXMAK2.Hardware.Spectrum.MemorySpectrum48();
                var machine = GetTestMachine(Resources.machines_test);
                machine.BusManager.Disconnect();
                machine.BusManager.Clear();
                machine.BusManager.Add((BusDeviceBase)memory);
                machine.BusManager.Add(new ZXMAK2.Hardware.Spectrum.UlaSpectrum48());
                machine.BusManager.Add(board);
                if (!machine.BusManager.Connect())
                    throw new InvalidOperationException("MIDI test board did not connect");
                var renderers = new System.Collections.Generic.List<ISoundRenderer>(
                    board.SoundRenderers);
                var midi = renderers[4];
                for (int chip = 0; chip < 2; ++chip)
                {
                    const ushort start = 0x4000;
                    var program = new System.Collections.Generic.List<byte>();
                    AddPortWrite(program, 0xFFFD, (byte)(0xFE | chip));
                    AddAyWrite(program, 0x07, 0xFC); // AY port A output
                    AddPortWrite(program, 0xFFFD, 0x0E);
                    AddSerialMidiByte(program, 0xC0); // piano program
                    AddSerialMidiByte(program, 0x00);
                    AddSerialMidiByte(program, 0x90); // note on
                    AddSerialMidiByte(program, 60);
                    AddSerialMidiByte(program, 100);
                    program.Add(0x18);
                    program.Add(0xFE);
                    for (int i = 0; i < program.Count; ++i)
                        memory.WRMEM_DBG((ushort)(start + i), program[i]);
                    machine.IsRunning = false;
                    machine.DebugReset();
                    machine.CPU.regs.PC = start;
                    int peak = 0;
                    for (int frame = 0; frame < 3; ++frame)
                    {
                        machine.ExecuteFrame();
                        peak = Math.Max(peak, GetStereoPeak(midi.AudioBuffer));
                    }
                    Console.WriteLine("{0} MIDI AY chip {1} peak={2}: {3}",
                        board.Name, chip + 1, peak,
                        peak > 100 ? "PASS" : "FAIL");
                    if (peak <= 100)
                        Environment.ExitCode = 1;
                }
                machine.BusManager.Disconnect();
                machine.Dispose();
            }
        }

        private static void AddSerialMidiByte(
            System.Collections.Generic.List<byte> program, byte value)
        {
            AddPortWrite(program, 0xBFFD, 0xFA); // start, A2 low
            for (int bit = 0; bit < 8; ++bit)
                AddPortWrite(program, 0xBFFD,
                    (byte)((value & (1 << bit)) != 0 ? 0xFE : 0xFA));
            AddPortWrite(program, 0xBFFD, 0xFE); // stop, A2 high
        }

        private static void TestEvoDisplayModes()
        {
            var cmos = new ZXMAK2.Hardware.Evo.CmosPentEvo();
            for (var mode = 0; mode < 8; mode++)
            {
                cmos.DisplayMode = mode;
                var expected = (byte)(((mode & 3) << 4) |
                    ((mode >> 2) & 1));
                if (cmos.DisplayMode != mode ||
                    cmos.AvrVideoConfiguration != expected)
                    throw new InvalidOperationException(
                        "EVO display mode mapping failed at " + mode);
            }
            Console.WriteLine("EVO TV/VGA x 4 raster modes: PASS");
        }

        private static void TestMasterVolume()
        {
            var weighted = new FrameSound(50,
                new uint[][] { new uint[] { PackStereoSample(10000) },
                    new uint[] { PackStereoSample(2000) } },
                false, 100, new int[] { 140, 100 });
            if (ReadLeftSample(weighted) != 8000)
                throw new InvalidOperationException("Card gain changed the unrelated source");
            var cancelled = new FrameSound(50,
                new uint[][] { new uint[] { PackStereoSample(30000) },
                    new uint[] { PackStereoSample(-30000) } },
                false, 100, new int[] { 140, 140 });
            if (ReadLeftSample(cancelled) != 0)
                throw new InvalidOperationException("Sources were limited before mixing");
            for (var sample = -32768; sample <= 32767; sample += 127)
            {
                var result = ReadLeftSample(new FrameSound(50,
                    new uint[][] { new uint[] { PackStereoSample((short)sample) } },
                    false, 100, new int[] { 140 }));
                if (Math.Sign(result) != Math.Sign(sample) ||
                    (Math.Abs(sample) < 17000 && result != sample * 140 / 100))
                    throw new InvalidOperationException("Wide card gain wrapped or distorted low levels");
            }
            var source = new uint[] { PackStereoSample(10000) };
            var sources = new uint[][] { source, source };
            if (ReadLeftSample(new FrameSound(50, sources, false, 0)) != 0 ||
                ReadLeftSample(new FrameSound(50, sources, false, 50)) != 5000 ||
                ReadLeftSample(new FrameSound(50, sources, false, 100)) != 10000 ||
                ReadLeftSample(new FrameSound(50, sources, false, 150)) != 15000)
                throw new InvalidOperationException(
                    "Master volume must scale the final mix exactly once");

            var loud = new uint[] { PackStereoSample(30000) };
            var boosted = ReadLeftSample(new FrameSound(
                50, new uint[][] { loud }, false, 200));
            if (boosted <= 30000 || boosted >= short.MaxValue)
                throw new InvalidOperationException(
                    "Master volume soft limiter is not active");

            var machine = GetTestMachine(Resources.machines_test);
            machine.BusManager.MasterVolume = 150;
            var xml = new XmlDocument();
            var root = xml.AppendChild(xml.CreateElement("Bus"));
            machine.BusManager.SaveConfigXml(root);
            machine.BusManager.LoadConfigXml(root);
            var persisted = machine.BusManager.MasterVolume == 150 &&
                machine.BusManager.SoundFrame != null;
            machine.BusManager.Disconnect();
            machine.Dispose();
            if (!persisted)
                throw new InvalidOperationException(
                    "Master volume configuration did not survive reload");
            Console.WriteLine("Master volume: mix, boost, soft limit, XML: PASS");
        }

        private static uint PackStereoSample(short sample)
        {
            var packed = (uint)(ushort)sample;
            return packed | (packed << 16);
        }

        private static short ReadLeftSample(FrameSound frame)
        {
            return (short)(frame.GetBuffer()[0] & 0xFFFF);
        }

        private static bool TestMultiSoundTsFmSaaCompatibility()
        {
            var nativeDefault =
                !new ZXMAK2.Hardware.Evo.ZxMultiSoundDevice()
                    .TsFmSaaPortCompatibility;
            IMemoryDevice memory =
                new ZXMAK2.Hardware.Spectrum.MemorySpectrum48();
            var ula = new ZXMAK2.Hardware.Spectrum.UlaSpectrum48();
            var board = new ZXMAK2.Hardware.Evo.ZxMultiSoundDevice
            {
                AutomaticConfiguration = false,
                YmEnabled = false,
                SaaEnabled = true,
                GeneralSoundEnabled = false,
                SoundDriveEnabled = false,
                TsFmSaaPortCompatibility = true,
            };
            var config = new XmlDocument();
            var configNode = config.AppendChild(config.CreateElement("Device"));
            board.SaveConfigXml(configNode);
            var loadedBoard = new ZXMAK2.Hardware.Evo.ZxMultiSoundDevice();
            loadedBoard.LoadConfigXml(configNode);
            var configRoundTrip = loadedBoard.TsFmSaaPortCompatibility;
            var machine = GetTestMachine(Resources.machines_test);
            machine.BusManager.Disconnect();
            machine.BusManager.Clear();
            machine.BusManager.Add((BusDeviceBase)memory);
            machine.BusManager.Add((BusDeviceBase)ula);
            machine.BusManager.Add(board);
            machine.BusManager.Connect();

            const ushort programAddress = 0x4000;
            var program = new System.Collections.Generic.List<byte>();
            AddPortWrite(program, 0xFFFD, 0xF7);
            AddAyWrite(program, 0x00, 0xFF);
            AddAyWrite(program, 0x08, 0x80);
            AddAyWrite(program, 0x10, 0x03);
            AddAyWrite(program, 0x14, 0x01);
            AddAyWrite(program, 0x1C, 0x01);
            for (var i = 0; i < program.Count; i++)
                memory.WRMEM_DBG((ushort)(programAddress + i), program[i]);

            machine.IsRunning = false;
            machine.DebugReset();
            machine.CPU.regs.PC = programAddress;
            while (machine.CPU.regs.PC < programAddress + program.Count)
                machine.DebugStepInto();
            machine.ExecuteFrame();

            var renderers =
                new System.Collections.Generic.List<ISoundRenderer>(
                    board.SoundRenderers);
            var audible = renderers.Count > 3 &&
                GetStereoPeak(renderers[3].AudioBuffer) > 256;
            machine.BusManager.Disconnect();
            machine.Dispose();
            return nativeDefault && configRoundTrip && audible;
        }

        private static bool TestMultiSoundGsDacTimeline(out int transitions)
        {
            IMemoryDevice memory =
                new ZXMAK2.Hardware.Spectrum.MemorySpectrum48();
            var ula = new ZXMAK2.Hardware.Spectrum.UlaSpectrum48();
            var board = new ZXMAK2.Hardware.Evo.ZxMultiSoundDevice
            {
                YmEnabled = false,
                SaaEnabled = false,
                SoundDriveEnabled = false,
                GeneralSoundEnabled = true,
            };
            var machine = GetTestMachine(Resources.machines_test);
            machine.BusManager.Disconnect();
            machine.BusManager.Clear();
            machine.BusManager.Add((BusDeviceBase)memory);
            machine.BusManager.Add((BusDeviceBase)ula);
            machine.BusManager.Add(board);
            if (!machine.BusManager.Connect())
                throw new InvalidOperationException("GS DAC test connect failed");

            machine.IsRunning = false;
            machine.DebugReset();
            memory.WRMEM_DBG(0x4000, 0x18); // JR $ keeps the host alive
            memory.WRMEM_DBG(0x4001, 0xFE);
            machine.CPU.regs.PC = 0x4000;

            var boardType = board.GetType().BaseType;
            var gsRam = (byte[])boardType.GetField(
                "m_gsRam", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(board);
            var gsCpu = (CpuUnit)boardType.GetField(
                "m_gsCpu", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(board);

            // Fixed GS RAM page: #4000 -> physical #C000, #6000 -> #E000.
            // This tiny private-Z80 loop alternates two PCM values on DAC 0.
            byte[] program =
            {
                0x3E, 0x3F,       // LD A,#3F
                0xD3, 0x06,       // OUT (#06),A: channel 0 volume
                0x3A, 0x00, 0x60, // LD A,(#6000): DAC sample #10
                0x06, 0x50,       // LD B,#50: audible-rate hold
                0x10, 0xFE,       // DJNZ $
                0x3A, 0x01, 0x60, // LD A,(#6001): DAC sample #F0
                0x06, 0x50,       // LD B,#50: audible-rate hold
                0x10, 0xFE,       // DJNZ $
                0xC3, 0x04, 0x40, // JP #4004
            };
            Array.Copy(program, 0, gsRam, 0xC000, program.Length);
            gsRam[0xE000] = 0x10;
            gsRam[0xE001] = 0xF0;
            gsCpu.regs.PC = 0x4000;
            machine.ExecuteFrame();

            transitions = CountLeftSignTransitions(board.AudioBuffer);
            machine.BusManager.Disconnect();
            machine.Dispose();
            return transitions > 20;
        }

        private static bool TestMultiSoundSoundDriveTimeline(
            out int transitions)
        {
            IMemoryDevice memory =
                new ZXMAK2.Hardware.Spectrum.MemorySpectrum48();
            var ula = new ZXMAK2.Hardware.Spectrum.UlaSpectrum48();
            var board = new ZXMAK2.Hardware.Evo.ZxMultiSoundDevice
            {
                YmEnabled = false,
                SaaEnabled = false,
                GeneralSoundEnabled = false,
                SoundDriveEnabled = true,
            };
            var machine = GetTestMachine(Resources.machines_test);
            machine.BusManager.Disconnect();
            machine.BusManager.Clear();
            machine.BusManager.Add((BusDeviceBase)memory);
            machine.BusManager.Add((BusDeviceBase)ula);
            machine.BusManager.Add(board);
            if (!machine.BusManager.Connect())
                throw new InvalidOperationException(
                    "SounDrive DAC test connect failed");

            const ushort start = 0x4000;
            byte[] program =
            {
                0x01, 0x0F, 0x00, // LD BC,#000F
                0x3E, 0x10,       // LD A,#10
                0xED, 0x79,       // OUT (C),A
                0x06, 0x28,       // LD B,#28: audible-rate hold
                0x10, 0xFE,       // DJNZ $
                0x3E, 0xF0,       // LD A,#F0
                0xED, 0x79,       // OUT (C),A
                0x06, 0x28,       // LD B,#28: audible-rate hold
                0x10, 0xFE,       // DJNZ $
                0xC3, 0x03, 0x40, // JP #4003
            };
            for (var i = 0; i < program.Length; i++)
                memory.WRMEM_DBG((ushort)(start + i), program[i]);
            machine.IsRunning = false;
            machine.DebugReset();
            machine.CPU.regs.PC = start;
            machine.ExecuteFrame();

            transitions = CountLeftSignTransitions(board.AudioBuffer);
            machine.BusManager.Disconnect();
            machine.Dispose();
            return transitions > 20;
        }

        private static int CountLeftSignTransitions(uint[] samples)
        {
            var transitions = 0;
            var previous = 0;
            var hasPrevious = false;
            foreach (var sample in samples)
            {
                var value = GetLeft(sample);
                var sign = value < 0 ? -1 : value > 0 ? 1 : 0;
                if (sign == 0)
                    continue;
                if (hasPrevious && sign != previous)
                    transitions++;
                previous = sign;
                hasPrevious = true;
            }
            return transitions;
        }

        private static uint PackStereo(short left, short right)
        {
            return (uint)((ushort)left | ((uint)(ushort)right << 16));
        }

        private static short GetLeft(uint sample)
        {
            return (short)(sample & 0xFFFF);
        }

        private static short GetRight(uint sample)
        {
            return (short)(sample >> 16);
        }

        private static void AddAyWrite(
            System.Collections.Generic.List<byte> program,
            byte register,
            byte value)
        {
            AddPortWrite(program, 0xFFFD, register);
            AddPortWrite(program, 0xBFFD, value);
        }

        private static void AddPortWrite(
            System.Collections.Generic.List<byte> program,
            ushort port,
            byte value)
        {
            program.Add(0x01);
            program.Add((byte)port);
            program.Add((byte)(port >> 8));
            program.Add(0x3E);
            program.Add(value);
            program.Add(0xED);
            program.Add(0x79);
        }

        private static void AddPortReadAndStore(
            System.Collections.Generic.List<byte> program,
            ushort port,
            ushort destination)
        {
            program.Add(0x01);
            program.Add((byte)port);
            program.Add((byte)(port >> 8));
            program.Add(0xED);
            program.Add(0x78);
            program.Add(0x32);
            program.Add((byte)destination);
            program.Add((byte)(destination >> 8));
        }

		#region ULA PATTERNS

		private static int[] s_patternUla48_Late_NOP = new int[]
        {
            14300, 14304, 14308, 14312, 14316, 14320, 14324, 14328, 14332, 
            14336, 14346, 14354, 14362, 14370, 14378, 14386, 14394, 14402, 14410, 14418, 14426, 14434, 14442, 14450, 14458, 14466, 14470, 14474, 14478, 14482, 14486, 14490, 14494, 14498, 
            14502, 14506, 14510, 14514, 14518, 14522, 14526, 14530, 14534, 14538, 14542, 14546, 14550, 14554, 14558, 14562, 14570, 14578, 14586, 14594, 14602, 14610, 14618, 14626, 14634,
            14642, 14650, 14658, 14666, 14674, 14682, 14690, 14694, 14698, 14702, 14706, 14710, 14714, 14718, 14722, 14726, 14730, 14734, 14738, 14742, 14746, 14750, 14754, 14758, 14762,
            14766, 14770, 14774, 14778, 14782, 14786, 14794, 14802,
        };

		private static int[] s_patternUla48_Early_NOP = new int[]
        {
            14300, 14304, 14308, 14312, 14316, 14320, 14324, 14328, 14332,
            14336, 14345, 14353, 14361, 14369, 14377, 14385, 14393, 14401, 14409, 14417, 14425, 14433, 14441, 14449, 14457, 14465, 14469, 14473, 14477, 14481, 14485, 14489, 14493, 14497,
            14501, 14505, 14509, 14513, 14517, 14521, 14525, 14529, 14533, 14537, 14541, 14545, 14549, 14553, 14557, 14561, 14569, 14577, 14585, 14593, 14601, 14609, 14617, 14625, 14633,
            14641, 14649, 14657, 14665, 14673, 14681, 14689, 14693, 14697, 14701, 14705, 14709, 14713, 14717, 14721, 14725, 14729, 14733, 
        };

		private static int[] s_patternUla48_Late_DJNZ = new int[]
        {
            14300, 14313, 14326, 14351, 14383, 14415, 14447, 14467, 14480, 14493, 14506, 14519, 14532, 14545, 14558, 14591, 14623, 14655, 14687, 14700, 14713, 14726, 14739, 14752, 14765,
            14778, 14807, 14839, 14871, 14903, 14919, 14932, 14945, 14958, 14971, 14984, 14997, 15016, 15055, 15087, 15119, 15139, 15152, 15165, 15178, 15191, 15204, 15217, 15230, 15263,
            15295, 15327,
        };

		private static int[] s_patternUla48_Early_DJNZ = new int[]
        {
            14300, 14313, 14326, 14351, 14390, 14422, 14454, 14470, 14483, 14496, 14509, 14522, 14535, 14548, 14567, 14606, 14638, 14670, 14690, 14703, 14716, 14729, 14742, 14755, 14768,
            14781, 14814, 14846, 14878, 14910, 14923, 14936, 14949, 14962, 14975, 14988, 15001, 15030, 15062, 15094, 15126, 15142, 15155, 15168, 15181, 15194, 15207, 15220, 15239, 15278,
            15310,
        };

		private static int[] s_patternUla48_Late_INAFE = new int[]
        {
            14300, 14311, 14322, 14333, 14353, 14377, 14401, 14425, 14449, 14469, 14480, 14491, 14502, 14513, 14524, 14535, 14546, 14557, 14577, 14601, 14625, 14649, 14673, 14693, 14704,
            14715, 14726, 14737, 14748, 14759, 14770, 14781, 14801, 14825, 14849, 14873, 14897, 14917, 14928, 14939, 14950, 14961, 14972, 14983, 14994, 15005, 15025, 15049, 15073, 15097,
            15121, 15141, 15152,
        };

		private static int[] s_patternUla48_Early_INAFE = new int[]
        {
            14300, 14311, 14322, 14333, 14352, 14376, 14400, 14424, 14448, 14468, 14479, 14490, 14501, 14512, 14523, 14534, 14545, 14556, 14576, 14600, 14624, 14648, 14672, 14692, 14703,
            14714, 14725, 14736, 14747, 14758, 14769, 14780, 14800, 14824, 14848, 14872, 14896, 14916, 14927, 14938, 14949, 14960, 14971, 14982, 14993, 15004, 15024, 15048, 15072, 15096,
            15120, 15140, 15151, 
        };

		private static int[] s_patternUla48_Late_OUTAFE = new int[]
        {
            14300, 14311, 14322, 14333, 14354, 14378, 14402, 14426, 14450, 14469, 14480, 14491, 14502, 14513, 14524, 14535, 14546, 14557, 14578, 14602, 14626, 14650, 14674, 14693, 14704,
            14715, 14726, 14737, 14748, 14759, 14770, 14781, 14802, 14826, 14850, 14874, 14898, 14917, 14928, 14939, 14950, 14961, 14972, 14983, 14994, 15005, 15026, 15050, 15074, 15098,
            15122, 15141, 15152, 15163,
        };

		private static int[] s_patternUla48_Early_OUTAFE = new int[]
        {
            14300, 14311, 14322, 14333, 14353, 14377, 14401, 14425, 14449, 14468, 14479, 14490, 14501, 14512, 14523, 14534, 14545, 14556, 14577, 14601, 14625, 14649, 14673, 14692, 14703, 
            14714, 14725, 14736, 14747, 14758, 14769, 14780, 14801, 14825, 14849, 14873, 14897, 14916, 14927, 14938, 14949, 14960, 14971, 14982, 14993, 15004, 15025, 15049, 15073, 15097, 
            15121, 15140, 15151, 15162,
        };

		private static int[] s_patternUla48_Late_LDAHL = new int[]
        {
            14300, 14307, 14314, 14321, 14328, 14335, 14345, 14361, 14377, 14393, 14409, 14425, 14441, 14457, 14469, 14476, 14483, 14490, 14497, 14504, 14511, 14518, 14525, 14532, 14539,
            14546, 14553, 14560, 14577, 14593, 14609, 14625, 14641, 14657, 14673, 14689, 14696, 14703, 14710, 14717, 14724, 14731, 14738, 14745, 14752, 14759, 14766, 14773, 14780, 14793,
            14809, 14825, 14841, 14857, 14873,
        };

		private static int[] s_patternUla48_Early_LDAHL = new int[]
        {
            14300, 14307, 14314, 14321, 14328, 14335, 14352, 14368, 14384, 14400, 14416, 14432, 14448, 14464, 14471, 14478, 14485, 14492, 14499, 14506, 14513, 14520, 14527, 14534, 14541,
            14548, 14555, 14568, 14584, 14600, 14616, 14632, 14648, 14664, 14680, 14692, 14699, 14706, 14713, 14720, 14727, 14734, 14741, 14748, 14755, 14762, 14769, 14776, 14783, 14800, 
            14816, 14832, 14848, 14864, 14880,
        };

		private static int[] s_patternUla48_Late_INAC = new int[]
        {
            14300, 14312, 14324, 14336, 14362, 14386, 14410, 14434, 14458, 14474, 14486, 14498, 14510, 14522, 14534, 14546, 14558, 14578, 14602, 14626, 14650, 14674, 14694, 14706, 14718,
            14730, 14742, 14754, 14766, 14778, 14794, 14818, 14842, 14866, 14890, 14914, 14926, 14938, 14950, 14962, 14974, 14986, 14998, 15010,
        };

		private static int[] s_patternUla48_Early_INAC = new int[]
        {
            14300, 14312, 14324, 14336, 14361, 14385, 14409, 14433, 14457, 14473, 14485, 14497, 14509, 14521, 14533, 14545, 14557, 14577, 14601, 14625, 14649, 14673, 14693, 14705, 14717,
            14729, 14741, 14753, 14765, 14777, 14793, 14817, 14841, 14865, 14889, 14913, 14925, 14937, 14949, 14961, 14973, 14985, 14997, 15009,
        };

		private static int[] s_patternUla48_Late_OUTCA = new int[]
        {
            14300, 14312, 14324, 14336, 14362, 14386, 14410, 14434, 14458, 14474, 14486, 14498, 14510, 14522, 14534, 14546, 14558, 14578, 14602, 14626, 14650, 14674, 14694, 14706, 14718,
            14730, 14742, 14754, 14766, 14778, 14794, 14818, 14842, 14866, 14890, 14914, 14926, 14938, 14950, 14962, 14974, 14986, 14998, 15010, 15034,
        };

		private static int[] s_patternUla48_Early_OUTCA = new int[]
        {
            14300, 14312, 14324, 14336, 14361, 14385, 14409, 14433, 14457, 14473, 14485, 14497, 14509, 14521, 14533, 14545, 14557, 14577, 14601, 14625, 14649, 14673, 14693, 14705, 14717,
            14729, 14741, 14753, 14765, 14777, 14793, 14817, 14841, 14865, 14889, 14913, 14925, 14937, 14949, 14961, 14973, 14985, 14997, 15009, 15033,
        };

		private static int[] s_patternUla48_Late_BIT7A = new int[]
        {
            14300, 14308, 14316, 14324, 14332, 14346, 14362, 14378, 14394, 14410, 14426, 14442, 14458, 14470, 14478, 14486, 14494, 14502, 14510, 14518, 14526, 14534, 14542, 14550, 14558,
            14570, 14586, 14602, 14618, 14634, 14650, 14666, 14682, 14694, 14702, 14710, 14718, 14726, 14734, 14742, 14750, 14758, 14766, 14774, 14782, 14794,
        };

		private static int[] s_patternUla48_Early_BIT7A = new int[]
        {
            14300, 14308, 14316, 14324, 14332, 14345, 14361, 14377, 14393, 14409, 14425, 14441, 14457, 14469, 14477, 14485, 14493, 14501, 14509, 14517, 14525, 14533, 14541, 14549, 14557,
            14569, 14585, 14601, 14617, 14633, 14649, 14665, 14681, 14693, 14701, 14709, 14717, 14725, 14733, 14741, 14749, 14757, 14765, 14773, 14781, 14793,
        };

		private static int[] s_patternUla48_Late_BIT7HL = new int[]
        {
            14300, 14312, 14324, 14336, 14362, 14386, 14410, 14434, 14458, 14474, 14486, 14498, 14510, 14522, 14534, 14546, 14558, 14578, 14602, 14626, 14650, 14674, 14694, 14706, 14718,
            14730, 14742, 14754, 14766, 14778, 14794, 14818, 14842, 14866, 14890, 14914, 14926, 14938, 14950, 14962, 14974, 14986, 14998, 15010, 15034
        };

		private static int[] s_patternUla48_Early_BIT7HL = new int[]
        {
            14300, 14312, 14324, 14336, 14361, 14385, 14409, 14433, 14457, 14473, 14485, 14497, 14509, 14521, 14533, 14545, 14557, 14577, 14601, 14625, 14649, 14673, 14693, 14705, 14717,
            14729, 14741, 14753, 14765, 14777, 14793, 14817, 14841, 14865, 14889, 14913, 14925, 14937, 14949, 14961, 14973, 14985, 14997, 15009, 15033,
        };

		private static int[] s_patternUla48_Late_SET7HL = new int[]
        {
            14300, 14315, 14330, 14354, 14386, 14418, 14450, 14473, 14488, 14503, 14518, 14533, 14548, 14569, 14602, 14634, 14666, 14693, 14708, 14723, 14738, 14753, 14768, 14783, 14810,
            14842, 14874, 14906, 14925, 14940, 14955, 14970, 14985, 15000, 15026, 15058, 15090, 15122, 15145, 15160, 15175, 15190, 15205,
        };

		private static int[] s_patternUla48_Early_SET7HL = new int[]
        {
            14300, 14315, 14330, 14353, 14385, 14417, 14449, 14472, 14487, 14502, 14517, 14532, 14547, 14568, 14601, 14633, 14665, 14692, 14707, 14722, 14737, 14752, 14767, 14782, 14809,
            14841, 14873, 14905, 14924, 14939, 14954, 14969, 14984, 14999, 15025, 15057, 15089, 15121,
        };

		private static int[] s_patternUla128_NOP = new int[]
        {
			14362, 14372, 14380, 14388, 14396, 14404, 14412, 14420, 14428, 14436, 14444, 14452, 14460, 14468, 14476, 14484, 14492, 
			14496, 14500, 14504, 14508, 14512, 14516, 14520, 14524, 14528, 14532, 14536, 14540, 14544, 14548, 14552, 14556, 14560, 14564, 14568, 14572, 14576, 14580, 14584, 14588, 14592,
			14600, 14608, 14616, 14624, 14632, 14640, 14648, 14656, 14664, 14672, 14680, 14688, 14696, 14704, 14712, 14720, 14724, 
			14728, 14732, 14736,
        };

		private static int[] s_patternUla128_INCHL = new int[]
        {
			14362, 14378, 14394, 14410, 14426, 14442, 14458, 14474, 14490, 
			14496, 14502, 14508, 14514, 14520, 14526, 14532, 14538, 14544, 14550, 14556, 14562, 14568, 14574, 14580, 14586, 14598,
			14614, 14630, 14646, 14662, 14678, 14694, 14710, 14722, 14728,
			14734, 14740, 14746,
        };

		private static int[] s_patternUla128_LDA_HL_ = new int[]
        {
			14362, 14379, 14395, 14411, 14427, 14443, 14459, 14475, 14491, 
			14498, 14505, 14512, 14519, 14526, 14533, 14540, 14547, 14554, 14561, 14568, 14575, 14582, 14589, 14599,
			14615, 14631, 14647, 14663, 14679, 14695, 14711, 14723, 14730, 
			14737,
        };

		private static int[] s_patternUla128_OUTCA = new int[]
        {
			14362, 14388, 14402, 14434, 14450, 14476, 14490, 
			14502, 14508, 14520, 14526, 14538, 14544, 14556, 14562, 14574, 14580, 14592, 
			14606, 14638, 14654, 14680, 14694, 14720, 
			14726, 14738, 14744, 14756, 14762, 14774, 14780, 14792, 14798, 14810, 14816,
			14842, 14858, 14884, 14898, 14930, 14946, 14958, 14964, 14976, 14982,
        };

		#endregion

		private static void runZexall()
		{
            var p128 = GetTestMachine(Resources.machines_test);
			p128.IsRunning = true;
			p128.DebugReset();
			p128.ExecuteFrame();
			p128.IsRunning = false;
            foreach (var kbd in p128.BusManager.FindDevices<IKeyboardDevice>())
            {
                kbd.KeyboardState = new FakeKeyboardState(Key.Y);
            }

            using (Stream testStream = GetTestStream("zexall.sna"))
            {
                p128.BusManager.LoadManager.GetSerializer(Path.GetExtension("zexall.sna")).Deserialize(testStream);
            }
			p128.IsRunning = true;
			int frame;
			for (frame = 0; frame < 700000; frame++)
			{
				p128.ExecuteFrame();
				if (frame % 30000 == 0 || ((frame > 630000 && frame < 660000) && frame % 10000 == 0))
				{
					Console.WriteLine(string.Format("{0:D8}", frame));
                    p128.BusManager.LoadManager.SaveFileName(string.Format("{0:D8}.PNG", frame));
				}
			}
            p128.BusManager.LoadManager.SaveFileName(string.Format("{0:D8}.PNG", frame));
			p128.BusManager.Disconnect();
		}

        private static void runPerf()
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            var frameCount = 50 * 10;
            while (true)
            {
                ExecTests("zexall.sna", frameCount);
            }
        }

        private static void runCpu()
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            var frameCount = 50 * 10;
            while (true)
            {
                ExecCpuTests("zexall.sna", frameCount);
            }
        }

		private static void ExecTests(string testName, int frameCount)
		{
            var p128 = GetTestMachine(Resources.machines_test);
			p128.IsRunning = true;
			p128.DebugReset();
			p128.ExecuteFrame();

			p128.IsRunning = false;
			using (Stream testStream = GetTestStream(testName))
                p128.BusManager.LoadManager.GetSerializer(Path.GetExtension(testName)).Deserialize(testStream);
			p128.IsRunning = true;


			Stopwatch watch = new Stopwatch();
			watch.Start();
			for (int frame = 0; frame < frameCount; frame++)
				p128.ExecuteFrame();
			watch.Stop();
			Console.WriteLine("{0}:\t{1} [ms]", testName, watch.ElapsedMilliseconds);
			//p128.Loader.SaveFileName(testName);
			p128.BusManager.Disconnect();
		}

        private static void ExecLightTests(string testName, int frameCount)
        {
            var p128 = GetTestMachine(Resources.machines_testLight);
            p128.IsRunning = true;
            p128.DebugReset();
            p128.ExecuteFrame();

            p128.IsRunning = false;
            using (Stream testStream = GetTestStream(testName))
                p128.BusManager.LoadManager.GetSerializer(Path.GetExtension(testName)).Deserialize(testStream);
            p128.IsRunning = true;


            Stopwatch watch = new Stopwatch();
            watch.Start();
            for (int frame = 0; frame < frameCount; frame++)
                p128.ExecuteFrame();
            watch.Stop();
            Console.WriteLine("{0} [light]:\t{1} [ms]", testName, watch.ElapsedMilliseconds);
            //p128.Loader.SaveFileName(testName);
            p128.BusManager.Disconnect();
        }

        private static void ExecCpuTests(string testName, int frameCount)
        {
            var cpu = new CpuUnit();
            cpu.regs.PC = 0;
            cpu.RESET = () => { };
            cpu.NMIACK_M1 = () => { };
            cpu.INTACK_M1 = () => { };
            cpu.RDMEM_M1 = addr => (byte)addr;
            cpu.RDMEM = addr => (byte)addr;
            cpu.WRMEM = (addr,value) => { };
            cpu.RDPORT = addr => 0xFF;
            cpu.WRPORT = (addr, value) => { };
            cpu.RDNOMREQ = addr => { };
            cpu.WRNOMREQ = addr => { };

            Stopwatch watch = new Stopwatch();
            watch.Start();
            for (var cycle = 0L; cycle < 71980 * frameCount; cycle++)
                cpu.ExecCycle();
            watch.Stop();
            Console.WriteLine("{0} [cpu]:\t{1} [ms]", testName, watch.ElapsedMilliseconds);
        }

        private static void TestHostFolderSd()
        {
            var root = Path.Combine(
                ZXMAK2.PortablePaths.DataDirectory("Temp"),
                "zxmak2-host-folder-sd-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var games = Directory.CreateDirectory(Path.Combine(root, "Games"));
                var rootPayload = Encoding.ASCII.GetBytes("ZXMAK2-HOST-SD-ROOT");
                var nestedPayload = Encoding.ASCII.GetBytes("ZXMAK2-HOST-SD-LONG-NAME");
                File.WriteAllBytes(Path.Combine(root, "test.trd"), rootPayload);
                File.WriteAllText(Path.Combine(root, "SHOCK.SCL"), "DELETE");
                var bin = Directory.CreateDirectory(Path.Combine(root, "bin"));
                var autoexecPayload = Encoding.ASCII.GetBytes(
                    "wizcfg.com -S\r\nnv.com\r\n");
                File.WriteAllBytes(Path.Combine(bin.FullName, "autoexec.bat"),
                    autoexecPayload);
                File.WriteAllBytes(Path.Combine(bin.FullName, "wizcfg.com"),
                    Encoding.ASCII.GetBytes("WIZ"));
                File.WriteAllBytes(Path.Combine(bin.FullName, "nv.com"),
                    Encoding.ASCII.GetBytes("NV"));
                File.WriteAllBytes(Path.Combine(bin.FullName, "cmd.com"),
                    Encoding.ASCII.GetBytes("CMD"));
                File.WriteAllBytes(Path.Combine(bin.FullName, "term.com"),
                    Encoding.ASCII.GetBytes("TERM"));
                // Force BIN to span more than one directory cluster.  NedoOS
                // must still find its startup programs through the global
                // program search used before CMD changes into BIN.
                for (var index = 0; index < 150; index++)
                {
                    File.WriteAllBytes(Path.Combine(bin.FullName,
                        string.Format("tool{0:000}.com", index)),
                        new byte[] { (byte)index });
                }
                File.WriteAllBytes(
                    Path.Combine(games.FullName, "Very Long Spectrum Collection Name.tzx"),
                    nestedPayload);
                File.WriteAllText(
                    Path.Combine(games.FullName, "Very Long Spectrum Collection Number One.tap"),
                    "ONE");
                File.WriteAllText(
                    Path.Combine(games.FullName, "Very Long Spectrum Collection Number Two.tap"),
                    "TWO");

                using (var stream = new HostFolderFat32Stream(root))
                {
                    AssertHostFolderSd(stream.CanRead, "virtual SD is not readable");
                    AssertHostFolderSd(stream.CanWrite, "virtual SD is not writable");
                    AssertHostFolderSd((stream.Length % 512) == 0, "virtual SD is not sector aligned");
                    AssertHostFolderSd(stream.Length == 512L * 1024L * 1024L,
                        "small host folders should use a standard 512 MiB card");
                    var boot = ReadHostFolderSector(stream, 0);
                    AssertHostFolderSd(boot[510] == 0x55 && boot[511] == 0xAA,
                        "FAT32 boot signature is missing");
                    AssertHostFolderSd(ReadLe16(boot, 11) == 512,
                        "FAT32 sector size is invalid");
                    AssertHostFolderSd(Encoding.ASCII.GetString(boot, 82, 8) == "FAT32   ",
                        "FAT32 type marker is missing");

                    var rootEntries = ReadFatDirectory(stream, boot, 2);
                    var rootFile = FindFatEntry(rootEntries, "test.trd");
                    var gamesEntry = FindFatEntry(rootEntries, "Games");
                    var binEntry = FindFatEntry(rootEntries, "bin");
                    AssertHostFolderSd(rootFile != null, "root file is missing");
                    AssertHostFolderSd(gamesEntry != null && gamesEntry.IsDirectory,
                        "nested directory is missing");
                    AssertHostFolderSd(binEntry != null &&
                        binEntry.ShortName == "BIN", "NedoOS BIN has an invalid 8.3 name");
                    AssertHostFolderSd(rootEntries.Count > 0 &&
                        rootEntries[0].ShortName == "BIN" &&
                        !rootEntries[0].HasLongName,
                        "NedoOS BIN is not the first root directory entry");
                    AssertHostFolderSd(!binEntry.HasLongName &&
                        binEntry.CaseFlags == 0x08,
                        "lowercase BIN should use the FAT short-name case flag");
                    AssertHostFolderSd(ReadFatDirectoryParentCluster(stream,
                        boot, binEntry.Cluster) == 0,
                        "BIN must mark the FAT32 root as its parent with cluster zero");
                    AssertHostFolderSd(ReadFatDirectoryParentCluster(stream,
                        boot, gamesEntry.Cluster) == 0,
                        "root child must mark the FAT32 root as its parent with cluster zero");
                    var binEntries = ReadFatDirectory(stream, boot, binEntry.Cluster);
                    var autoexec = FindFatEntry(binEntries, "autoexec.bat");
                    var wizcfg = FindFatEntry(binEntries, "wizcfg.com");
                    var nv = FindFatEntry(binEntries, "nv.com");
                    AssertHostFolderSd(autoexec.ShortName == "AUTOEXEC.BAT" &&
                        wizcfg.ShortName == "WIZCFG.COM" &&
                        nv.ShortName == "NV.COM",
                        "NedoOS startup commands have invalid 8.3 names");
                    AssertHostFolderSd(!autoexec.HasLongName && !wizcfg.HasLongName &&
                        !nv.HasLongName && autoexec.CaseFlags == 0x18 &&
                        wizcfg.CaseFlags == 0x18 && nv.CaseFlags == 0x18,
                        "NedoOS startup commands should be plain 8.3 entries");
                    AssertHostFolderSd(IsEntryInFirstDirectoryCluster(stream,
                        boot, binEntry.Cluster, "WIZCFG  COM"),
                        "NedoOS WIZCFG is outside the first BIN directory cluster");
                    AssertHostFolderSd((autoexec.Attributes & 0x01) == 0 &&
                        (wizcfg.Attributes & 0x01) == 0 &&
                        (nv.Attributes & 0x01) == 0 &&
                        (rootFile.Attributes & 0x01) == 0,
                        "folder files must not have FAT read-only attributes");
                    AssertHostFolderSd(BytesEqual(ReadFatFile(stream, boot,
                        autoexec), autoexecPayload),
                        "NedoOS startup batch content differs");
                    AssertHostFolderSd(BytesEqual(
                        ReadFatFile(stream, boot, rootFile), rootPayload),
                        "root file data differs");

                    var nestedEntries = ReadFatDirectory(stream, boot, gamesEntry.Cluster);
                    var nestedFile = FindFatEntry(
                        nestedEntries,
                        "Very Long Spectrum Collection Name.tzx");
                    AssertHostFolderSd(nestedFile != null,
                        "long filename entry is missing");
                    AssertHostFolderSd(BytesEqual(
                        ReadFatFile(stream, boot, nestedFile), nestedPayload),
                        "long filename file data differs");

                    var aliasOne = FindFatEntry(
                        nestedEntries,
                        "Very Long Spectrum Collection Number One.tap");
                    var aliasTwo = FindFatEntry(
                        nestedEntries,
                        "Very Long Spectrum Collection Number Two.tap");
                    AssertHostFolderSd(aliasOne != null && aliasTwo != null,
                        "colliding long filenames are missing");
                    AssertHostFolderSd(
                        !string.Equals(aliasOne.ShortName, aliasTwo.ShortName,
                            StringComparison.OrdinalIgnoreCase),
                        "colliding long filenames received the same 8.3 alias");

                    File.WriteAllText(Path.Combine(root, "added-after-mount.tap"), "NEW");
                    rootEntries = ReadFatDirectory(stream, boot, 2);
                    AssertHostFolderSd(
                        FindFatEntry(rootEntries, "added-after-mount.tap") == null,
                        "mounted metadata snapshot changed without Refresh");

                    File.Delete(Path.Combine(
                        games.FullName,
                        "Very Long Spectrum Collection Name.tzx"));
                    var missingData = ReadFatFile(stream, boot, nestedFile);
                    AssertHostFolderSd(BytesEqual(
                        missingData,
                        new byte[nestedPayload.Length]),
                        "disappeared host file was not handled safely");
                }

                using (var refreshed = new HostFolderFat32Stream(root))
                {
                    var boot = ReadHostFolderSector(refreshed, 0);
                    var rootEntries = ReadFatDirectory(refreshed, boot, 2);
                    AssertHostFolderSd(
                        FindFatEntry(rootEntries, "added-after-mount.tap") != null,
                        "new host file did not appear after Refresh snapshot");
                }

                var originalHostData = File.ReadAllBytes(Path.Combine(root, "test.trd"));
                using (var writable = new HostFolderFat32Stream(root))
                {
                    var boot = ReadHostFolderSector(writable, 0);
                    var file = FindFatEntry(ReadFatDirectory(writable, boot, 2), "test.trd");
                    var dataStart = ReadLe16(boot, 14) +
                        (long)boot[16] * ReadLe32(boot, 36);
                    var fileSector = dataStart +
                        (file.Cluster - 2) * boot[13];
                    var sector = ReadHostFolderSector(writable, fileSector);
                    sector[0] = (byte)'Q';
                    writable.Position = fileSector * 512;
                    writable.Write(sector, 0, sector.Length);
                    AssertHostFolderSd(writable.HasChanges,
                        "SD write did not mark the session changed");
                    AssertHostFolderSd(BytesEqual(
                        File.ReadAllBytes(Path.Combine(root, "test.trd")),
                        originalHostData),
                        "SD write touched the source folder before confirmation");
                    var journal = (string)typeof(HostFolderFat32Stream).GetField(
                        "m_journalPath", System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic).GetValue(writable);
                    AssertHostFolderSd(File.Exists(journal) && journal.StartsWith(
                        SdSessionStorage.GetDirectory("HostFolders") +
                        Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
                        "host-folder journal is not portable");
                    writable.CommitChanges();
                    AssertHostFolderSd(!writable.HasChanges && !File.Exists(journal),
                        "accepted save did not clear the folder journal");
                }
                using (var fresh = new HostFolderFat32Stream(root))
                {
                    AssertHostFolderSd(!fresh.HasChanges && !fresh.WasRecovered,
                        "new folder mount must not recover a previous session");
                }
                AssertHostFolderSd(File.ReadAllBytes(
                    Path.Combine(root, "test.trd"))[0] == (byte)'Q',
                    "confirmed SD write was not saved to the source folder");

                // Simulate a crashed/copied journal, then change the PC folder.
                // A fresh mount must use PC data even with that journal present.
                string abandonedJournal = null;
                try
                {
                    using (var abandoned = new HostFolderFat32Stream(root))
                    {
                        var sector = ReadHostFolderSector(abandoned, 0);
                        sector[3] ^= 1;
                        abandoned.Position = 0;
                        abandoned.Write(sector, 0, sector.Length);
                        var journal = (string)typeof(HostFolderFat32Stream).GetField(
                            "m_journalPath", System.Reflection.BindingFlags.Instance |
                            System.Reflection.BindingFlags.NonPublic).GetValue(abandoned);
                        abandonedJournal = Path.Combine(Path.GetDirectoryName(journal),
                            Guid.NewGuid().ToString("N") + ".journal");
                        File.Copy(journal, abandonedJournal);
                    }
                    File.WriteAllText(Path.Combine(root, "pc-after-crash.txt"), "CURRENT");
                    using (var fresh = new HostFolderFat32Stream(root))
                    {
                        AssertHostFolderSd(!fresh.HasChanges && !fresh.WasRecovered &&
                            FindFatEntry(ReadFatDirectory(fresh,
                                ReadHostFolderSector(fresh, 0), 2), "pc-after-crash.txt") != null,
                            "stale journal blocked or overlaid changed PC folder");
                    }
                }
                finally
                {
                    if (abandonedJournal != null && File.Exists(abandonedJournal))
                        File.Delete(abandonedJournal);
                }

                using (var writable = new HostFolderFat32Stream(root))
                {
                    var boot = ReadHostFolderSector(writable, 0);
                    var reserved = ReadLe16(boot, 14);
                    var fatSectors = ReadLe32(boot, 36);
                    var dataStart = reserved + (long)boot[16] * fatSectors;
                    uint freeCluster = 2;
                    while (true)
                    {
                        var fat = ReadHostFolderSector(writable,
                            reserved + freeCluster / 128);
                        if (ReadLe32(fat, (int)(freeCluster % 128) * 4) == 0)
                        {
                            var position = (int)(freeCluster % 128) * 4;
                            fat[position] = 0xFF;
                            fat[position + 1] = 0xFF;
                            fat[position + 2] = 0xFF;
                            fat[position + 3] = 0x0F;
                            writable.Position = (reserved + freeCluster / 128) * 512L;
                            writable.Write(fat, 0, fat.Length);
                            break;
                        }
                        freeCluster++;
                    }
                    var data = ReadHostFolderSector(writable,
                        dataStart + (freeCluster - 2) * boot[13]);
                    Encoding.ASCII.GetBytes("NEW!").CopyTo(data, 0);
                    writable.Position =
                        (dataStart + (freeCluster - 2) * boot[13]) * 512;
                    writable.Write(data, 0, data.Length);

                    var rootSector = ReadHostFolderSector(writable, dataStart);
                    var entryOffset = 0;
                    while (rootSector[entryOffset] != 0)
                        entryOffset += 32;
                    Encoding.ASCII.GetBytes("CREATED TXT")
                        .CopyTo(rootSector, entryOffset);
                    rootSector[entryOffset + 11] = 0x20;
                    rootSector[entryOffset + 20] = (byte)(freeCluster >> 16);
                    rootSector[entryOffset + 21] = (byte)(freeCluster >> 24);
                    rootSector[entryOffset + 26] = (byte)freeCluster;
                    rootSector[entryOffset + 27] = (byte)(freeCluster >> 8);
                    rootSector[entryOffset + 28] = 4;
                    writable.Position = dataStart * 512;
                    writable.Write(rootSector, 0, rootSector.Length);
                    AssertHostFolderSd(!File.Exists(Path.Combine(root, "CREATED.TXT")),
                        "a guest-created file escaped before confirmation");
                    writable.CommitChanges();
                }
                AssertHostFolderSd(File.ReadAllText(Path.Combine(root,
                    "CREATED.TXT")) == "NEW!",
                    "guest-created FAT file was not saved to the folder");

                using (var writable = new HostFolderFat32Stream(root))
                {
                    var boot = ReadHostFolderSector(writable, 0);
                    var rootSector = ReadLe16(boot, 14) +
                        (long)boot[16] * ReadLe32(boot, 36);
                    var directory = ReadHostFolderSector(writable, rootSector);
                    var offset = 0;
                    while (offset < directory.Length &&
                        Encoding.ASCII.GetString(directory, offset, 11) !=
                            "CREATED TXT")
                        offset += 32;
                    AssertHostFolderSd(offset < directory.Length,
                        "new file was not visible before rename");
                    Encoding.ASCII.GetBytes("RENAMED TXT")
                        .CopyTo(directory, offset);
                    writable.Position = rootSector * 512;
                    writable.Write(directory, 0, directory.Length);
                    writable.CommitChanges();
                }
                AssertHostFolderSd(!File.Exists(Path.Combine(root, "CREATED.TXT")) &&
                    File.ReadAllText(Path.Combine(root, "RENAMED.TXT")) == "NEW!",
                    "guest rename was not saved to the folder");

                using (var writable = new HostFolderFat32Stream(root))
                {
                    var boot = ReadHostFolderSector(writable, 0);
                    var rootSector = ReadLe16(boot, 14) +
                        (long)boot[16] * ReadLe32(boot, 36);
                    var directory = ReadHostFolderSector(writable, rootSector);
                    var offset = 0;
                    while (offset < directory.Length &&
                        Encoding.ASCII.GetString(directory, offset, 11) !=
                            "RENAMED TXT")
                        offset += 32;
                    AssertHostFolderSd(offset < directory.Length,
                        "new file was not visible after remount");
                    directory[offset] = 0xE5;
                    writable.Position = rootSector * 512;
                    writable.Write(directory, 0, directory.Length);
                    writable.CommitChanges();
                }
                AssertHostFolderSd(!File.Exists(Path.Combine(root, "RENAMED.TXT")),
                    "guest deletion was not saved to the folder");

                using (var writable = new HostFolderFat32Stream(root))
                {
                    var boot = ReadHostFolderSector(writable, 0);
                    var rootSector = ReadLe16(boot, 14) +
                        (long)boot[16] * ReadLe32(boot, 36);
                    var directory = ReadHostFolderSector(writable, rootSector);
                    var offset = 0;
                    while (offset < directory.Length &&
                        Encoding.ASCII.GetString(directory, offset, 11) !=
                            "SHOCK   SCL")
                        offset += 32;
                    AssertHostFolderSd(offset < directory.Length &&
                        directory[offset + 11] == 0x20,
                        "pre-existing host file should be writable on FAT");
                    directory[offset] = 0xE5;
                    writable.Position = rootSector * 512;
                    writable.Write(directory, 0, directory.Length);
                    writable.CommitChanges();
                }
                AssertHostFolderSd(!File.Exists(Path.Combine(root, "SHOCK.SCL")),
                    "guest deletion of an existing host file was not saved");

                using (var writable = new HostFolderFat32Stream(root))
                {
                    var boot = ReadHostFolderSector(writable, 0);
                    var file = FindFatEntry(ReadFatDirectory(writable,
                        boot, 2), "test.trd");
                    var dataStart = ReadLe16(boot, 14) +
                        (long)boot[16] * ReadLe32(boot, 36);
                    var sectorNumber = dataStart +
                        (file.Cluster - 2) * boot[13];
                    var sector = ReadHostFolderSector(writable, sectorNumber);
                    sector[0] = (byte)'R';
                    writable.Position = sectorNumber * 512;
                    writable.Write(sector, 0, sector.Length);
                    File.WriteAllText(Path.Combine(root, "external-change.tap"),
                        "WINDOWS");
                    writable.CommitChanges();
                    AssertHostFolderSd(!writable.HasChanges &&
                        File.ReadAllText(Path.Combine(root,
                            "external-change.tap")) == "WINDOWS" &&
                        File.ReadAllBytes(Path.Combine(root, "test.trd"))[0] ==
                            (byte)'R',
                        "saving a guest file lost an unrelated PC addition");
                    writable.DiscardChanges();
                }

                var imagePath = Path.Combine(root, "session.img");
                File.WriteAllBytes(imagePath, new byte[1024]);
                using (var image = new SdImageSessionStream(
                    File.Open(imagePath, FileMode.Open, FileAccess.ReadWrite,
                        FileShare.Read), imagePath))
                {
                    image.Position = 100;
                    image.Write(new byte[] { 0x51, 0x52 }, 0, 2);
                    var journal = (string)typeof(SdImageSessionStream).GetField(
                        "m_journalPath", System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic).GetValue(image);
                    AssertHostFolderSd(File.Exists(journal) && journal.StartsWith(
                        SdSessionStorage.GetDirectory("Images") + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase), "SD image journal is not portable");
                    using (var original = new FileStream(imagePath,
                        FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        original.Position = 100;
                        AssertHostFolderSd(original.ReadByte() == 0,
                            "SD image was modified before confirmation");
                    }
                }
                using (var image = new SdImageSessionStream(
                    File.Open(imagePath, FileMode.Open, FileAccess.ReadWrite,
                        FileShare.Read), imagePath))
                {
                    AssertHostFolderSd(image.HasChanges,
                        "unsaved SD image session was not recovered");
                    image.CommitChanges();
                }
                AssertHostFolderSd(File.ReadAllBytes(imagePath)[100] == 0x51 &&
                    File.ReadAllBytes(imagePath)[101] == 0x52,
                    "confirmed SD image write was not applied");
                using (var image = new SdImageSessionStream(
                    File.Open(imagePath, FileMode.Open, FileAccess.ReadWrite,
                        FileShare.Read), imagePath))
                {
                    image.Position = 100;
                    image.Write(new byte[] { 0x58 }, 0, 1);
                    image.DiscardChanges();
                }
                AssertHostFolderSd(File.ReadAllBytes(imagePath)[100] == 0x51,
                    "discarded SD image changes reached the source");

                var configured = new ZXMAK2.Hardware.Evo.ZsdPentEvo();
                configured.ConfigureSource(
                    ZXMAK2.Hardware.Evo.SdSourceType.HostFolder,
                    root);
                var document = new XmlDocument();
                var configNode = document.AppendChild(document.CreateElement("Device"));
                configured.SaveConfigXml(configNode);
                var restored = new ZXMAK2.Hardware.Evo.ZsdPentEvo();
                restored.LoadConfigXml(configNode);
                AssertHostFolderSd(
                    restored.ConfiguredSourceType ==
                        ZXMAK2.Hardware.Evo.SdSourceType.HostFolder &&
                    string.Equals(restored.ConfiguredSourcePath, Path.GetFullPath(root),
                        StringComparison.OrdinalIgnoreCase),
                    "Host Folder source did not survive configuration round-trip");
                TestPortableSdStartup(root);
                Console.WriteLine("Host Folder SD FAT32: PASS");
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        private static void TestPortableSdStartup(string testRoot)
        {
            var flags = System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic;
            var restore = typeof(ZXMAK2.Hardware.Evo.ZsdPentEvo).GetMethod(
                "RestoreConfiguredCard", flags);
            var missing = Path.Combine(testRoot, "missing-source");
            var document = new XmlDocument();
            document.LoadXml("<Device sdSource='HostFolder' />");
            document.DocumentElement.SetAttribute("sdPath", missing);
            var device = new ZXMAK2.Hardware.Evo.ZsdPentEvo();
            device.LoadConfigXml(document.DocumentElement);
            restore.Invoke(device, null);
            AssertHostFolderSd(!device.IsMediaMounted &&
                string.IsNullOrEmpty(device.RestoreWarning) && !Directory.Exists(missing) &&
                device.ConfiguredSourcePath == missing,
                "missing folder did not silently leave an empty slot/retain its path");
            Directory.CreateDirectory(missing);
            File.WriteAllText(Path.Combine(missing, "now-present.txt"), "PC");
            restore.Invoke(device, null);
            AssertHostFolderSd(device.IsMediaMounted && string.IsNullOrEmpty(device.RestoreWarning),
                "returned PC folder was not mounted on a later startup");
            device.BusDisconnect();

            // Existing internal sources are saved relatively, independent of CWD.
            var appFolder = Path.GetDirectoryName(typeof(SdSessionStorage).Assembly.Location);
            var internalFolder = Path.Combine(appFolder, "sd-path-test-" + Guid.NewGuid().ToString("N"));
            var previousCwd = Environment.CurrentDirectory;
            Directory.CreateDirectory(internalFolder);
            try
            {
                device.ConfigureSource(ZXMAK2.Hardware.Evo.SdSourceType.HostFolder, internalFolder);
                document.LoadXml("<Device />");
                device.SaveConfigXml(document.DocumentElement);
                AssertHostFolderSd(!Path.IsPathRooted(document.DocumentElement.GetAttribute("sdPath")),
                    "internal SD folder was saved as an absolute PC path");
                Environment.CurrentDirectory = testRoot;
                var relocated = new ZXMAK2.Hardware.Evo.ZsdPentEvo();
                relocated.LoadConfigXml(document.DocumentElement);
                restore.Invoke(relocated, null);
                AssertHostFolderSd(relocated.ConfiguredSourcePath == internalFolder && relocated.IsMediaMounted,
                    "portable source resolution depends on the current directory");
                relocated.BusDisconnect();
            }
            finally
            {
                Environment.CurrentDirectory = previousCwd;
                Directory.Delete(internalFolder);
            }
        }

        private static byte[] ReadHostFolderSector(Stream stream, long sector)
        {
            var data = new byte[512];
            stream.Position = sector * 512;
            var offset = 0;
            while (offset < data.Length)
            {
                var read = stream.Read(data, offset, data.Length - offset);
                if (read == 0)
                    throw new EndOfStreamException();
                offset += read;
            }
            return data;
        }

        private static bool IsEntryInFirstDirectoryCluster(
            Stream stream,
            byte[] boot,
            uint directoryCluster,
            string shortName)
        {
            var dataStart = ReadLe16(boot, 14) +
                (long)boot[16] * ReadLe32(boot, 36);
            for (var sectorIndex = 0; sectorIndex < boot[13]; sectorIndex++)
            {
                var sector = ReadHostFolderSector(stream,
                    dataStart + (directoryCluster - 2) * boot[13] + sectorIndex);
                for (var offset = 0; offset < sector.Length; offset += 32)
                {
                    if (sector[offset] == 0)
                        return false;
                    if (sector[offset] != 0xE5 && sector[offset + 11] != 0x0F &&
                        Encoding.ASCII.GetString(sector, offset, 11) == shortName)
                        return true;
                }
            }
            return false;
        }

        private static uint ReadFatDirectoryParentCluster(
            Stream stream, byte[] boot, uint directoryCluster)
        {
            var dataStart = ReadLe16(boot, 14) +
                (long)boot[16] * ReadLe32(boot, 36);
            var sector = ReadHostFolderSector(stream,
                dataStart + (directoryCluster - 2) * boot[13]);
            const int parentOffset = 32;
            AssertHostFolderSd(Encoding.ASCII.GetString(sector,
                parentOffset, 11) == "..         ",
                "directory is missing its FAT parent entry");
            return ((uint)ReadLe16(sector, parentOffset + 20) << 16) |
                ReadLe16(sector, parentOffset + 26);
        }

        private static void AuditNedoOsHostFolder(string folderPath)
        {
            using (var stream = new HostFolderFat32Stream(folderPath))
            {
                AssertHostFolderSd(!stream.HasChanges,
                    "NedoOS audit requires a clean virtual SD session");
                AssertHostFolderSd(stream.Length == 4L * 1024L * 1024L * 1024L,
                    "the current NedoOS collection should use a standard 4 GiB card");
                var boot = ReadHostFolderSector(stream, 0);
                var root = ReadFatDirectory(stream, boot, 2);
                var bin = FindFatEntry(root, "bin");
                AssertHostFolderSd(bin != null && bin.IsDirectory &&
                    !bin.HasLongName && bin.CaseFlags == 0x08,
                    "NedoOS BIN directory is not a plain 8.3 entry");
                AssertHostFolderSd(ReadFatDirectoryParentCluster(stream,
                    boot, bin.Cluster) == 0,
                    "NedoOS BIN must mark the FAT32 root as its parent with cluster zero");
                AssertHostFolderSd(root.Count > 0 &&
                    root[0].ShortName == "BIN" && !root[0].HasLongName,
                    "NedoOS BIN is not the first root directory entry");
                var files = ReadFatDirectory(stream, boot, bin.Cluster);
                foreach (var fileName in new[] {
                    "autoexec.bat", "cmd.com", "term.com", "nv.com", "wizcfg.com" })
                {
                    var file = FindFatEntry(files, fileName);
                    AssertHostFolderSd(file != null && !file.HasLongName &&
                        file.CaseFlags == 0x18 &&
                        (file.Attributes & 0x01) == 0,
                        "NedoOS startup file has incompatible metadata: " + fileName);
                    AssertHostFolderSd(BytesEqual(ReadFatFile(stream, boot, file),
                        File.ReadAllBytes(Path.Combine(folderPath, "bin", fileName))),
                        "NedoOS startup file differs in virtual SD: " + fileName);
                }
                AssertHostFolderSd(IsEntryInFirstDirectoryCluster(stream,
                    boot, bin.Cluster, "WIZCFG  COM"),
                    "NedoOS WIZCFG is outside the first BIN directory cluster");
                var sdBoot = FindFatEntry(root, "sd_boot.$C");
                AssertHostFolderSd(sdBoot != null &&
                    BytesEqual(ReadFatFile(stream, boot, sdBoot),
                        File.ReadAllBytes(Path.Combine(folderPath, "sd_boot.$C"))),
                    "NedoOS SD boot program differs in virtual SD");
                Console.WriteLine("NedoOS folder startup files: PASS");
            }
        }

        private static System.Collections.Generic.List<FatTestEntry> ReadFatDirectory(
            Stream stream,
            byte[] boot,
            uint startCluster)
        {
            var sectorsPerCluster = boot[13];
            var reserved = ReadLe16(boot, 14);
            var fatCount = boot[16];
            var sectorsPerFat = ReadLe32(boot, 36);
            var dataStart = reserved + (long)fatCount * sectorsPerFat;
            var result = new System.Collections.Generic.List<FatTestEntry>();
            var lfnParts = new System.Collections.Generic.Dictionary<int, string>();
            var cluster = startCluster;
            while (cluster >= 2 && cluster < 0x0FFFFFF8)
            {
                for (var sectorIndex = 0; sectorIndex < sectorsPerCluster; sectorIndex++)
                {
                    var sector = ReadHostFolderSector(
                        stream,
                        dataStart + (cluster - 2) * sectorsPerCluster + sectorIndex);
                    for (var offset = 0; offset < sector.Length; offset += 32)
                    {
                        if (sector[offset] == 0)
                            return result;
                        if (sector[offset] == 0xE5)
                            continue;
                        if (sector[offset + 11] == 0x0F)
                        {
                            var order = sector[offset] & 0x1F;
                            lfnParts[order] = ReadLfnPart(sector, offset);
                            continue;
                        }
                        var entry = new FatTestEntry();
                        entry.Attributes = sector[offset + 11];
                        entry.CaseFlags = sector[offset + 12];
                        entry.IsDirectory = (sector[offset + 11] & 0x10) != 0;
                        entry.Cluster = ((uint)ReadLe16(sector, offset + 20) << 16) |
                            ReadLe16(sector, offset + 26);
                        entry.Length = ReadLe32(sector, offset + 28);
                        var stem = Encoding.ASCII.GetString(sector, offset, 8).TrimEnd();
                        var extension = Encoding.ASCII.GetString(sector, offset + 8, 3).TrimEnd();
                        entry.ShortName = extension.Length == 0
                            ? stem
                            : stem + "." + extension;
                        if (lfnParts.Count > 0)
                        {
                            entry.HasLongName = true;
                            var name = new StringBuilder();
                            for (var part = 1; lfnParts.ContainsKey(part); part++)
                                name.Append(lfnParts[part]);
                            entry.Name = name.ToString();
                            lfnParts.Clear();
                        }
                        else
                        {
                            entry.Name = entry.ShortName;
                        }
                        if (entry.Name != "." && entry.Name != "..")
                            result.Add(entry);
                    }
                }
                var fatOffset = cluster * 4;
                var fatSector = reserved + fatOffset / 512;
                var fatData = ReadHostFolderSector(stream, fatSector);
                cluster = ReadLe32(fatData, (int)(fatOffset % 512)) & 0x0FFFFFFF;
            }
            return result;
        }

        private static byte[] ReadFatFile(Stream stream, byte[] boot, FatTestEntry entry)
        {
            var sectorsPerCluster = boot[13];
            var reserved = ReadLe16(boot, 14);
            var dataStart = reserved + (long)boot[16] * ReadLe32(boot, 36);
            var result = new byte[entry.Length];
            var completed = 0;
            var cluster = entry.Cluster;
            while (completed < result.Length && cluster >= 2 && cluster < 0x0FFFFFF8)
            {
                for (var sectorIndex = 0;
                    sectorIndex < sectorsPerCluster && completed < result.Length;
                    sectorIndex++)
                {
                    var sector = ReadHostFolderSector(
                        stream,
                        dataStart + (cluster - 2) * sectorsPerCluster + sectorIndex);
                    var take = Math.Min(512, result.Length - completed);
                    Buffer.BlockCopy(sector, 0, result, completed, take);
                    completed += take;
                }
                var fatOffset = cluster * 4;
                var fatData = ReadHostFolderSector(stream, reserved + fatOffset / 512);
                cluster = ReadLe32(fatData, (int)(fatOffset % 512)) & 0x0FFFFFFF;
            }
            return result;
        }

        private static string ReadLfnPart(byte[] data, int offset)
        {
            var result = new StringBuilder();
            var positions = new[] { 1, 3, 5, 7, 9, 14, 16, 18, 20, 22, 24, 28, 30 };
            foreach (var position in positions)
            {
                var value = ReadLe16(data, offset + position);
                if (value == 0 || value == 0xFFFF)
                    break;
                result.Append((char)value);
            }
            return result.ToString();
        }

        private static FatTestEntry FindFatEntry(
            System.Collections.Generic.IEnumerable<FatTestEntry> entries,
            string name)
        {
            foreach (var entry in entries)
            {
                if (string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase))
                    return entry;
            }
            return null;
        }

        private static ushort ReadLe16(byte[] data, int offset)
        {
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }

        private static uint ReadLe32(byte[] data, int offset)
        {
            return (uint)(data[offset] |
                (data[offset + 1] << 8) |
                (data[offset + 2] << 16) |
                (data[offset + 3] << 24));
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left.Length != right.Length)
                return false;
            for (var index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index])
                    return false;
            }
            return true;
        }

        private static void AssertHostFolderSd(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("Host Folder SD test failed: " + message);
        }

        private sealed class FatTestEntry
        {
            public string Name;
            public string ShortName;
            public bool IsDirectory;
            public bool HasLongName;
            public byte Attributes;
            public byte CaseFlags;
            public uint Cluster;
            public uint Length;
        }

		private static Stream GetTestStream(string testName)
		{
			testName = string.Format("Test.{0}", testName);
			return Assembly.GetExecutingAssembly().GetManifestResourceStream(testName);
		}
	}

	public class FakeKeyboardState : IKeyboardState
	{
		private readonly bool m_pressSimulation;
		private readonly Key m_key;

		public FakeKeyboardState()
		{
			m_pressSimulation = false;
		}

		public FakeKeyboardState(Key keyPressed)
		{
			m_key = keyPressed;
			m_pressSimulation = true;
		}

		public bool this[Key key]
		{
			get { return m_pressSimulation ? (key == m_key) : false; }
		}
	}
}
