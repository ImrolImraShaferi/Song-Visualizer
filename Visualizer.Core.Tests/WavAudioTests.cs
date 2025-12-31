using System.Linq;
using System.Text;
using Visualizer.Core;
using Xunit;

namespace Visualizer.Core.Tests;

public class WavAudioTests
{
    [Fact]
    public void Load_ReadsMonoSineWave()
    {
        const int sampleRate = 44100;
        const int channels = 1;
        const double durationSeconds = 1.0;
        const double frequency = 440.0;

        var wavBytes = CreateSineWaveWav16Bit(sampleRate, channels, durationSeconds, frequency, includeJunkChunk: true);

        using var stream = new MemoryStream(wavBytes);
        var audio = WavAudio.Load(stream);

        Assert.Equal(sampleRate, audio.SampleRate);
        Assert.Equal(channels, audio.Channels);
        Assert.Equal(sampleRate * durationSeconds, audio.TotalSamplesPerChannel);
        Assert.InRange(audio.DurationSeconds, 0.99, 1.01);

        var maxAmplitude = audio.Samples.Select(MathF.Abs).Max();
        Assert.InRange(maxAmplitude, 0.9f, 1.01f);

        Assert.All(audio.Samples, sample => Assert.InRange(sample, -1.0001f, 1.0001f));
    }

    private static byte[] CreateSineWaveWav16Bit(int sampleRate, int channels, double durationSeconds, double frequency, bool includeJunkChunk)
    {
        var totalSamplesPerChannel = (int)Math.Round(sampleRate * durationSeconds);
        var bytesPerSample = 2;
        var dataSize = totalSamplesPerChannel * channels * bytesPerSample;
        var fmtChunkSize = 16;
        var riffSize = 4 + (8 + fmtChunkSize) + (includeJunkChunk ? 8 + 4 : 0) + (8 + dataSize);

        using var memoryStream = new MemoryStream();
        using var writer = new BinaryWriter(memoryStream, Encoding.ASCII, leaveOpen: true);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(riffSize);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));

        if (includeJunkChunk)
        {
            writer.Write(Encoding.ASCII.GetBytes("JUNK"));
            writer.Write(4);
            writer.Write(new byte[4]);
        }

        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(fmtChunkSize);
        writer.Write((ushort)1); // PCM
        writer.Write((ushort)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bytesPerSample);
        writer.Write((ushort)(channels * bytesPerSample));
        writer.Write((ushort)16); // bits per sample

        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);

        const double amplitude = 0.98;
        for (int i = 0; i < totalSamplesPerChannel; i++)
        {
            var time = i / (double)sampleRate;
            var value = Math.Sin(2 * Math.PI * frequency * time) * amplitude;
            var sample = (short)Math.Round(value * short.MaxValue);

            for (int channel = 0; channel < channels; channel++)
            {
                writer.Write(sample);
            }
        }

        writer.Flush();
        return memoryStream.ToArray();
    }
}
