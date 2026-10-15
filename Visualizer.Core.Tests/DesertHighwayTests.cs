using Visualizer.Core;
using Xunit;

namespace Visualizer.Core.Tests;

public class DesertHighwayTests
{
    [Fact]
    public void SceneIsSelectableAndRetainsMetadata()
    {
        var result = SceneArguments.Parse(new[] { "song.wav", "--scene", "desert-highway", "--title", "Phases", "--artist", "Imra" });
        Assert.Equal(SceneKind.DesertHighway, result.Scene.Scene);
        Assert.Equal("Phases", result.Scene.Title);
        Assert.Equal("Imra", result.Scene.Artist);
    }

    [Fact]
    public void PlantsAndRocksKeepTheirProfilesWhileDriving()
    {
        var first = DesertHighwayLayout.CountryAt(0).ToDictionary(p => p.Seed);
        var next = DesertHighwayLayout.CountryAt(0.1).ToDictionary(p => p.Seed);
        Assert.Contains(first.Values, p => p.Kind == DesertPropKind.Cactus);
        Assert.Contains(first.Values, p => p.Kind == DesertPropKind.Bush);
        Assert.Contains(first.Values, p => p.Kind == DesertPropKind.Rock);
        Assert.Contains(first.Values, p => p.X < 0);
        Assert.Contains(first.Values, p => p.X > 0);
        var compared = 0;
        foreach (var (seed, a) in first)
        {
            if (a.Distance < 10 || !next.TryGetValue(seed, out var b)) continue;
            Assert.Equal(a with { Distance = b.Distance }, b);
            Assert.InRange(a.Distance - b.Distance, 2.19, 2.21);
            compared++;
        }
        Assert.True(compared > 140);
    }

    [Fact]
    public void TownHasBusinessesAndOpenHighwayBetweenSettlements()
    {
        var town = DesertHighwayLayout.TownAt(8).ToArray();
        Assert.Contains(town, p => p.Kind == DesertPropKind.GasStation);
        Assert.Contains(town, p => p.Kind == DesertPropKind.Motel);
        Assert.Contains(town, p => p.Kind == DesertPropKind.Diner);
        Assert.Contains(town, p => p.Kind == DesertPropKind.WaterTower);
        Assert.Contains(town, p => p.Kind == DesertPropKind.TownSign);
        Assert.All(town, p => Assert.True(Math.Abs(p.X) - p.Width / 2 > 6.5));
        Assert.DoesNotContain(DesertHighwayLayout.TownAt(34), p => p.Distance < 100);
        Assert.Equal(DesertHighwayLayout.TownAt(8).ToArray(), DesertHighwayLayout.TownAt(128).ToArray());
    }

    [Fact]
    public void DesertUsesDustWithoutCityRainAndBandStrengthsCanBeDisabled()
    {
        using var dry = new SceneFrameRenderer(320, 180, new(SceneKind.DesertHighway, Rain: false, BassStrength: 0, HighStrength: 0));
        using var defaults = new SceneFrameRenderer(320, 180, new(SceneKind.DesertHighway, BassStrength: 0, HighStrength: 0));
        var a = dry.RenderToRgba(default, 5).ToArray();
        Assert.True(a.SequenceEqual(defaults.RenderToRgba(new(0, 1, 0, 1), 5).ToArray()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(17.8)]
    [InlineData(18)]
    [InlineData(21)]
    [InlineData(34)]
    [InlineData(78)]
    public void TownApproachAndPassageRenderOpaqueFrames(double time)
    {
        using var renderer = new SceneFrameRenderer(320, 180, new(SceneKind.DesertHighway, "Phases", "Imra"));
        var pixels = renderer.RenderToRgba(new(0.5, 0.6, 0.5, 0.8), time).ToArray();
        Assert.Equal(320 * 180 * 4, pixels.Length);
        for (var i = 3; i < pixels.Length; i += 4) Assert.Equal(255, pixels[i]);
        Assert.True(pixels.SequenceEqual(renderer.RenderToRgba(new(0.5, 0.6, 0.5, 0.8), time).ToArray()));
    }
}
