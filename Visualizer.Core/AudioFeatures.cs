namespace Visualizer.Core;

/// <summary>Smoothed, track-normalized controls in the range 0..1.</summary>
public readonly record struct AudioFeatures(double Level, double Bass, double Mid, double High);
