using Visualizer.Core;
using Xunit;

namespace Visualizer.Core.Tests;

public class SpectralEnvelopeTests
{
    [Theory]
    [InlineData(100, 0)]
    [InlineData(1000, 1)]
    [InlineData(8000, 2)]
    public void TonesDriveTheirOwnBand(int frequency, int band)
    {
        var samples = Enumerable.Range(0, 48000).Select(i => (float)(0.5 * Math.Sin(2 * Math.PI * frequency * i / 48000))).ToArray();
        var envelope = SpectralEnvelope.FromInterleaved(samples, 48000, 1);
        var value = envelope.GetValueAtTime(0.5);
        var bands = new[] { value.Bass, value.Mid, value.High };
        Assert.InRange(bands[band], 0.9, 1);
        for (var i = 0; i < 3; i++) if (i != band) Assert.InRange(bands[i], 0, 0.08);
    }

    [Fact]
    public void OppositePhaseStereoDoesNotCancel()
    {
        var samples = Enumerable.Range(0, 48000).SelectMany(i =>
        {
            var value = (float)(0.5 * Math.Sin(2 * Math.PI * 100 * i / 48000));
            return new[] { value, -value };
        }).ToArray();
        Assert.InRange(SpectralEnvelope.FromInterleaved(samples, 48000, 2).GetValueAtTime(0.5).Bass, 0.9, 1);
    }

    [Fact]
    public void SilenceAndShortClipsAreFinite()
    {
        Assert.Equal(default, SpectralEnvelope.FromInterleaved(Array.Empty<float>(), 48000, 1).GetValueAtTime(0));
        Assert.Equal(default, SpectralEnvelope.FromInterleaved(new float[100], 48000, 1).GetValueAtTime(0));
        var value = SpectralEnvelope.FromInterleaved(new[] { 0.5f }, 48000, 1).GetValueAtTime(0);
        Assert.True(double.IsFinite(value.Bass + value.Mid + value.High));
    }
}
