using System;
using System.Linq;
using Visualizer.Core;
using Xunit;

namespace Visualizer.Core.Tests;

public class RmsEnvelopeTests
{
    [Fact]
    public void FromInterleaved_ComputesSineWaveRms()
    {
        const int sampleRate = 48000;
        const int channels = 2;
        const double durationSeconds = 1.0;
        const double amplitude = 0.8;
        const double frequency = 440.0;

        var samples = CreateSineWave(amplitude, frequency, sampleRate, channels, durationSeconds);

        var envelope = RmsEnvelope.FromInterleaved(samples, sampleRate, channels, windowSize: 1024);

        var expectedRms = amplitude / Math.Sqrt(2);
        Assert.NotEmpty(envelope.Values);
        Assert.All(envelope.Values, value => Assert.InRange(value, expectedRms * 0.97, expectedRms * 1.03));
    }

    [Fact]
    public void FromInterleaved_ComputesSilenceRms()
    {
        const int sampleRate = 44100;
        const int channels = 1;
        var samples = new float[sampleRate];

        var envelope = RmsEnvelope.FromInterleaved(samples, sampleRate, channels, windowSize: 512);

        Assert.NotEmpty(envelope.Values);
        Assert.All(envelope.Values, value => Assert.InRange(value, 0, 1e-6));
    }

    [Fact]
    public void GetValueAtTime_InterpolatesAndClamps()
    {
        const int sampleRate = 1000;
        const int channels = 1;
        var samples = new float[]
        {
            1, 1, 1, 1,  // RMS 1
            0, 0, 0, 0   // RMS 0
        };

        var envelope = RmsEnvelope.FromInterleaved(samples, sampleRate, channels, windowSize: 4);

        Assert.Equal(envelope.Values.First(), envelope.GetValueAtTime(-1));
        Assert.Equal(envelope.Values.Last(), envelope.GetValueAtTime(10));

        var midTime = (envelope.Times[0] + envelope.Times[1]) / 2;
        var interpolated = envelope.GetValueAtTime(midTime);

        Assert.InRange(interpolated, 0.45, 0.55);
    }

    private static float[] CreateSineWave(double amplitude, double frequency, int sampleRate, int channels, double durationSeconds)
    {
        var totalSamplesPerChannel = (int)Math.Round(durationSeconds * sampleRate);
        var samples = new float[totalSamplesPerChannel * channels];

        for (int i = 0; i < totalSamplesPerChannel; i++)
        {
            var sampleValue = (float)(Math.Sin(2 * Math.PI * frequency * (i / (double)sampleRate)) * amplitude);

            for (int channel = 0; channel < channels; channel++)
            {
                samples[(i * channels) + channel] = sampleValue;
            }
        }

        return samples;
    }
}
