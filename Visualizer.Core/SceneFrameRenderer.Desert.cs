using SkiaSharp;

namespace Visualizer.Core;

public sealed partial class SceneFrameRenderer
{
    private void DesertHighway(double seconds, float bass, float mid, float high)
    {
        DesertSky(seconds, bass, high);
        Gradient(new(0, 625, 1920, 1080), C("#b77d62"), C("#785446"));
        // Flat desert tracks and dry washes recede to the same vanishing point as the highway.
        for (var i = 0; i < 18; i++)
        {
            var z = (float)((i * 50 - (seconds * DesertHighwayLayout.Speed) % DesertHighwayLayout.Far + DesertHighwayLayout.Far) % DesertHighwayLayout.Far);
            if (z < DesertHighwayLayout.Near) continue;
            foreach (var side in new[] { -1, 1 })
                WorldLine(side * 10, 0, z, side * 220, 0, z + 30, new SKColor(218, 166, 114, 32), Math.Max(1, 20 / z));
        }
        var near = DesertHighwayLayout.Near; var far = DesertHighwayLayout.Far;
        Polygon(C("#c0926c"), Project(-8.2f, 0, far), Project(8.2f, 0, far), Project(8.2f, 0, near), Project(-8.2f, 0, near));
        Polygon(C("#383a43"), Project(-6.5f, 0, far), Project(6.5f, 0, far), Project(6.5f, 0, near), Project(-6.5f, 0, near));
        foreach (var side in new[] { -1, 1 })
            WorldLine(side * 6.15f, 0.03f, far, side * 6.15f, 0.03f, near, C("#d8cbb6"), 2);
        var travel = (float)((seconds * DesertHighwayLayout.Speed) % far);
        for (var i = 0; i < 90; i++)
        {
            var z = (i * 10 - travel + far) % far;
            if (z < near || z + 4 > far) continue;
            Polygon(C("#e6b966"), Project(-0.09f, 0.04f, z), Project(0.09f, 0.04f, z),
                Project(0.09f, 0.04f, z + 4), Project(-0.09f, 0.04f, z + 4));
        }
        // Reflector posts and a utility line give the open landscape a sense of speed and scale.
        var poles = new List<float>();
        for (var i = 0; i < 18; i++)
        {
            var z = (i * 50 - travel + far) % far;
            if (z < near) continue;
            poles.Add(z);
            foreach (var side in new[] { -1, 1 })
            {
                WorldLine(side * 7.8f, 0, z, side * 7.8f, 0.9f, z, C("#d5bb96"), Math.Max(1, 70 / z));
                var dot = Project(side * 7.8f, 0.78f, z);
                _paint.Color = new SKColor(255, 207, 134, (byte)(110 + mid * 130));
                _canvas.DrawCircle(dot, Math.Max(0.6f, 26 / z), _paint);
            }
        }
        var props = DesertHighwayLayout.CountryAt(seconds).Concat(DesertHighwayLayout.TownAt(seconds)).ToArray();
        var objects = props.Select((p, i) => (Distance: p.Distance, Kind: 0, Index: i)).ToList();
        objects.AddRange(poles.Select((z, i) => (z, 1, i)));
        foreach (var item in objects.OrderByDescending(x => x.Distance))
        {
            if (item.Kind == 1) DesertUtilityPole(poles[item.Index]);
            else
            {
                var prop = props[item.Index];
                switch (prop.Kind)
                {
                    case DesertPropKind.Cactus: DesertCactus(prop, seconds, bass, high); break;
                    case DesertPropKind.Bush: DesertBush(prop, seconds, high); break;
                    case DesertPropKind.Rock: DesertRock(prop); break;
                    case DesertPropKind.WaterTower: DesertWaterTower(prop, mid); break;
                    case DesertPropKind.TownSign: DesertTownSign(prop); break;
                    default: DesertTownBuilding(prop, mid); break;
                }
            }
        }
        // Windblown dust instead of the city scene's rain; treble makes the gusts more visible.
        for (var i = 0; i < 65; i++)
        {
            var x = (float)((DesertHighwayLayout.Noise(i + 600) * 2200 + seconds * 90) % 2200) - 140;
            var y = 650 + DesertHighwayLayout.Noise(i + 610) * 430;
            Line(x, y, x + 7 + high * 13, y - 1.5f, new SKColor(241, 205, 149, (byte)(8 + high * 25)), 1);
        }
    }

    private void DesertSky(double seconds, float bass, float high)
    {
        // Adjacent antialiased sky/ground edges need an opaque base at fractional output scales.
        _canvas.Clear(C("#b77d62"));
        Gradient(new(0, 0, 1920, 625), C("#222a48"), C("#e6a67e"));
        _paint.Color = new SKColor(255, 189, 120, 18); _canvas.DrawCircle(1450, 435, 135, _paint);
        _paint.Color = new SKColor(255, 189, 120, 23); _canvas.DrawCircle(1450, 435, 100, _paint);
        _paint.Color = C("#ffd29b"); _canvas.DrawCircle(1450, 435, 65, _paint);
        for (var i = 0; i < 6; i++)
        {
            var x = (float)((i * 420 + seconds * 3 + high * 12) % 2500) - 300;
            var y = 240 + Hash(i + 900) * 180;
            _paint.Color = new SKColor(223, 159, 155, 40);
            _canvas.DrawOval(new SKRect(x, y, x + 230 + i * 17, y + 8 + high * 3), _paint);
        }
        for (var layer = 0; layer < 3; layer++)
        {
            var bottom = 625 - (2 - layer) * 12;
            var baseY = 545 + layer * 28;
            using var mountains = new SKPath();
            mountains.MoveTo(-100, bottom);
            for (var i = 0; i < 8; i++)
            {
                var x = -230 + i * 310 + Hash(i + layer * 11 + 770) * 75;
                var height = (25 + Hash(i + layer * 28 + 690) * 130) * (1 + bass * 0.018f);
                var shift = (float)Math.Sin(seconds * 0.015) * (layer + 1) * 5;
                var plateau = 70 + Hash(i + layer * 19 + 791) * 110;
                mountains.LineTo(x + shift, baseY);
                mountains.LineTo(x + 28 + shift, baseY - height * 0.3f);
                mountains.LineTo(x + 65 + shift, baseY - height);
                mountains.LineTo(x + 65 + plateau + shift, baseY - height);
                mountains.LineTo(x + 92 + plateau + shift, baseY - height * 0.38f);
                mountains.LineTo(x + 130 + plateau + shift, baseY);
            }
            mountains.LineTo(2100, bottom); mountains.Close();
            _paint.Color = layer == 0 ? C("#9b7886") : layer == 1 ? C("#926771") : C("#80564f");
            _canvas.DrawPath(mountains, _paint);
        }
        // Subtle strata catch the last light on the distant rock formations.
        for (var i = 0; i < 11; i++)
        {
            var x = Hash(i + 754) * 1900;
            Line(x, 578 + Hash(i + 755) * 20, x + 40 + Hash(i + 756) * 80, 578 + Hash(i + 755) * 20,
                new SKColor(238, 162, 109, 45), 2);
        }
    }

    private void DesertShadow(DesertProp p, float width)
    {
        var a = Project(p.X - width / 2, 0, p.Distance);
        var b = Project(p.X + width, 0, p.Distance + p.Height * 0.7f);
        _paint.Color = new SKColor(65, 55, 58, 42);
        _canvas.DrawOval(new SKRect(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y) + 2), _paint);
    }

    private void DesertCactus(DesertProp p, double seconds, float bass, float high)
    {
        var z = p.Distance; var scale = 720 / z;
        var height = p.Height * (1 + bass * 0.035f);
        var sway = (float)Math.Sin(seconds * 1.4 + p.Seed) * high * 0.09f;
        var basePoint = Project(p.X, 0, z); var tip = Project(p.X + sway, height, z);
        if (basePoint.X < -120 || basePoint.X > 2040 || basePoint.Y < 0) return;
        DesertShadow(p, 1.5f);
        var cap = _stroke.StrokeCap;
        try
        {
            _stroke.StrokeCap = SKStrokeCap.Round;
            Line(basePoint.X, basePoint.Y, tip.X, tip.Y, C("#36544c"), Math.Max(1, scale * 0.36f));
            WorldLine(p.X - 0.08f, 0.1f, z, p.X - 0.08f + sway, height - 0.08f, z,
                new SKColor(133, 155, 108, 150), Math.Max(0.5f, scale * 0.06f));
            foreach (var side in new[] { -1, 1 })
            {
                var h = height * (side < 0 ? 0.45f : 0.62f);
                var armX = p.X + side * (0.55f + DesertHighwayLayout.Noise(p.Seed + side + 10) * 0.5f) + sway;
                var a = Project(p.X + sway, h, z); var b = Project(armX, h, z); var c = Project(armX, h + height * 0.27f, z);
                using var arm = new SKPath(); arm.MoveTo(a); arm.LineTo(b); arm.LineTo(c);
                _stroke.Color = C("#36544c"); _stroke.StrokeWidth = Math.Max(1, scale * 0.26f); _canvas.DrawPath(arm, _stroke);
            }
        }
        finally { _stroke.StrokeCap = cap; }
    }

    private void DesertBush(DesertProp p, double seconds, float high)
    {
        var basePoint = Project(p.X, 0, p.Distance); var scale = 720 / p.Distance;
        if (basePoint.X < -120 || basePoint.X > 2040) return;
        DesertShadow(p, p.Width);
        var wind = (float)Math.Sin(seconds * 2 + p.Seed) * high * 0.2f;
        for (var i = 0; i < 7; i++)
        {
            var x = p.X + (DesertHighwayLayout.Noise(p.Seed + i) - 0.5f) * p.Width;
            var h = p.Height * (0.45f + DesertHighwayLayout.Noise(p.Seed + i + 20) * 0.55f);
            var top = Project(x + wind, h, p.Distance);
            Line(basePoint.X, basePoint.Y, top.X, top.Y, C("#625d45"), Math.Max(0.7f, scale * 0.065f));
            _paint.Color = i % 2 == 0 ? C("#7b8058") : C("#686b4c");
            _canvas.DrawOval(top.X, top.Y, scale * p.Width * 0.15f, scale * h * 0.3f, _paint);
        }
    }

    private void DesertRock(DesertProp p)
    {
        var x = p.X; var z = p.Distance;
        Polygon(C("#69534a"), Project(x - p.Width / 2, 0, z), Project(x + p.Width / 2, 0, z),
            Project(x + p.Width * 0.2f, p.Height, z), Project(x - p.Width * 0.25f, p.Height * 0.8f, z));
        WorldLine(x - p.Width * 0.25f, p.Height * 0.8f, z, x + p.Width * 0.2f, p.Height, z, C("#c3906a"), Math.Max(1, 25 / z));
    }

    private void DesertUtilityPole(float z)
    {
        WorldLine(25, 0, z, 25, 9, z, C("#685247"), Math.Max(0.8f, 140 / z));
        WorldLine(23, 8.2f, z, 27, 8.2f, z, C("#685247"), Math.Max(0.8f, 100 / z));
        if (z + 50 > DesertHighwayLayout.Far) return;
        foreach (var x in new[] { 23.5f, 26.5f })
        {
            using var wire = new SKPath();
            var a = Project(x, 8.3f, z); var b = Project(x, 8.3f, z + 50); var sag = Project(x, 7.1f, z + 25);
            wire.MoveTo(a); wire.QuadTo(sag, b); _stroke.Color = new SKColor(65, 51, 51, 150); _stroke.StrokeWidth = Math.Max(0.7f, 30 / z); _canvas.DrawPath(wire, _stroke);
        }
    }

    private void DesertLabel(string text, float x, float height, float z, float size, SKColor color)
    {
        var point = Project(x, height, z);
        _paint.Typeface = _typeface; _paint.TextAlign = SKTextAlign.Center; _paint.TextSize = size * 720 / z; _paint.Color = color;
        _canvas.DrawText(text, point.X, point.Y, _paint);
    }

    private void DesertTownBuilding(DesertProp p, float mid)
    {
        var x = p.X; var z = p.Distance; var w = p.Width; var h = p.Height;
        var side = Math.Sign(x);
        // A concrete forecourt connects each roadside business to the shoulder.
        Polygon(C("#ba9876"), Project(side * 8.3f, 0.02f, Math.Max(DesertHighwayLayout.Near, z - 4)), Project(x + side * w / 2, 0.02f, Math.Max(DesertHighwayLayout.Near, z - 4)),
            Project(x + side * w / 2, 0.02f, z + 9), Project(side * 8.3f, 0.02f, z + 9));
        Polygon(p.Seed % 2 == 0 ? C("#d0aa86") : C("#b99079"), Project(x - w / 2, 0, z), Project(x + w / 2, 0, z),
            Project(x + w / 2, h, z), Project(x - w / 2, h, z));
        Polygon(C("#8b6d61"), Project(x + side * w / 2, 0, z), Project(x + side * w / 2, 0, z + 8),
            Project(x + side * w / 2, h, z + 8), Project(x + side * w / 2, h, z));
        WorldLine(x - w / 2, h, z, x + w / 2, h, z, C("#ecbc88"), Math.Max(1, 125 / z));
        for (var i = 0; i < 4; i++)
        {
            var wx = x - w * 0.39f + i * w * 0.22f;
            Polygon(new SKColor(255, 207, 131, (byte)(100 + mid * 130)), Project(wx, h * 0.24f, z - 0.02f), Project(wx + w * 0.13f, h * 0.24f, z - 0.02f),
                Project(wx + w * 0.13f, h * 0.65f, z - 0.02f), Project(wx, h * 0.65f, z - 0.02f));
        }
        if (p.Kind is DesertPropKind.Motel or DesertPropKind.Diner)
        {
            var name = p.Kind == DesertPropKind.Motel ? "MOTEL" : "DINER";
            var a = Project(x - w * 0.38f, h * 0.82f, z - 0.03f); var b = Project(x + w * 0.38f, h * 1.18f, z - 0.03f);
            Rect(a.X, b.Y, b.X - a.X, a.Y - b.Y, C("#504447"));
            DesertLabel(name, x, h * 0.91f, z - 0.04f, h * 0.24f, new SKColor(255, 171, 148, (byte)(145 + mid * 100)));
        }
        if (p.Kind != DesertPropKind.GasStation) return;
        var inner = x - side * (w / 2 + 1); var outer = inner - side * 7;
        var front = Math.Max(DesertHighwayLayout.Near, z - 3);
        var back = z + 5;
        Polygon(C("#d2b18c"), Project(inner, 3.2f, front), Project(outer, 3.2f, front), Project(outer, 3.2f, back), Project(inner, 3.2f, back));
        WorldLine(inner, 3.2f, front, outer, 3.2f, front, C("#b66450"), Math.Max(1, 180 / z));
        foreach (var pumpX in new[] { inner - side, outer + side })
        {
            WorldLine(pumpX, 0, front + 1, pumpX, 3.2f, front + 1, C("#d7c5a1"), Math.Max(1, 65 / z));
            var a = Project(pumpX - 0.3f, 0, front + 1); var b = Project(pumpX + 0.3f, 1.3f, front + 1);
            Rect(a.X, b.Y, b.X - a.X, a.Y - b.Y, C("#be6f57"));
        }
        var signX = side * 11;
        WorldLine(signX, 0, z + 7, signX, 9, z + 7, C("#785d50"), Math.Max(1, 90 / z));
        var signA = Project(signX - 2.2f, 7.2f, z + 7); var signB = Project(signX + 2.2f, 9.7f, z + 7);
        Rect(signA.X, signB.Y, signB.X - signA.X, signA.Y - signB.Y, C("#8b4f4c"));
        DesertLabel("GAS", signX, 7.9f, z + 6.99f, 1.15f, C("#ffe1a7"));
    }

    private void DesertWaterTower(DesertProp p, float mid)
    {
        var z = p.Distance; var x = p.X; var h = p.Height;
        foreach (var side in new[] { -1, 1 })
        {
            WorldLine(x + side * 2.7f, 0, z, x + side * 2, h - 4, z, C("#766e65"), Math.Max(1, 90 / z));
            WorldLine(x + side * 2.5f, 3, z, x - side * 2.2f, 10, z, C("#766e65"), Math.Max(0.8f, 45 / z));
        }
        var a = Project(x - 3.5f, h - 5, z); var b = Project(x + 3.5f, h - 1, z);
        Rect(a.X, b.Y, b.X - a.X, a.Y - b.Y, C("#a29a86"));
        Polygon(C("#c5b59a"), Project(x - 4, h - 1, z), Project(x + 4, h - 1, z), Project(x, h + 1, z));
        DesertLabel("WATER", x, h - 3.7f, z - 0.01f, 0.9f, C("#56565c"));
        var light = Project(x, h + 1.2f, z);
        _paint.Color = new SKColor(250, 163, 135, (byte)(130 + mid * 110)); _canvas.DrawCircle(light, Math.Max(0.7f, 20 / z), _paint);
    }

    private void DesertTownSign(DesertProp p)
    {
        var x = p.X; var z = p.Distance;
        WorldLine(x, 0, z, x, 3.8f, z, C("#8e8773"), Math.Max(1, 75 / z));
        var a = Project(x - 3.4f, 2.7f, z); var b = Project(x + 3.4f, 4.4f, z);
        Rect(a.X, b.Y, b.X - a.X, a.Y - b.Y, C("#476c63"));
        DesertLabel(p.Seed % 2 == 0 ? "DRY CREEK" : "MESA RIDGE", x, 3.32f, z - 0.02f, 0.66f, C("#f4e2bd"));
    }
}
