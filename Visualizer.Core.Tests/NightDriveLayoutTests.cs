using Visualizer.Core;
using Xunit;

namespace Visualizer.Core.Tests;

public class NightDriveLayoutTests
{
    [Fact]
    public void BuildingsHaveIndependentSizesAndThreeRowsOnBothSides()
    {
        var buildings = NightDriveLayout.BuildingsAt(3).ToArray();
        foreach (var side in new[] { -1, 1 })
            Assert.Equal(new[] { 0, 1, 2 }, buildings.Where(b => b.Side == side).Select(b => b.Row).Distinct().OrderBy(x => x));
        Assert.Contains(buildings, b => b.Height < 20);
        Assert.Contains(buildings, b => b.Height > 80);
        Assert.Contains(buildings, b => b.Width < 10);
        Assert.Contains(buildings, b => b.Width > 20);
        Assert.Equal(3, buildings.Select(b => b.RoofStyle).Distinct().Count());
        Assert.True(buildings.Where(b => b.Row == 2).Min(b => b.Setback) > buildings.Where(b => b.Row == 0).Max(b => b.Setback));
    }

    [Fact]
    public void WorldProfilesStayStableWhileApproaching()
    {
        var first = NightDriveLayout.BuildingsAt(0).ToDictionary(b => b.Seed);
        var next = NightDriveLayout.BuildingsAt(0.1).ToDictionary(b => b.Seed);
        var compared = 0;
        foreach (var (seed, a) in first)
        {
            if (!next.TryGetValue(seed, out var b) || a.Distance < 10) continue;
            Assert.Equal(a with { Distance = b.Distance }, b);
            Assert.InRange(a.Distance - b.Distance, 1.79, 1.81);
            compared++;
        }
        Assert.True(compared > 90);
    }

    [Fact]
    public void RampsHaveClearanceThroughTheBuildingRows()
    {
        var exit = NightDriveLayout.LandmarksAt(8).First(x => x.Kind == RoadLandmarkKind.Exit && x.Side == 1);
        foreach (var b in NightDriveLayout.BuildingsAt(8).Where(b => b.Side == exit.Side))
        {
            var u = (exit.Distance + 65 - b.Distance) / 160;
            if (u < 0 || u > 1) continue;
            var center = NightDriveLayout.ExitCenter(u);
            Assert.False(center + 6 > b.Setback && center - 6 < b.Setback + b.Width);
        }
    }

    [Fact]
    public void LandmarksApproachThenPassAndExitsAlternate()
    {
        var a = NightDriveLayout.LandmarksAt(25).Single(x => x.Kind == RoadLandmarkKind.Flyover);
        var b = NightDriveLayout.LandmarksAt(26).Single(x => x.Kind == RoadLandmarkKind.Flyover);
        Assert.Equal(18f, a.Distance - b.Distance);
        // The passed bridge is gone; the following bridge may just be visible at the far horizon.
        Assert.DoesNotContain(NightDriveLayout.LandmarksAt(28), x => x.Kind == RoadLandmarkKind.Flyover && x.Distance < 100);
        var right = NightDriveLayout.LandmarksAt(14).Single(x => x.Kind == RoadLandmarkKind.Exit && Math.Abs(x.Distance) < 1);
        var left = NightDriveLayout.LandmarksAt(56).Single(x => x.Kind == RoadLandmarkKind.Exit && Math.Abs(x.Distance) < 1);
        Assert.Equal(1, right.Side);
        Assert.Equal(-1, left.Side);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13.9)]
    [InlineData(14)]
    [InlineData(27.7)]
    [InlineData(28)]
    [InlineData(42)]
    [InlineData(56)]
    public void LandmarkTransitionsRenderWithoutCorruption(double time)
    {
        using var renderer = new SceneFrameRenderer(320, 180, new(SceneKind.NightDrive, "Phases", "Imra"));
        var pixels = renderer.RenderToRgba(new(0.5, 0.5, 0.5, 0.5), time).ToArray();
        Assert.Equal(320 * 180 * 4, pixels.Length);
        for (var i = 3; i < pixels.Length; i += 4) Assert.Equal(255, pixels[i]);
        Assert.True(pixels.SequenceEqual(renderer.RenderToRgba(new(0.5, 0.5, 0.5, 0.5), time).ToArray()));
    }
}
