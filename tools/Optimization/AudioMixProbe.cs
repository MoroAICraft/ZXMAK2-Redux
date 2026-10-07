using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using ZXMAK2.Engine;
using ZXMAK2.Hardware.Evo;
using ZXMAK2.Host.Entities;

// Bounded regression of final PCM, plus actual native Moon ROM voice.
internal static class AudioMixProbe
{
    private static int checks;
    private static void Check(bool ok, string name) { checks++; if (!ok) throw new Exception(name); }
    private static short L(uint v) { return unchecked((short)v); }
    private static short R(uint v) { return unchecked((short)(v >> 16)); }
    private static uint P(short l, short r) { return (uint)(ushort)l | ((uint)(ushort)r << 16); }
    private static FrameSound Modern(int rate, uint[][] sources, int master, int[] gains)
    { return new FrameSound(rate, sources, false, master, gains, true); }

    private static void Mix()
    {
        foreach (int rate in new[] { 44100, 48000, 96000 }) {
            int n = rate / 50;
            var signal = new uint[n];
            for (int i = 0; i < n; i++) signal[i] = P((short)(12000 * Math.Sin(i * 0.3)), (short)(8000 * Math.Cos(i * 0.2)));
            var expected = Modern(rate, new[] { signal }, 100, null).GetBuffer();
            foreach (int count in new[] { 2, 4, 8, 12 }) {
                var sources = new uint[count][];
                sources[0] = signal;
                for (int j = 1; j < count; j++) sources[j] = new uint[n];
                Check(expected.SequenceEqual(Modern(rate, sources, 100, null).GetBuffer()), "Silent sources changed level");
                for (int j = 1; j < count; j++) for (int i = 0; i < n; i++) sources[j][i] = P(short.MinValue, short.MinValue);
                Check(expected.SequenceEqual(Modern(rate, sources, 100, null).GetBuffer()), "PSG DC changed sound / early narrowing");
            }
            Check(Modern(rate, new[] { signal }, 0, null).GetBuffer().All(v => v == 0), "Master mute");
            Check(Modern(rate, new uint[0][], 100, null).GetBuffer().All(v => v == 0), "Empty mix");
            var muted = Modern(rate, new[] { signal }, 100, new[] { 0 });
            Check(muted.GetBuffer().All(v => v == 0), "Source mute");
            var cached = Modern(rate, new[] { signal }, 100, null);
            Check(Object.ReferenceEquals(cached.GetBuffer(), cached.GetBuffer()), "Frame cache");

            // Same continuous input with/without frame boundaries.
            var pieces = Modern(rate, new[] { signal }, 100, null);
            var collected = new List<uint>();
            for (int frame = 0; frame < 5; frame++) {
                for (int i = 0; i < n; i++) signal[i] = P((short)(9000 * Math.Sin((frame * n + i) * 0.4)), 0);
                pieces.Refresh(); collected.AddRange(pieces.GetBuffer());
            }
            var all = new uint[n * 5];
            for (int i = 0; i < all.Length; i++) all[i] = P((short)(9000 * Math.Sin(i * 0.4)), 0);
            Check(collected.SequenceEqual(Modern(rate * 5, new[] { all }, 100, null).GetBuffer()) == false, "Sample-rate-aware coefficient");
            // Compare frame chunks against one recurrent run at the SAME rate.
            var continuous = Modern(rate, new[] { signal }, 100, null);
            for (int frame = 0; frame < 5; frame++) {
                Array.Copy(all, frame * n, signal, 0, n);
                continuous.Refresh();
                Check(continuous.GetBuffer().SequenceEqual(collected.Skip(frame * n).Take(n)), "Cross-frame state");
            }
            foreach (int hz in new[] { 20, 50, 1000 }) {
                var filter = Modern(rate, new[] { signal }, 100, null);
                double squares = 0; long samples = 0;
                for (int frame = 0; frame < 100; frame++) {
                    for (int i = 0; i < n; i++) signal[i] = P((short)Math.Round(12000 * Math.Sin(2 * Math.PI * hz * (frame * n + i) / rate)), 0);
                    filter.Refresh(); var output = filter.GetBuffer();
                    if (frame >= 50) foreach (var p in output) { squares += (double)L(p) * L(p); samples++; Check(R(p) == 0, "Channel separation"); }
                }
                double db = 20 * Math.Log10(Math.Sqrt(squares / samples) / (6000 / Math.Sqrt(2)));
                Check(db > -0.3 && db < 0.01, "Bass / sample-rate response");
                Console.WriteLine("MODERN rate={0} hz={1} response={2:F4}dB relative fixed 0.5", rate, hz, db);
            }
            for (int i = 0; i < n; i++) signal[i] = P((short)((i & 1) == 0 ? 32767 : -32768), 0);
            signal[0] = 0; // Prime zero, not the positive peak of this synthetic step.
            var overload = Modern(rate, Enumerable.Repeat(signal, 64).ToArray(), 200, Enumerable.Repeat(200, 64).ToArray());
            var clipped = overload.GetBuffer();
            Check(clipped.All(v => R(v) == 0), "Overload channel separation");
            Check(L(clipped[1]) < -24576 && L(clipped[2]) > 24576, "Overload wrapped sign");
            var legacy = new FrameSound(rate, new[] { signal, new uint[n] });
            Check(L(legacy.GetBuffer()[2]) == 16383, "Legacy divisor rollback");
        }
    }

    private static void Config()
    {
        var bus = new BusManager();
        Check(!bus.UseLegacyAudioMix, "Default modern mode");
        const string devices = "<Device type='ZXMAK2.Hardware.Spectrum.UlaSpectrum48'/><Device type='ZXMAK2.Hardware.Spectrum.MemorySpectrum48'/>";
        var doc = new XmlDocument(); doc.LoadXml("<Bus legacyAudioMix='true'>" + devices + "</Bus>");
        bus.LoadConfigXml(doc.DocumentElement);
        Check(bus.UseLegacyAudioMix, "Load rollback mode");
        bus.SaveConfigXml(doc.DocumentElement);
        Check(doc.DocumentElement.GetAttribute("legacyAudioMix") == "true", "Save rollback mode");
        bus.UseLegacyAudioMix = false; bus.SaveConfigXml(doc.DocumentElement);
        Check(!doc.DocumentElement.HasAttribute("legacyAudioMix"), "Remove stale rollback mode");
        doc.LoadXml("<Bus>" + devices + "</Bus>"); bus.LoadConfigXml(doc.DocumentElement);
        Check(!bus.UseLegacyAudioMix, "Old profiles default modern");
        bus.Disconnect();
    }
    private static void Port(List<byte> code, ushort port, byte value)
    { code.AddRange(new byte[] { 0x01, (byte)port, (byte)(port >> 8), 0x3E, value, 0xED, 0x79 }); }
    private static void Reg(List<byte> code, ushort port, byte reg, byte value)
    { Port(code, port, reg); Port(code, (ushort)(port + 1), value); }
    private static uint[] Moon(int volume)
    {
        string xml = "<Bus><Device type='ZXMAK2.Hardware.Spectrum.UlaSpectrum48'/><Device type='ZXMAK2.Hardware.Spectrum.MemorySpectrum48'/><Device type='ZXMAK2.Hardware.Evo.ZxmMoonSoundDevice' volume='" + volume + "'/></Bus>";
        var machine = OptimizationProbe.Create(xml);
        var mem = OptimizationProbe.Prepare(machine, null, 0);
        var board = machine.BusManager.FindDevice<ZxmMoonSoundDevice>();
        Check(board.CoreAvailable, "Moon native core");
        var code = new List<byte>();
        Reg(code, 0x78C6, 0x05, 0x03); Reg(code, 0x9A7E, 0xF9, 0x00);
        Reg(code, 0x9A7E, 0x08, 0x00); Reg(code, 0x9A7E, 0x20, 0xFE);
        Reg(code, 0x9A7E, 0x38, 0x07); Reg(code, 0x9A7E, 0x50, 0x01); Reg(code, 0x9A7E, 0x68, 0x80);
        code.AddRange(new byte[] { 0x18, 0xFE });
        for (int i = 0; i < code.Count; i++) mem.WRMEM_DBG((ushort)(0x4000 + i), code[i]);
        machine.DebugReset(); machine.CPU.regs.PC = 0x4000;
        var output = new List<uint>();
        for (int frame = 0; frame < 50; frame++) { machine.ExecuteFrame(); if (frame >= 10) output.AddRange(board.AudioBuffer); }
        machine.BusManager.Disconnect();
        return output.ToArray();
    }
    private static int Main()
    {
        try {
            Mix(); Config();
            var full = Moon(100); var half = Moon(50); var mute = Moon(0);
            Check(full.Any(v => v != 0), "Moon voice silent");
            Check(mute.All(v => v == 0), "Moon mute");
            Check(full.Length == half.Length && full.Select((v,i) => L(half[i]) == L(v) / 2 && R(half[i]) == R(v) / 2).All(v => v), "Moon volume exact 50%");
            ulong hash = 14695981039346656037UL;
            foreach (uint p in full) unchecked { hash = (hash ^ p) * 1099511628211UL; }
            Check(hash == 0x9F3EE7BD381A9180UL, "Moon 100% raw PCM differs from v81");
            Console.WriteLine("MOON native PCM 0/50/100 verified; v81 100% hash={0:X16}", hash);
            Console.WriteLine("PASS common audio checks={0}", checks); return 0;
        } catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
