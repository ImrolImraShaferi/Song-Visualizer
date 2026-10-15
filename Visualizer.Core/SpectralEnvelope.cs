using System.Numerics;

namespace Visualizer.Core;

/// <summary>Hann-windowed FFT energy: bass 20–250 Hz, mids 250–4000 Hz, highs 4000 Hz–Nyquist.
/// Stereo channel powers are averaged, so opposite-phase channels do not cancel.</summary>
public sealed class SpectralEnvelope
{
    private readonly AudioFeatures[] _frames;
    private readonly double _step;

    private SpectralEnvelope(AudioFeatures[] frames, double step)
    {
        _frames = frames;
        _step = step;
    }

    public static SpectralEnvelope FromInterleaved(float[] samples, int sampleRate, int channels)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));
        if (samples.Length % channels != 0) throw new ArgumentException("Incomplete sample frame.", nameof(samples));
        const int size = 2048, hop = 512;
        var total = samples.Length / channels;
        var count = (int)(((long)total + hop - 1) / hop);
        var raw = new AudioFeatures[count];
        var window = new double[size];
        for (var i = 0; i < size; i++) window[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (size - 1));
        var normalization = size * window.Sum(x => x * x) * channels;
        var spectrum = new Complex[size];
        for (var frame = 0; frame < count; frame++)
        {
            double bass = 0, mid = 0, high = 0, level = 0;
            for (var channel = 0; channel < channels; channel++)
            {
                for (var i = 0; i < size; i++)
                {
                    var index = (long)frame * hop + i - size / 2;
                    var sample = index >= 0 && index < total ? samples[index * channels + channel] : 0;
                    spectrum[i] = new Complex(sample * window[i], 0);
                }
                Fft(spectrum);
                for (var bin = 1; bin <= size / 2; bin++)
                {
                    var frequency = (double)bin * sampleRate / size;
                    var power = spectrum[bin].Magnitude * spectrum[bin].Magnitude * (bin == size / 2 ? 1 : 2) / normalization;
                    level += power;
                    if (frequency < 20) continue;
                    if (frequency < 250) bass += power;
                    else if (frequency < 4000) mid += power;
                    else high += power;
                }
            }
            raw[frame] = new(Math.Sqrt(level), Math.Sqrt(bass), Math.Sqrt(mid), Math.Sqrt(high));
        }

        // A per-band 95th percentile keeps quiet treble useful without one peak flattening the animation.
        double Scale(Func<AudioFeatures, double> select)
        {
            if (count == 0) return 1;
            var values = raw.Select(select).OrderBy(x => x).ToArray();
            return Math.Max(0.0001, values[(int)((count - 1) * 0.95)]);
        }
        var levelScale = Scale(x => x.Level);
        // Do not amplify FFT leakage in an otherwise absent band into full-strength motion.
        var bandFloor = levelScale * 0.05;
        var scales = new AudioFeatures(levelScale, Math.Max(bandFloor, Scale(x => x.Bass)),
            Math.Max(bandFloor, Scale(x => x.Mid)), Math.Max(bandFloor, Scale(x => x.High)));
        var step = hop / (double)sampleRate;
        double Smooth(double previous, double value) => previous + (1 - Math.Exp(-step / (value > previous ? 0.035 : 0.22))) * (value - previous);
        var previous = new AudioFeatures();
        for (var i = 0; i < count; i++)
        {
            var f = raw[i];
            previous = new(Smooth(previous.Level, Math.Clamp(f.Level / scales.Level, 0, 1)),
                Smooth(previous.Bass, Math.Clamp(f.Bass / scales.Bass, 0, 1)),
                Smooth(previous.Mid, Math.Clamp(f.Mid / scales.Mid, 0, 1)),
                Smooth(previous.High, Math.Clamp(f.High / scales.High, 0, 1)));
            raw[i] = previous;
        }
        return new(raw, step);
    }

    public AudioFeatures GetValueAtTime(double seconds)
    {
        if (!double.IsFinite(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (_frames.Length == 0 || seconds < 0) return default;
        var position = Math.Min(seconds / _step, _frames.Length - 1);
        var lower = (int)position;
        var upper = Math.Min(lower + 1, _frames.Length - 1);
        var t = position - lower;
        var a = _frames[lower]; var b = _frames[upper];
        return new(a.Level + (b.Level - a.Level) * t, a.Bass + (b.Bass - a.Bass) * t,
            a.Mid + (b.Mid - a.Mid) * t, a.High + (b.High - a.High) * t);
    }

    private static void Fft(Complex[] data)
    {
        for (int i = 1, j = 0; i < data.Length; i++)
        {
            var bit = data.Length >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) (data[i], data[j]) = (data[j], data[i]);
        }
        for (var length = 2; length <= data.Length; length <<= 1)
        {
            var angle = -2 * Math.PI / length;
            var root = new Complex(Math.Cos(angle), Math.Sin(angle));
            for (var start = 0; start < data.Length; start += length)
            {
                var factor = Complex.One;
                for (var k = 0; k < length / 2; k++)
                {
                    var a = data[start + k]; var b = data[start + k + length / 2] * factor;
                    data[start + k] = a + b; data[start + k + length / 2] = a - b;
                    factor *= root;
                }
            }
        }
    }
}
