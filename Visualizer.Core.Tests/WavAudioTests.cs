using System;
using System.Collections.Generic;
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

        const double amplitude = 0.98;
        var samples = new short[totalSamplesPerChannel * channels];
        for (int i = 0; i < totalSamplesPerChannel; i++)
        {
            var time = i / (double)sampleRate;
            var value = Math.Sin(2 * Math.PI * frequency * time) * amplitude;
            var sample = (short)Math.Round(value * short.MaxValue);

            for (int channel = 0; channel < channels; channel++)
            {
                samples[(i * channels) + channel] = sample;
            }
        }

        return CreatePcmWav16Bit(sampleRate, channels, samples, includeJunkChunk);
    }

    [Fact]
    public void Load_AllowsDataChunkBeforeFmt()
    {
        const int sampleRate = 8000;
        const int channels = 1;
        var samples = new short[] { 0, short.MaxValue };

        var wavBytes = CreatePcmWav16Bit(sampleRate, channels, samples, dataBeforeFmt: true);

        using var stream = new MemoryStream(wavBytes);
        var audio = WavAudio.Load(stream);

        Assert.Equal(sampleRate, audio.SampleRate);
        Assert.Equal(channels, audio.Channels);
        Assert.Equal(samples.Length, audio.Samples.Length);
        Assert.InRange(audio.Samples[1], 0.99f, 1.01f);
    }

    [Fact]
    public void Load_SupportsExtendedFmtChunkSize()
    {
        const int sampleRate = 44100;
        const int channels = 2;
        var samples = new short[] { short.MinValue, short.MaxValue, 0, short.MaxValue };

        var wavBytes = CreatePcmWav16Bit(sampleRate, channels, samples, fmtChunkSize: 18);

        using var stream = new MemoryStream(wavBytes);
        var audio = WavAudio.Load(stream);

        Assert.Equal(sampleRate, audio.SampleRate);
        Assert.Equal(channels, audio.Channels);
        Assert.Equal(samples.Length, audio.Samples.Length);
        Assert.InRange(audio.Samples[1], 0.99f, 1.01f);
        Assert.InRange(audio.Samples[2], -0.01f, 0.01f);
    }

    [Fact]
    public void Load_SkipsOddSizedChunkPadding()
    {
        const int sampleRate = 16000;
        const int channels = 1;
        var samples = new short[] { 0, short.MaxValue, short.MinValue, 0 };

        var wavBytes = CreatePcmWav16Bit(sampleRate, channels, samples, includeJunkChunk: true, junkChunkSize: 3);

        using var stream = new MemoryStream(wavBytes);
        var audio = WavAudio.Load(stream);

        Assert.Equal(sampleRate, audio.SampleRate);
        Assert.Equal(channels, audio.Channels);
        Assert.Equal(samples.Length, audio.Samples.Length);
    }

    [Fact]
    public void Load_ThrowsOnNonPcmAudioFormat()
    {
        const int sampleRate = 8000;
        const int channels = 1;
        var samples = new short[] { 0, short.MaxValue };

        var wavBytes = CreatePcmWav16Bit(sampleRate, channels, samples, audioFormat: 3);

        using var stream = new MemoryStream(wavBytes);

        Assert.Throws<NotSupportedException>(() => WavAudio.Load(stream));
    }

    private static byte[] CreatePcmWav16Bit(
        int sampleRate,
        int channels,
        IReadOnlyList<short> samples,
        bool includeJunkChunk = false,
        bool dataBeforeFmt = false,
        int fmtChunkSize = 16,
        ushort audioFormat = 1,
        int junkChunkSize = 4)
    {
        if (fmtChunkSize < 16)
        {
            throw new ArgumentOutOfRangeException(nameof(fmtChunkSize), "fmt chunk size must be at least 16 bytes.");
        }

        if (includeJunkChunk && junkChunkSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(junkChunkSize), "JUNK chunk size must be positive.");
        }

        const int bytesPerSample = 2;
        var dataSize = samples.Count * bytesPerSample;
        var fmtExtraBytes = fmtChunkSize - 16;

        var chunks = new List<(string Id, int Size, Action<BinaryWriter> WriteContent)>();

        if (includeJunkChunk)
        {
            chunks.Add(("JUNK", junkChunkSize, writer => writer.Write(new byte[junkChunkSize])));
        }

        var fmtChunk = ("fmt ", fmtChunkSize, (Action<BinaryWriter>)(writer =>
        {
            writer.Write(audioFormat);
            writer.Write((ushort)channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * bytesPerSample);
            writer.Write((ushort)(channels * bytesPerSample));
            writer.Write((ushort)16); // bits per sample

            if (fmtExtraBytes > 0)
            {
                writer.Write(new byte[fmtExtraBytes]);
            }
        }));

        var dataChunk = ("data", dataSize, (Action<BinaryWriter>)(writer =>
        {
            foreach (var sample in samples)
            {
                writer.Write(sample);
            }
        }));

        if (dataBeforeFmt)
        {
            chunks.Add(dataChunk);
            chunks.Add(fmtChunk);
        }
        else
        {
            chunks.Add(fmtChunk);
            chunks.Add(dataChunk);
        }

        static int Padding(int size) => (size & 1) == 1 ? 1 : 0;

        var riffSize = 4 + chunks.Sum(chunk => 8 + chunk.Size + Padding(chunk.Size));

        using var memoryStream = new MemoryStream();
        using var writer = new BinaryWriter(memoryStream, Encoding.ASCII, leaveOpen: true);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(riffSize);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));

        foreach (var (id, size, writeContent) in chunks)
        {
            writer.Write(Encoding.ASCII.GetBytes(id));
            writer.Write(size);
            writeContent(writer);

            if ((size & 1) == 1)
            {
                writer.Write((byte)0);
            }
        }

        writer.Flush();
        return memoryStream.ToArray();
    }
}
