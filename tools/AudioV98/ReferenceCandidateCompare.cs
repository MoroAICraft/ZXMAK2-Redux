using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

// Offline diagnostics only. Does not load an emulator, change profiles, or
// modify input WAV files. Rail occupancy is NOT treated as proof of clipping.
internal static class ReferenceCandidateCompare
{
    private sealed class Wave
    {
        internal string Path, FileHash, PcmHash;
        internal int Rate;
        internal short[] L, R;
    }
    private sealed class Channel
    {
        internal double Mean, Rms, AcRms, AcPeak;
        internal int Peak, Min = 32767, Max = -32768, RailRun, RailClusters, FirstRail = -1, LastRail = -1;
        internal long NegativeRail, PositiveRail;
    }
    private static string Hash(byte[] data)
    {
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "");
    }
    private static Wave Read(string path)
    {
        byte[] file = File.ReadAllBytes(path);
        using (var reader = new BinaryReader(new MemoryStream(file)))
        {
            if (new string(reader.ReadChars(4)) != "RIFF") throw new Exception("Expected RIFF: " + path);
            uint riff = reader.ReadUInt32();
            if (riff + 8L != file.Length || new string(reader.ReadChars(4)) != "WAVE")
                throw new Exception("Incomplete/unsupported WAV: " + path);
            int format = 0, rate = 0, channels = 0, bits = 0;
            while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
            {
                string tag = new string(reader.ReadChars(4));
                uint size = reader.ReadUInt32();
                long end = reader.BaseStream.Position + size + (size & 1);
                if (end > reader.BaseStream.Length) throw new Exception("Incomplete chunk: " + path);
                if (tag == "fmt ")
                {
                    format = reader.ReadUInt16(); channels = reader.ReadUInt16();
                    rate = reader.ReadInt32(); reader.ReadInt32(); reader.ReadInt16(); bits = reader.ReadInt16();
                }
                else if (tag == "data")
                {
                    if (format != 1 || channels != 2 || bits != 16 || rate <= 0 || size % 4 != 0)
                        throw new Exception("Expected PCM16 stereo: " + path);
                    byte[] pcm = reader.ReadBytes(checked((int)size));
                    int count = pcm.Length / 4;
                    var wave = new Wave { Path = path, Rate = rate, L = new short[count], R = new short[count],
                        FileHash = Hash(file), PcmHash = Hash(pcm) };
                    for (int i = 0; i < count; i++)
                    { wave.L[i] = BitConverter.ToInt16(pcm, i * 4); wave.R[i] = BitConverter.ToInt16(pcm, i * 4 + 2); }
                    return wave;
                }
                reader.BaseStream.Position = end;
            }
            throw new Exception("No data: " + path);
        }
    }
    private static Channel Measure(short[] pcm)
    {
        var result = new Channel(); double sum = 0, squares = 0; int run = 0, previous = 0, index = 0;
        foreach (short value in pcm)
        {
            sum += value; squares += (double)value * value;
            result.Min = Math.Min(result.Min, value); result.Max = Math.Max(result.Max, value);
            result.Peak = Math.Max(result.Peak, Math.Abs((int)value));
            if (value == -32768) result.NegativeRail++;
            if (value == 32767) result.PositiveRail++;
            if (value == -32768 || value == 32767)
            {
                run = value == previous ? run + 1 : 1;
                if (run == 1) result.RailClusters++;
                if (result.FirstRail < 0) result.FirstRail = index;
                result.LastRail = index;
                result.RailRun = Math.Max(result.RailRun, run);
            }
            else run = 0;
            previous = value; index++;
        }
        result.Mean = sum / pcm.Length;
        result.Rms = Math.Sqrt(squares / pcm.Length);
        result.AcRms = Math.Sqrt(Math.Max(0, squares / pcm.Length - result.Mean * result.Mean));
        result.AcPeak = Math.Max(Math.Abs(result.Min - result.Mean), Math.Abs(result.Max - result.Mean));
        return result;
    }
    private static string Db(double value) { return value == 0 ? "-inf" : (20 * Math.Log10(value / 32768)).ToString("F6"); }
    private static void Row(StreamWriter output, string run, string variant, string file, string channel, Wave wave, Channel c)
    {
        output.WriteLine(String.Join("\t", new object[] { run, variant, file, channel, wave.Rate, wave.L.Length,
            (wave.L.Length / (double)wave.Rate).ToString("F6"), c.Mean.ToString("F6"), c.Rms.ToString("F6"),
            c.AcRms.ToString("F6"), Db(c.AcRms), c.Peak, c.AcPeak.ToString("F6"), c.Min, c.Max,
            c.NegativeRail, c.PositiveRail, c.RailRun, c.RailClusters, c.FirstRail, c.LastRail,
            c.AcRms < 0.000001 ? "constant/DC-or-silence" : "varying; rails alone do not prove clipping",
            wave.FileHash, wave.PcmHash }));
    }
    private static string IoHash(string folder)
    {
        string path = System.IO.Path.Combine(folder, "guest.txt");
        if (!File.Exists(path)) return "absent";
        var matches = Regex.Matches(File.ReadAllText(path), @"IOHASH count=\d+ hash=[0-9A-F]+ tact=\d+ PC=[0-9A-F]+");
        return matches.Count == 0 ? "absent" : matches[matches.Count - 1].Value;
    }
    private static void Clip(string path, Wave wave, int first, int count, double leftMean, double rightMean, double gain)
    {
        using (var writer = new BinaryWriter(new FileStream(path, FileMode.CreateNew)))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 4);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)2); writer.Write(wave.Rate); writer.Write(wave.Rate * 4);
            writer.Write((short)4); writer.Write((short)16); writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(count * 4);
            for (int i = first; i < first + count; i++)
            {
                double l = Math.Round((wave.L[i] - leftMean) * gain), r = Math.Round((wave.R[i] - rightMean) * gain);
                if (l < -32768 || l > 32767 || r < -32768 || r > 32767) throw new Exception("Clip overflow");
                writer.Write((short)l); writer.Write((short)r);
            }
        }
    }
    private static void Clips(string directory, string run, Wave a, Wave b, TextWriter report)
    {
        if (a.Rate != b.Rate) throw new Exception("Clip rate mismatch");
        int first = a.Rate * 2, count = Math.Min(a.Rate * 10, Math.Min(a.L.Length, b.L.Length) - first);
        if (count <= 0) throw new Exception("Clip too short");
        short[] al = a.L.Skip(first).Take(count).ToArray(), ar = a.R.Skip(first).Take(count).ToArray();
        short[] bl = b.L.Skip(first).Take(count).ToArray(), br = b.R.Skip(first).Take(count).ToArray();
        Channel ma = Measure(al), ra = Measure(ar), mb = Measure(bl), rb = Measure(br);
        double peak = Math.Max(Math.Max(ma.AcPeak, ra.AcPeak), Math.Max(mb.AcPeak, rb.AcPeak));
        double gain = peak == 0 ? 1 : Math.Min(1, 32767 * 0.8 / peak);
        string output = System.IO.Path.Combine(directory, run); Directory.CreateDirectory(output);
        Clip(System.IO.Path.Combine(output, "A-v97.wav"), a, first, count, ma.Mean, ra.Mean, gain);
        Clip(System.IO.Path.Combine(output, "B-v98-test.wav"), b, first, count, mb.Mean, rb.Mean, gain);
        report.WriteLine("CLIP {0}: common fixed gain={1:F12} ({2:F9}dB), start=2s duration={3:F6}s; DC removed A={4:F9}/{5:F9}, B={6:F9}/{7:F9}.",
            run, gain, 20 * Math.Log10(gain), count / (double)a.Rate, ma.Mean, ra.Mean, mb.Mean, rb.Mean);
        double rmsA = Math.Sqrt((ma.AcRms * ma.AcRms + ra.AcRms * ra.AcRms) / 2);
        double rmsB = Math.Sqrt((mb.AcRms * mb.AcRms + rb.AcRms * rb.AcRms) / 2);
        if (rmsA == 0 || rmsB == 0)
        { report.WriteLine("MATCHED {0}: not generated: silent segment cannot be level matched", run); return; }
        double crest = Math.Max(Math.Max(ma.AcPeak, ra.AcPeak) / rmsA, Math.Max(mb.AcPeak, rb.AcPeak) / rmsB);
        double target = Math.Min(32768 * Math.Pow(10, -23.0 / 20), 32767 * 0.8 / crest);
        double gainA = target / rmsA, gainB = target / rmsB;
        string matched = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(directory), "clips-matched", run);
        Directory.CreateDirectory(matched);
        Clip(System.IO.Path.Combine(matched, "A-v97-matched.wav"), a, first, count, ma.Mean, ra.Mean, gainA);
        Clip(System.IO.Path.Combine(matched, "B-v98-test-matched.wav"), b, first, count, mb.Mean, rb.Mean, gainB);
        report.WriteLine("MATCHED {0}: common target AC_RMS={1:F9} ({2}dBFS); one fixed stereo gain per recording A={3:F12} ({4:F9}dB), B={5:F12} ({6:F9}dB); AC_RMS before A={7:F9}, B={8:F9}; peak ceiling=80%FS. Same segment/DC removal as CLIP. RMS matching, NOT perceptual loudness matching; no compression/EQ/time stretching, no dither.",
            run, target, Db(target), gainA, 20 * Math.Log10(gainA), gainB, 20 * Math.Log10(gainB), rmsA, rmsB);
    }
    private static int Main(string[] args)
    {
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            string root = args[0], destination = args[1];
            if (Directory.Exists(destination)) throw new Exception("Preserve previous comparison: choose a new output directory");
            Directory.CreateDirectory(destination);
            var clipRuns = new HashSet<string>(new[] { "moon-midi", "multi-midi", "tsfm-fm", "max-aryx", "opl3-aryx", "extreme-saa-correct" });
            using (var metrics = new StreamWriter(System.IO.Path.Combine(destination, "metrics.tsv"), false, Encoding.UTF8))
            using (var report = new StreamWriter(System.IO.Path.Combine(destination, "comparison.txt"), false, Encoding.UTF8))
            {
                metrics.WriteLine("run\tvariant\tfile\tchannel\trate\tframes\tseconds\tDC\tRMS\tAC_RMS\tAC_dBFS\tpeak\tAC_peak\tmin\tmax\tnegative_rail_samples\tpositive_rail_samples\tmax_same_rail_run\trail_clusters\tfirst_rail_index\tlast_rail_index\tinterpretation\tfile_SHA256\tPCM_SHA256");
                report.WriteLine("Same guest script/start; no automatic time or sample alignment. Equal IOHASH supports identical guest I/O, not physical hardware fidelity.");
                report.WriteLine("Metrics are independent per channel; raw/DC-offset renderers may occupy a signed rail even while silent. Rail samples/runs are diagnostics, NOT confirmed clipping.");
                report.WriteLine("Clips use mixed PCM only: same 2..12s segment, constant channel means removed, one common gain per A/B pair for <=80%FS headroom. No gain boosting, compression, EQ, fades or time stretching. Original volume differences remain; no perceptual listening claim.");
                report.WriteLine("Separate clips-matched uses fixed per-recording gain to a shared -23dBFS AC-RMS target, reduced jointly if needed for 80%FS peak ceiling. One gain for both channels preserves their AC balance. It is not perceptual loudness matching.");
                foreach (string run in args.Skip(2))
                {
                    string baseline = System.IO.Path.Combine(root, "baseline\\diagnostics", run);
                    string candidate = System.IO.Path.Combine(root, "candidate\\diagnostics", run);
                    if (!Directory.Exists(baseline) || !Directory.Exists(candidate)) { report.WriteLine("MISSING " + run); continue; }
                    string ia = IoHash(baseline), ib = IoHash(candidate);
                    report.WriteLine("RUN {0}: IOHASH equal={1}; A={2}; B={3}", run, ia != "absent" && ia == ib, ia, ib);
                    foreach (string path in Directory.GetFiles(baseline, "music-*.wav").OrderBy(x => x))
                    {
                        string name = System.IO.Path.GetFileName(path), next = System.IO.Path.Combine(candidate, name);
                        if (!File.Exists(next)) { report.WriteLine("MISSING candidate " + run + "/" + name); continue; }
                        Wave a = Read(path), b = Read(next);
                        Channel al = Measure(a.L), ar = Measure(a.R), bl = Measure(b.L), br = Measure(b.R);
                        Row(metrics, run, "v97", name, "L", a, al); Row(metrics, run, "v97", name, "R", a, ar);
                        Row(metrics, run, "v98-test", name, "L", b, bl); Row(metrics, run, "v98-test", name, "R", b, br);
                        double acA = Math.Sqrt((al.AcRms * al.AcRms + ar.AcRms * ar.AcRms) / 2);
                        double acB = Math.Sqrt((bl.AcRms * bl.AcRms + br.AcRms * br.AcRms) / 2);
                        report.WriteLine("  {0}: PCM exact={1}; count/rate equal={2}; AC_RMS={3:F6}->{4:F6}, delta={5}; peak={6}->{7}; DC L/R={8:F3}/{9:F3}->{10:F3}/{11:F3}",
                            name, a.PcmHash == b.PcmHash, a.Rate == b.Rate && a.L.Length == b.L.Length, acA, acB,
                            acA == 0 || acB == 0 ? "undefined (silent)" : (20 * Math.Log10(acB / acA)).ToString("F6") + "dB",
                            Math.Max(al.Peak, ar.Peak), Math.Max(bl.Peak, br.Peak), al.Mean, ar.Mean, bl.Mean, br.Mean);
                        if (name == "music-mixed.wav" && clipRuns.Contains(run)) Clips(System.IO.Path.Combine(destination, "clips"), run, a, b, report);
                    }
                }
            }
            Console.WriteLine("Comparison written: " + destination); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
