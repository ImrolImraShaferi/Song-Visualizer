// Visualizer.Core.Tests/FrameTimelineTests.cs
using Visualizer.Core;
using Xunit;

namespace Visualizer.Core.Tests;

public class FrameTimelineTests
{
    [Fact]
    public void GetSampleIndexForFrame_FrameZero_IsZero()
    {
        var t = new FrameTimeline(sampleRate: 48000, fps: 60);
        Assert.Equal(0, t.GetSampleIndexForFrame(0));
    }

    [Fact]
    public void GetSampleIndexForFrame_IntegerFps_IsExact()
    {
        var t = new FrameTimeline(sampleRate: 48000, fps: 60);

        Assert.Equal(0, t.GetSampleIndexForFrame(0));
        Assert.Equal(800, t.GetSampleIndexForFrame(1));
        Assert.Equal(1600, t.GetSampleIndexForFrame(2));
        Assert.Equal(48000, t.GetSampleIndexForFrame(60));
        Assert.Equal(48000 * 60, t.GetSampleIndexForFrame(60 * 60)); // 1 minute at 60fps => 3600 frames
    }

    [Fact]
    public void GetSampleIndexForFrame_DoesNotAccumulateDrift()
    {
        var t = new FrameTimeline(sampleRate: 44100, fps: 60);

        // 10 minutes at 60 fps = 36,000 frames
        var frames = 60L * 10L * 60L;
        var expectedSamples = 44100L * 10L * 60L;

        Assert.Equal(expectedSamples, t.GetSampleIndexForFrame(frames));
    }

    [Fact]
    public void Constructor_RejectsInvalidArgs()
    {
        Assert.ThrowsAny<System.ArgumentOutOfRangeException>(() => new FrameTimeline(0, 60));
        Assert.ThrowsAny<System.ArgumentOutOfRangeException>(() => new FrameTimeline(48000, 0));
    }

    [Fact]
    public void FractionalFps_IsStableAndMonotonic()
    {
        var t = new FrameTimeline(sampleRate: 48000, fps: 29.97);

        long prev = -1;
        for (long i = 0; i < 10_000; i++)
        {
            var s = t.GetSampleIndexForFrame(i);
            Assert.True(s >= prev);
            prev = s;
        }

        // Rough sanity: after ~29.97 frames, ~1 second => ~48000 samples
        var samplesAt30Frames = t.GetSampleIndexForFrame(30);
        Assert.InRange(samplesAt30Frames, 45_000, 55_000);
    }
}
