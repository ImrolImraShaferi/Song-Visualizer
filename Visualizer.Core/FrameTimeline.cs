// Visualizer.Core/FrameTimeline.cs
using System;

namespace Visualizer.Core;

public sealed class FrameTimeline
{
    public FrameTimeline(int sampleRate, double fps)
    {
        if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate must be positive.");
        if (!double.IsFinite(fps) || fps <= 0 || sampleRate / fps >= long.MaxValue) throw new ArgumentOutOfRangeException(nameof(fps), "FPS must be finite, positive, and yield a representable sample interval.");

        SampleRate = sampleRate;
        Fps = fps;

        // Reduce ratio sampleRate/fps to avoid overflow in long renders.
        // fps might be non-integer (e.g., 29.97), so approximate as rational.
        // For common integer FPS, this becomes exact.
        (Numerator, Denominator) = ToRational(sampleRate / fps);
    }

    public int SampleRate { get; }
    public double Fps { get; }

    // sampleIndex = round(frameIndex * Numerator / Denominator)
    // where Numerator/Denominator ≈ sampleRate/fps.
    public long Numerator { get; }
    public long Denominator { get; }

    public long GetSampleIndexForFrame(long frameIndex)
    {
        if (frameIndex <= 0) return 0;

        checked
        {
            // Round to nearest, ties away from zero.
            // sample = round(frameIndex * Numerator / Denominator)
            // => (frameIndex*Numerator + Denominator/2) / Denominator
            var n = (Int128)frameIndex * Numerator;
            return checked((long)((n + (Denominator / 2)) / Denominator));
        }
    }

    // Good enough for integer FPS; stable for common fractional FPS too.
    // Uses a bounded denominator to avoid crazy rationals.
    private static (long num, long den) ToRational(double value, long maxDen = 1_000_000)
    {
        // Continued fraction approximation
        long a0 = (long)Math.Floor(value);
        if (Math.Abs(value - a0) < 1e-12) return (a0, 1);

        long p0 = 1, q0 = 0;
        long p1 = a0, q1 = 1;

        double frac = value - a0;

        while (true)
        {
            if (frac == 0) break;
            double inv = 1.0 / frac;
            if (!double.IsFinite(inv) || inv > (maxDen - q0) / q1) break;
            long a = (long)Math.Floor(inv);

            var nextNumerator = (Int128)a * p1 + p0;
            var nextDenominator = (Int128)a * q1 + q0;
            if (nextNumerator > long.MaxValue || nextDenominator > maxDen) break;
            long p2 = (long)nextNumerator;
            long q2 = (long)nextDenominator;


            p0 = p1; q0 = q1;
            p1 = p2; q1 = q2;

            frac = inv - a;
        }

        // Reduce
        var g = Gcd(Math.Abs(p1), q1);
        return (p1 / g, q1 / g);
    }

    private static long Gcd(long a, long b)
    {
        while (b != 0)
        {
            var t = a % b;
            a = b;
            b = t;
        }
        return a == 0 ? 1 : a;
    }
}
