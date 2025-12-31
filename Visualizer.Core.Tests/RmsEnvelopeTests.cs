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

        var envelope = RmsEnvelope.FromInterleaved(samples, sampleRate, channels, windowSize: 1024, hopSize: 1024);

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

        var envelope = RmsEnvelope.FromInterleaved(samples, sampleRate, channels, windowSize: 512, hopSize: 512);

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

        var envelope = RmsEnvelope.FromInterleaved(samples, sampleRate, channels, windowSize: 4, hopSize: 4);

        Assert.Equal(envelope.Values.First(), envelope.GetValueAtTime(-1));
        Assert.Equal(envelope.Values.Last(), envelope.GetValueAtTime(10));

        var midTime = (envelope.Times[0] + envelope.Times[1]) / 2;
        var interpolated = envelope.GetValueAtTime(midTime);

        Assert.InRange(interpolated, 0.45, 0.55);
    }

    [Fact]
    public void GetValueAtSample_UsesSamplePrecision()
    {
        const int sampleRate = 10;
        const int channels = 1;
        var samples = Enumerable.Repeat(1f, 30).Concat(Enumerable.Repeat(0f, 30)).ToArray();

        var envelope = RmsEnvelope.FromInterleaved(samples, sampleRate, channels, windowSize: 10, hopSize: 5);

        var earlySample = envelope.GetValueAtSample(0);
        var midSample = envelope.GetValueAtSample(30);
        var lateSample = envelope.GetValueAtSample(55);

        Assert.InRange(earlySample, 0.95, 1.05);
        Assert.InRange(midSample, 0.6, 0.9);
        Assert.InRange(lateSample, -0.01, 0.05);
    }

    [Fact]
    public void OverlappingWindows_ProducesSmoothTransition()
    {
        const int sampleRate = 16;
        const int channels = 1;
        var samples = Enumerable.Repeat(1f, 16).Concat(Enumerable.Repeat(0f, 16)).ToArray();

        var nonOverlapping = RmsEnvelope.FromInterleaved(samples, sampleRate, channels, windowSize: 8, hopSize: 8);
        var overlapping = RmsEnvelope.FromInterleaved(samples, sampleRate, channels, windowSize: 8, hopSize: 4);

        Assert.Equal(4, nonOverlapping.Values.Count); // starts: 0,8,16,24
        Assert.Equal(7, overlapping.Values.Count);    // starts: 0,4,8,12,16,20,24

        // Non-overlap should be two high then two low
        Assert.InRange(nonOverlapping.Values[0], 0.95, 1.05);
        Assert.InRange(nonOverlapping.Values[1], 0.95, 1.05);
        Assert.InRange(nonOverlapping.Values[2], -0.01, 0.05);
        Assert.InRange(nonOverlapping.Values[3], -0.01, 0.05);

        // Overlap: the mixed window is the one starting at 12 (samples 12..19 => 4 ones + 4 zeros)
        // RMS = sqrt((4*1^2)/8) = sqrt(0.5) ≈ 0.7071
        var mixed = overlapping.Values[3];
        Assert.InRange(mixed, 0.69, 0.73);

        Assert.True(overlapping.Values[2] > mixed); // window start 8 is all ones
        Assert.True(mixed > overlapping.Values[4]); // window start 16 is all zeros
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
