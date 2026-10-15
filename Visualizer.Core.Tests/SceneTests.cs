using Visualizer.Core;
using Xunit;

namespace Visualizer.Core.Tests;

public class SceneTests
{
    private static byte[] Render(SceneFrameRenderer renderer, AudioFeatures features, double time = 2) => renderer.RenderToRgba(features, time).ToArray();

    [Theory]
    [InlineData(SceneKind.NightDrive)]
    [InlineData(SceneKind.Skyline)]
    [InlineData(SceneKind.DesertHighway)]
    public void SceneryChangesWithTimeAndEachFrequencyBand(SceneKind kind)
    {
        using var renderer = new SceneFrameRenderer(320, 180, new(kind, "Phases", "Imra"));
        var quiet = Render(renderer, default);
        Assert.Equal(320 * 180 * 4, quiet.Length);
        Assert.False(quiet.SequenceEqual(Render(renderer, new(0, 1, 0, 0))));
        Assert.False(quiet.SequenceEqual(Render(renderer, new(0, 0, 1, 0))));
        Assert.False(quiet.SequenceEqual(Render(renderer, new(0, 0, 0, 1))));
        Assert.False(quiet.SequenceEqual(Render(renderer, default, 3)));
        Assert.True(quiet.SequenceEqual(Render(renderer, default)));
    }

    [Fact]
    public void MetadataIsVisibleAndRainCanBeDisabled()
    {
        using var plain = new SceneFrameRenderer(320, 180, new(SceneKind.NightDrive));
        using var titled = new SceneFrameRenderer(320, 180, new(SceneKind.NightDrive, "Phases", "Imra"));
        using var dry = new SceneFrameRenderer(320, 180, new(SceneKind.NightDrive, Rain: false));
        var a = Render(plain, default); var b = Render(titled, default);
        Assert.False(a.Take(320 * 40 * 4).SequenceEqual(b.Take(320 * 40 * 4)));
        Assert.False(a.SequenceEqual(Render(dry, default)));
    }

    [Fact]
    public void ZeroStrengthDisablesAudioMotion()
    {
        using var renderer = new SceneFrameRenderer(320, 180, new(SceneKind.NightDrive, BassStrength: 0, HighStrength: 0));
        Assert.True(Render(renderer, default).SequenceEqual(Render(renderer, new(0, 1, 0, 1))));
    }

    [Fact]
    public void OptionsParseWithoutBreakingPositionalArguments()
    {
        var result = SceneArguments.Parse(new[] { "input.wav", "pipe", "--scene", "night-drive", "--title", "Phases", "--artist", "Imra", "--no-rain", "--bass-strength", "0.5" });
        Assert.Equal(new[] { "input.wav", "pipe" }, result.Positional);
        Assert.Equal(SceneKind.NightDrive, result.Scene.Scene);
        Assert.Equal("Phases", result.Scene.Title);
        Assert.Equal("Imra", result.Scene.Artist);
        Assert.False(result.Scene.Rain);
        Assert.Equal(0.5, result.Scene.BassStrength);
        Assert.Equal(SceneKind.Bars, SceneArguments.Parse(new[] { "input.wav" }).Scene.Scene);
        Assert.Throws<ArgumentException>(() => SceneArguments.Parse(new[] { "--scene", "missing" }));
        Assert.Throws<ArgumentException>(() => SceneArguments.Parse(new[] { "--title" }));
        Assert.Throws<ArgumentOutOfRangeException>(() => SceneArguments.Parse(new[] { "--high-strength", "NaN" }));
    }
}
