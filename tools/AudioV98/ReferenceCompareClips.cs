using System;
using System.Globalization;
using System.IO;

// Diagnostic-only clips. Reuses the alignment established with the older
// AudioReferenceCompare probe; does not claim sample/phase-perfect equivalence.
internal static class ReferenceCompareClips
{
    private sealed class Wave
    {
        internal int Rate;
        internal double[] Left, Right;
    }

    private sealed class Segment
    {
        internal double[] Left, Right;
        internal double MeanLeft, MeanRight, Rms, Peak;
    }

    private static Wave Read(string path)
    {
        using (var reader = new BinaryReader(File.OpenRead(path)))
        {
            if (new string(reader.ReadChars(4)) != "RIFF") throw new Exception("RIFF required");
            reader.ReadUInt32();
            if (new string(reader.ReadChars(4)) != "WAVE") throw new Exception("WAVE required");
            int rate = 0, channels = 0, bits = 0, format = 0;
            while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
            {
                string tag = new string(reader.ReadChars(4));
                uint size = reader.ReadUInt32();
                long end = reader.BaseStream.Position + size + (size & 1);
                if (end > reader.BaseStream.Length) throw new Exception("Truncated RIFF");
                if (tag == "fmt ")
                {
                    format = reader.ReadUInt16(); channels = reader.ReadUInt16();
                    rate = reader.ReadInt32(); reader.ReadInt32();
                    reader.ReadUInt16(); bits = reader.ReadUInt16();
                }
                else if (tag == "data")
                {
                    if (format != 1 || channels != 2 || bits != 16 || rate <= 0 || size % 4 != 0)
                        throw new Exception("PCM16 stereo required");
                    int count = checked((int)(size / 4));
                    var wave = new Wave { Rate = rate, Left = new double[count], Right = new double[count] };
                    for (int i = 0; i < count; i++)
                    { wave.Left[i] = reader.ReadInt16(); wave.Right[i] = reader.ReadInt16(); }
                    return wave;
                }
                reader.BaseStream.Position = end;
            }
            throw new Exception("Data chunk missing");
        }
    }

    private static Segment Extract(Wave wave, double start, double seconds)
    {
        int first = (int)Math.Round(start * wave.Rate);
        int count = (int)Math.Round(seconds * wave.Rate);
        if (first < 0 || count <= 0 || first + count > wave.Left.Length) throw new Exception("Invalid segment");
        var result = new Segment { Left = new double[count], Right = new double[count] };
        for (int i = 0; i < count; i++)
        { result.MeanLeft += wave.Left[first + i]; result.MeanRight += wave.Right[first + i]; }
        result.MeanLeft /= count; result.MeanRight /= count;
        double square = 0;
        for (int i = 0; i < count; i++)
        {
            double l = wave.Left[first + i] - result.MeanLeft;
            double r = wave.Right[first + i] - result.MeanRight;
            result.Left[i] = l; result.Right[i] = r;
            square += l * l + r * r;
            result.Peak = Math.Max(result.Peak, Math.Max(Math.Abs(l), Math.Abs(r)));
        }
        result.Rms = Math.Sqrt(square / (count * 2.0));
        if (result.Rms <= 0) throw new Exception("Cannot level-match silence");
        return result;
    }

    private static void Write(string path, int rate, Segment segment, double gain)
    {
        // CreateNew prevents accidental replacement of prior listening artifacts.
        using (var writer = new BinaryWriter(new FileStream(path, FileMode.CreateNew)))
        {
            int bytes = checked(segment.Left.Length * 4);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + bytes);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)2); writer.Write(rate);
            writer.Write(rate * 4); writer.Write((short)4); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(bytes);
            for (int i = 0; i < segment.Left.Length; i++)
            {
                double l = Math.Round(segment.Left[i] * gain);
                double r = Math.Round(segment.Right[i] * gain);
                if (l < -32768 || l > 32767 || r < -32768 || r > 32767) throw new Exception("Unexpected clipping");
                writer.Write((short)l); writer.Write((short)r);
            }
        }
    }

    private static void Report(string label, Segment segment, double gain)
    {
        Console.WriteLine("{0}: DC_subtracted_L={1:F9} R={2:F9}; AC_RMS_before={3:F9}; constant_gain={4:F12} ({5:F9} dB); peak_after={6:F3}; AC_RMS_after={7:F3}",
            label, segment.MeanLeft, segment.MeanRight, segment.Rms, gain, 20 * Math.Log10(gain), segment.Peak * gain, segment.Rms * gain);
    }

    private static int Main(string[] args)
    {
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            var guest = Read(args[0]); var reference = Read(args[1]);
            if (guest.Rate != reference.Rate) throw new Exception("Rate mismatch");
            double offset = double.Parse(args[3], CultureInfo.InvariantCulture);
            const double start = 2, seconds = 20;
            var a = Extract(guest, start, seconds);
            var b = Extract(reference, start + offset, seconds);
            // Target -20 dBFS AC RMS; reduce both further if either clip would
            // exceed 80% full-scale peak. Only a constant per-clip gain is used.
            double maxCrest = Math.Max(a.Peak / a.Rms, b.Peak / b.Rms);
            double target = Math.Min(32768 * 0.1, 32767 * 0.8 / maxCrest);
            double gainA = target / a.Rms, gainB = target / b.Rms;
            Directory.CreateDirectory(args[2]);
            Write(Path.Combine(args[2], "A-ZXMAK2-v97-PT3-matched.wav"), guest.Rate, a, gainA);
            Write(Path.Combine(args[2], "B-ZXTune-r5100-PT3-matched.wav"), guest.Rate, b, gainB);
            Console.WriteLine("Guest={0}; reference={1}", args[0], args[1]);
            Console.WriteLine("Segments: guest {0:F3}..{1:F3}s; reference {2:F3}..{3:F3}s; rate={4}; PCM16 stereo", start, start + seconds, start + offset, start + offset + seconds, guest.Rate);
            Console.WriteLine("Coarse 20ms envelope alignment only; not sample/phase-perfect. Constant segment mean is removed from each channel of BOTH clips; no high-pass/EQ/compressor/limiter/fade/time stretching applied.");
            Console.WriteLine("AC RMS matching, not perceptual BS.1770 loudness normalization. Musical dynamics inside each clip are unchanged except constant DC/gain and PCM16 rounding.");
            Console.WriteLine("Target AC RMS={0:F9} ({1:F9} dBFS); common peak ceiling=80%FS; sample quantization has no dither.", target, 20 * Math.Log10(target / 32768));
            Report("A ZXMAK2", a, gainA); Report("B ZXTune", b, gainB);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
