using System;
using System.Collections.Generic;

namespace Visualizer.Core;

public sealed class RmsEnvelope
{
    private readonly double[] _times;
    private readonly double[] _values;

    private RmsEnvelope(double[] times, double[] values, double durationSeconds, int sampleRate)
    {
        _times = times;
        _values = values;
        DurationSeconds = durationSeconds;
        SampleRate = sampleRate;
    }

    public IReadOnlyList<double> Times => _times;

    public IReadOnlyList<double> Values => _values;

    public double DurationSeconds { get; }

    public int SampleRate { get; }

    public static RmsEnvelope FromInterleaved(float[] samples, int sampleRate, int channels, int windowSize, int hopSize)
    {
        if (samples is null)
        {
            throw new ArgumentNullException(nameof(samples));
        }

        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate must be positive.");
        }

        if (channels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(channels), "Channel count must be positive.");
        }

        if (windowSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(windowSize), "Window size must be positive.");
        }

        if (hopSize <= 0 || hopSize > windowSize)
        {
            throw new ArgumentOutOfRangeException(nameof(hopSize), "Hop size must be positive and no greater than the window size.");
        }

        if (samples.Length % channels != 0)
        {
            throw new ArgumentException("Sample buffer length must be divisible by channel count.", nameof(samples));
        }

        var totalFrames = samples.Length / channels;
        if (totalFrames == 0)
        {
            return new RmsEnvelope(Array.Empty<double>(), Array.Empty<double>(), 0, sampleRate);
        }

        var values = new List<double>();
        var times = new List<double>();

        for (int frameStart = 0; frameStart < totalFrames; frameStart += hopSize)
        {
            var framesInWindow = Math.Min(windowSize, totalFrames - frameStart);

            double sumSquares = 0;
            for (int frame = 0; frame < framesInWindow; frame++)
            {
                double monoSample = 0;
                var baseIndex = (frameStart + frame) * channels;
                for (int channel = 0; channel < channels; channel++)
                {
                    monoSample += samples[baseIndex + channel];
                }

                monoSample /= channels;
                sumSquares += monoSample * monoSample;
            }

            var rms = Math.Sqrt(sumSquares / framesInWindow);
            values.Add(rms);

            var centerFrame = frameStart + (framesInWindow / 2.0);
            times.Add(centerFrame / sampleRate);
        }

        var durationSeconds = totalFrames / (double)sampleRate;
        return new RmsEnvelope(times.ToArray(), values.ToArray(), durationSeconds, sampleRate);
    }

    public double GetValueAtTime(double t)
    {
        if (_values.Length == 0)
        {
            return 0;
        }

        if (t <= _times[0])
        {
            return _values[0];
        }

        var lastIndex = _values.Length - 1;
        if (t >= _times[lastIndex])
        {
            return _values[lastIndex];
        }

        var index = Array.BinarySearch(_times, t);
        if (index >= 0)
        {
            return _values[index];
        }

        var upper = ~index;
        var lower = upper - 1;

        var timeSpan = _times[upper] - _times[lower];
        if (timeSpan <= 0)
        {
            return _values[lower];
        }

        var ratio = (t - _times[lower]) / timeSpan;
        return _values[lower] + ((_values[upper] - _values[lower]) * ratio);
    }

    public double GetValueAtSample(long sampleIndex)
    {
        if (sampleIndex <= 0)
        {
            return GetValueAtTime(0);
        }

        var time = sampleIndex / (double)SampleRate;
        return GetValueAtTime(time);
    }
}
