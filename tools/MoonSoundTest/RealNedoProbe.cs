using System;
using System.IO;
using System.Xml;
using System.Drawing;
using System.Reflection;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Hardware.Evo;
using ZXMAK2.Hardware.General;
using ZXMAK2.Hardware.Circuits.SecureDigital;
using ZXMAK2.Host.Entities;
using ZXMAK2.Host.Interfaces;

// Runs the original VM ROM, NedoOS SD boot and unmodified player.
// Host keys are supplied to the emulated keyboard, not a Windows UI.
internal static class RealNedoProbe
{
    private sealed class Keys : IKeyboardState
    {
        public Key? Held;
        public bool this[Key key] { get { return Held == key; } }
    }
    private static Spectrum spec;
    private static KeyboardDevice keyboard;
    private static Keys keys = new Keys();
    private static UlaPentEvo ula;
    private static MemoryPentEvo memory;
    private static CmosPentEvo cmos;
    private static StreamWriter trace;
    private static int rows;
    private static int audioPeak;
    private static string phase = "boot";
    private static readonly BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;

    private static void Frames(int count)
    {
        for (int i = 0; i < count; i++)
        {
            keyboard.KeyboardState = keys; if (cmos != null) cmos.KeyboardState = keys; spec.ExecuteFrame();
            var board = spec.BusManager.FindDevice<ZxmMoonSoundDevice>();
            if (board != null) foreach (uint s in board.AudioBuffer)
            { audioPeak = Math.Max(audioPeak, Math.Max(Math.Abs((int)(short)s), Math.Abs((int)(short)(s >> 16)))); }
        }
    }
    private static void Press(Key key)
    {
        keys.Held = key; Frames(8); keys.Held = null; Frames(8);
    }
    private static void Command(string command)
    {
        foreach (char c in command)
        {
            Key key;
            if (c == ' ') key = Key.Space;
            else if (c == '.') key = Key.Period;
            else if (c == '/') key = Key.Slash;
            else if (c == '-') key = Key.Minus;
            else if (char.IsDigit(c)) key = (Key)Enum.Parse(typeof(Key), "D" + c);
            else key = (Key)Enum.Parse(typeof(Key), char.ToUpperInvariant(c).ToString());
            Press(key);
        }
        Press(Key.Return);
    }
    private static void Screen(string path)
    {
        ula.ForceRedrawFrame(); var video = ula.VideoData;
        using (var image = new Bitmap(video.Size.Width, video.Size.Height))
        {
            for (int y = 0; y < image.Height; y++) for (int x = 0; x < image.Width; x++)
                image.SetPixel(x, y, Color.FromArgb(video.Buffer[y * image.Width + x]));
            image.Save(path);
        }
        Console.WriteLine("SCREEN {0} PC={1:X4} DOSEN={2} CPM={3} clock={4} audioPeak={5}", path, spec.CPU.regs.PC, memory.DOSEN, memory.CPM, memory.CpuClockMultiplier, audioPeak);
        trace.Flush();
    }
    private static bool Target(ushort p)
    { int b = p & 255; return (b >= 0xC2 && b <= 0xC7) || b == 0x7E || b == 0x7F; }
    private static void Record(string op, ushort p, byte value, bool before, bool after, ZxmMoonSoundDevice board)
    {
        if (rows++ >= 1500) return;
        var gate = typeof(ZxmMoonSoundDevice).GetMethod("AreExpansionPortsAvailable", flags);
        bool available = gate != null ? (bool)gate.Invoke(board, new object[] { p }) :
            (bool)typeof(ZxmMoonSoundDevice).GetProperty("AreExpansionPortsAvailable", flags).GetValue(board, null);
        trace.WriteLine("{0},{1},{2:X4},{3:X4},{4},{5},{6},{7},{8:X2},{9},{10}", phase, op, p,
            spec.CPU.regs.PC, memory.DOSEN, available, before, after, value, !available ? "GATE" : "", memory.CpuClockMultiplier);
    }
    private static void TracePorts()
    {
        var events = ((IBusManager)spec.BusManager).Events;
        var reads = (BusReadIoProc[])events.GetType().GetField("m_mapReadPort", flags).GetValue(events);
        var writes = (BusWriteIoProc[])events.GetType().GetField("m_mapWritePort", flags).GetValue(events);
        for (int i = 0; i < 65536; i++)
        {
            if (!Target((ushort)i) && i != 0x0CBE) continue;
            var originalRead = reads[i]; var originalWrite = writes[i];
            if (originalRead != null) reads[i] = delegate(ushort p, ref byte v, ref bool h)
            {
                foreach (BusReadIoProc handler in originalRead.GetInvocationList())
                {
                    bool was = h; handler(p, ref v, ref h);
                    var board = handler.Target as ZxmMoonSoundDevice;
                    if (board != null) Record("R", p, v, was, h, board);
                    else if (p == 0x0CBE && handler.Target == memory)
                        trace.WriteLine("{0},R,{1:X4},{2:X4},{3},n/a,{4},{5},{6:X2},,{7}", phase,
                            p, spec.CPU.regs.PC, memory.DOSEN, was, h, v, memory.CpuClockMultiplier);
                }
            };
            if (originalWrite != null) writes[i] = delegate(ushort p, byte v, ref bool h)
            {
                foreach (BusWriteIoProc handler in originalWrite.GetInvocationList())
                {
                    bool was = h; handler(p, v, ref h);
                    var board = handler.Target as ZxmMoonSoundDevice;
                    if (board != null) Record("W", p, v, was, h, board);
                }
            };
        }
    }
    private static int Main(string[] args)
    {
        try
        {
            string output = args[2]; Directory.CreateDirectory(output);
            spec = new Spectrum(); spec.BusManager.Init(spec, true);
            var config = new XmlDocument(); config.Load(args[0]);
            var sd = config.SelectSingleNode("//Device[contains(@type,'ZsdPentEvo')]");
            sd.Attributes["sdPath"].Value = args[1];
            if (Array.IndexOf(args, "board:OmniSound") >= 0)
            {
                config.SelectSingleNode("//Device[contains(@type,'UlaPentEvo')]")
                    .Attributes["zxBusSlot1Device"].Value = "OmniSound";
                config.SelectSingleNode("//Device[contains(@type,'ZxmMoonSoundDevice')]")
                    .Attributes["type"].Value = typeof(ZxOmniSoundDevice).AssemblyQualifiedName;
            }
            spec.BusManager.LoadConfigXml(config.SelectSingleNode("//Bus"));
            memory = spec.BusManager.FindDevice<MemoryPentEvo>();
            ula = spec.BusManager.FindDevice<UlaPentEvo>();
            keyboard = spec.BusManager.FindDevice<KeyboardDevice>();
            cmos = spec.BusManager.FindDevice<CmosPentEvo>();
            // Sandbox avoids opening the original profile media. Mount only
            // our copied SD explicitly; guest firmware remains unchanged.
            if (File.Exists(args[1]))
                spec.BusManager.FindDevice<ZsdPentEvo>().MountCard(Path.GetFullPath(args[1]));
            else
                spec.BusManager.FindDevice<ZsdPentEvo>().MountHostFolder(Path.GetFullPath(args[1]));
            trace = new StreamWriter(Path.Combine(output, "ports.csv"));
            trace.WriteLine("phase,op,port,PC,DOSEN,available,handled_before,handled_after,value,rejected,clock");
            TracePorts(); foreach (byte[] page in memory.RamPages) Array.Clear(page, 0, page.Length);
            spec.DebugReset(); spec.CPU.Tact = 0; Frames(180); Screen(Path.Combine(output, "00-ers.png"));
            for (int i = 3; i < args.Length; i++)
            {
                phase = args[i]; rows = 0; audioPeak = 0;
                if (phase.StartsWith("board:")) continue;
                if (phase.StartsWith("key:")) Press((Key)Enum.Parse(typeof(Key), phase.Substring(4)));
                else if (phase.StartsWith("keys:"))
                {
                    var sequence = phase.Split(':');
                    for (int k = 0; k < int.Parse(sequence[2]); k++)
                        Press((Key)Enum.Parse(typeof(Key), sequence[1]));
                }
                else if (phase.StartsWith("wait:")) Frames(int.Parse(phase.Substring(5)));
                else if (phase.StartsWith("clock:"))
                {
                    int rate = int.Parse(phase.Substring(6));
                    var mode = memory.GetType().GetField("m_pFF77", flags);
                    mode.SetValue(memory, ((int)mode.GetValue(memory) & ~8) | (rate == 4 ? 8 : 0));
                    memory.TURBO = rate == 1; memory.LatchClockAtRefresh();
                }
                else if (phase.StartsWith("disk:"))
                    typeof(FddController).GetMethod("MountFloppy", flags).Invoke(
                        spec.BusManager.FindDevice<FddPentEvo>(),
                        new object[] { 0, Path.GetFullPath(phase.Substring(5)), true });
                else if (phase == "cold")
                {
                    foreach (byte[] page in memory.RamPages) Array.Clear(page, 0, page.Length);
                    spec.DebugReset(); spec.CPU.Tact = 0;
                }
                else Command(phase);
                Frames(120); Screen(Path.Combine(output, i.ToString("D2") + ".png"));
            }
            trace.Dispose();
            // This harness uses disposable copied media, never user sources.
            var imageSession = (SdCard)typeof(ZsdPentEvo).GetField("card", flags)
                .GetValue(spec.BusManager.FindDevice<ZsdPentEvo>());
            if (imageSession.ImageSessionStream != null) imageSession.ImageSessionStream.DiscardChanges();
            spec.BusManager.Disconnect(); return 0;
        }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
    }
}
