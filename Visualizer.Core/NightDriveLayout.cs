namespace Visualizer.Core;

internal enum RoadLandmarkKind { Exit, Flyover }
internal readonly record struct RoadLandmark(RoadLandmarkKind Kind, float Distance, int Side);
internal readonly record struct CityBuilding(int Seed, int Row, int Side, float Distance,
    float Setback, float Width, float Height, int RoofStyle);

/// <summary>A repeatable world in metres. Profiles belong to world blocks, never to individual frames.</summary>
internal static class NightDriveLayout
{
    internal const float Speed = 18;
    internal const float Near = 4;
    internal const float Far = 760;
    internal const float Horizon = 625;
    internal const float RoadHalfWidth = 8.7f;
    internal const double LandmarkInterval = 42;

    internal static float Noise(int seed)
    {
        // Integer mixing avoids correlations between nearby building dimensions.
        var value = unchecked((uint)seed * 747796405u + 2891336453u);
        value = unchecked(((value >> (int)((value >> 28) + 4)) ^ value) * 277803737u);
        return ((value >> 22) ^ value) / (float)uint.MaxValue;
    }

    private static readonly CityBuilding[] Blocks = CreateBlocks();

    private static CityBuilding[] CreateBlocks()
    {
        var blocks = new List<CityBuilding>();
        for (var row = 0; row < 3; row++)
        for (var side = -1; side <= 1; side += 2)
        for (var index = 0; index < 24; index++)
        {
            var seed = 1000 + row * 1000 + (side + 1) * 200 + index;
            var kind = Noise(seed + 57);
            var height = kind < 0.35f ? 9 + Noise(seed + 17) * 12
                : kind < 0.78f ? 25 + Noise(seed + 17) * 26
                : 58 + Noise(seed + 17) * (22 + row * 24);
            var width = kind > 0.78f ? 7 + Noise(seed + 91) * 7 : 10 + Noise(seed + 91) * 16;
            blocks.Add(new(seed, row, side, index * (Far / 24) + Noise(seed + 8) * 8,
                14 + row * 27 + Noise(seed + 37) * 7, width, height, (int)(Noise(seed + 72) * 3)));
        }
        return blocks.ToArray();
    }

    internal static IEnumerable<CityBuilding> BuildingsAt(double seconds)
    {
        var travel = (float)((seconds * Speed) % Far);
        var exits = LandmarksAt(seconds).Where(x => x.Kind == RoadLandmarkKind.Exit).ToArray();
        foreach (var building in Blocks)
        {
            var distance = (building.Distance - travel + Far) % Far;
            if (distance < Near) continue;
            // Leave physical space for the ramp through the city blocks on its side of the road.
            if (exits.Any(exit =>
            {
                var u = (exit.Distance + 65 - distance) / 160;
                return exit.Side == building.Side && u >= 0 && u <= 1 &&
                    ExitCenter(u) + 6 > building.Setback && ExitCenter(u) - 6 < building.Setback + building.Width;
            })) continue;
            yield return building with { Distance = distance };
        }
    }

    internal static float ExitCenter(float u) => 10.7f + 38 * u * u * (3 - 2 * u);

    internal static IEnumerable<RoadLandmark> LandmarksAt(double seconds)
    {
        // Alternate exits each interval. Modulo first keeps long renders numerically bounded.
        var travel = (float)((seconds % (LandmarkInterval * 2)) * Speed);
        for (var cycle = -1; cycle <= 2; cycle++)
        {
            var offset = cycle * (float)LandmarkInterval * Speed - travel;
            var exit = 14 * Speed + offset;
            if (exit >= -70 && exit <= Far + 100)
                yield return new(RoadLandmarkKind.Exit, exit, (cycle & 1) == 0 ? 1 : -1);
            var flyover = 28 * Speed + offset;
            if (flyover > Near && flyover <= Far)
                yield return new(RoadLandmarkKind.Flyover, flyover, 0);
        }
    }
}
