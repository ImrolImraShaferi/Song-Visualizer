namespace Visualizer.Core;

internal enum DesertPropKind { Cactus, Bush, Rock, House, Diner, Motel, GasStation, WaterTower, TownSign }
internal readonly record struct DesertProp(int Seed, DesertPropKind Kind, float X, float Distance, float Width, float Height);

/// <summary>Fixed world profiles, with empty highway between small settlements.</summary>
internal static class DesertHighwayLayout
{
    internal const float Speed = 22;
    internal const float Far = 900;
    internal const float Near = 4;
    internal const double TownInterval = 60;
    internal static float Noise(int seed) => NightDriveLayout.Noise(seed + 14000);
    private static readonly DesertProp[] Country = CreateCountry();

    private static DesertProp[] CreateCountry()
    {
        var props = new List<DesertProp>();
        for (var i = 0; i < 210; i++)
        {
            var seed = 5000 + i;
            var side = i % 2 == 0 ? -1 : 1;
            var kind = i % 5 < 2 ? DesertPropKind.Cactus : i % 5 < 4 ? DesertPropKind.Bush : DesertPropKind.Rock;
            var height = kind == DesertPropKind.Cactus ? 2.5f + Noise(seed + 7) * 4.5f : 0.4f + Noise(seed + 7) * 1.1f;
            props.Add(new(seed, kind, side * (11 + Noise(seed + 13) * 105), Noise(seed + 31) * Far,
                kind == DesertPropKind.Cactus ? 0.5f : 1.3f + Noise(seed + 41) * 2, height));
        }
        return props.ToArray();
    }

    internal static IEnumerable<DesertProp> CountryAt(double seconds)
    {
        var travel = (float)((seconds * Speed) % Far);
        var town = TownAt(seconds).ToArray();
        foreach (var prop in Country)
        {
            var distance = (prop.Distance - travel + Far) % Far;
            if (distance < Near) continue;
            if (town.Any(b => Math.Abs(b.Distance - distance) < 16 && Math.Abs(b.X - prop.X) < b.Width / 2 + 5)) continue;
            yield return prop with { Distance = distance };
        }
    }

    internal static IEnumerable<DesertProp> TownAt(double seconds)
    {
        var travel = (float)((seconds % (TownInterval * 2)) * Speed);
        for (var cycle = -1; cycle <= 2; cycle++)
        {
            var center = 18 * Speed + cycle * (float)TownInterval * Speed - travel;
            for (var i = 0; i < 8; i++)
            {
                var seed = 8000 + (cycle & 1) * 100 + i;
                var side = i % 2 == 0 ? -1 : 1;
                var kind = i == 1 ? DesertPropKind.GasStation : i == 2 ? DesertPropKind.Diner
                    : i == 5 ? DesertPropKind.Motel : DesertPropKind.House;
                var distance = center - 100 + (i / 2) * 48 + (side > 0 ? 14 : 0);
                if (distance < Near || distance > Far) continue;
                yield return new(seed, kind, side * (kind == DesertPropKind.GasStation ? 34 : 19 + Noise(seed) * 9), distance,
                    11 + Noise(seed + 9) * 12, 3.5f + Noise(seed + 23) * 2.5f);
            }
            var tower = center + 90;
            if (tower >= Near && tower <= Far) yield return new(8500 + (cycle & 1), DesertPropKind.WaterTower, -55, tower, 7, 17);
            var sign = center - 160;
            if (sign >= Near && sign <= Far) yield return new(8600 + (cycle & 1), DesertPropKind.TownSign, 10, sign, 5, 3.8f);
        }
    }
}
