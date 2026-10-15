using SkiaSharp;

namespace Visualizer.Core;

public sealed partial class SceneFrameRenderer
{
    // A shared camera keeps buildings, road markings, ramps and bridges in the same perspective.
    private static SKPoint Project(float x, float height, float distance) =>
        new(960 + x * 720 / distance, NightDriveLayout.Horizon + (6 - height) * 720 / distance);

    private void WorldLine(float x, float height, float z, float x2, float height2, float z2, SKColor color, float width = 1)
    {
        var a = Project(x, height, z); var b = Project(x2, height2, z2);
        Line(a.X, a.Y, b.X, b.Y, color, width);
    }

    private void Road(double seconds, float bass, float mid)
    {
        Gradient(new(0, 625, 1920, 1080), C("#141e31"), C("#070f20"));
        var far = NightDriveLayout.Far; var near = NightDriveLayout.Near;
        Polygon(C("#243344"), Project(-11, 0, far), Project(11, 0, far), Project(11, 0, near), Project(-11, 0, near));
        Polygon(C("#0b1524"), Project(-8.7f, 0, far), Project(8.7f, 0, far), Project(8.7f, 0, near), Project(-8.7f, 0, near));
        foreach (var side in new[] { -1, 1 })
            WorldLine(side * 8.7f, 0.03f, far, side * 8.7f, 0.03f, near,
                side < 0 ? new SKColor(70, 190, 198, 125) : new SKColor(205, 103, 163, 100), 2.5f);

        var landmarks = NightDriveLayout.LandmarksAt(seconds).ToArray();
        foreach (var exit in landmarks.Where(x => x.Kind == RoadLandmarkKind.Exit)) ExitRamp(exit, mid);
        // Paint lane markings on the ground plane; each dash keeps its physical length while approaching.
        var travel = (float)((seconds * NightDriveLayout.Speed) % far);
        for (var i = 0; i < 70; i++)
        {
            var z = (i * 11 - travel + far) % far;
            if (z < near || z + 4 > far) continue;
            foreach (var side in new[] { -1, 1 })
                Polygon(new SKColor(171, 201, 207, 145),
                    Project(side * 2.9f - 0.065f, 0.04f, z), Project(side * 2.9f + 0.065f, 0.04f, z),
                    Project(side * 2.9f + 0.065f, 0.04f, z + 4), Project(side * 2.9f - 0.065f, 0.04f, z + 4));
        }
        for (var i = 0; i < 65; i++)
        {
            var z = (NightDriveLayout.Noise(i + 345) * far - travel + far) % far;
            if (z < near) continue;
            var x = (NightDriveLayout.Noise(i + 72) - 0.5f) * 16;
            WorldLine(x, 0.05f, z, x + 0.03f, 0.05f, z + 2,
                i % 2 == 0 ? new SKColor(74, 192, 204, (byte)(15 + mid * 28)) : new SKColor(224, 112, 170, (byte)(15 + mid * 28)), Math.Max(1, 45 / z));
        }

        var buildings = NightDriveLayout.BuildingsAt(seconds).ToArray();
        // Interleave all vertical objects by depth so nearby facades occlude distant bridge decks and signs.
        var objects = buildings.Select((b, i) => (Distance: b.Distance, Kind: 0, Index: i)).ToList();
        var bridges = landmarks.Where(x => x.Kind == RoadLandmarkKind.Flyover).ToArray();
        objects.AddRange(bridges.Select((b, i) => (b.Distance, 1, i)));
        var exits = landmarks.Where(x => x.Kind == RoadLandmarkKind.Exit && x.Distance > near).ToArray();
        objects.AddRange(exits.Select((b, i) => (b.Distance, 2, i)));
        var lamps = Enumerable.Range(0, 24).Select(i => (i * (far / 24) - travel + far) % far).Where(z => z >= near).ToArray();
        objects.AddRange(lamps.Select((z, i) => (z, 3, i)));
        foreach (var item in objects.OrderByDescending(x => x.Distance))
        {
            switch (item.Kind)
            {
                case 0: Building(buildings[item.Index], bass, mid); break;
                case 1: Flyover(bridges[item.Index]); break;
                case 2: ExitSign(exits[item.Index]); break;
                case 3: StreetLamps(lamps[item.Index]); break;
            }
        }
    }

    private void Building(CityBuilding building, float bass, float mid)
    {
        var b = building;
        var x = b.Side * b.Setback;
        var outer = x + b.Side * b.Width;
        var z = b.Distance;
        var height = b.Height * (1 + bass * 0.035f);
        var baseA = Project(x, 0, z); var baseB = Project(outer, 0, z);
        var topA = Project(x, height, z); var topB = Project(outer, height, z);
        var left = Math.Min(topA.X, topB.X); var width = Math.Abs(topB.X - topA.X);
        if (left > 1980 || left + width < -60 || baseA.Y < 0) return;
        var depthColor = b.Row == 2 ? C("#26334c") : b.Row == 1 ? C("#1b2941") : C("#101b2c");
        Polygon(depthColor, baseA, baseB, topB, topA);
        // Inner-facing wall exposes the block's depth, rather than painting flat billboards.
        var back = Math.Max(NightDriveLayout.Near, z - Math.Min(b.Width * 0.65f, z * 0.18f));
        Polygon(b.Row == 0 ? C("#1a2940") : C("#2b3850"), baseA, Project(x, 0, back), Project(x, height, back), topA);
        Line(topA.X, topA.Y, baseA.X, baseA.Y, new SKColor(83, 149, 167, 95), 1);
        Line(topA.X, topA.Y, topB.X, topB.Y, new SKColor(139, 157, 181, 110), 1);

        // Low broad blocks, narrow towers, stepped crowns, and antennas vary independently of depth.
        if (b.RoofStyle == 1)
        {
            var a = x + b.Side * b.Width * 0.22f; var c = x + b.Side * b.Width * 0.78f;
            Polygon(depthColor, Project(a, height, z), Project(c, height, z), Project(c, height + 3, z), Project(a, height + 3, z));
        }
        else if (b.RoofStyle == 2 && b.Height > 40)
        {
            var tip = Project((x + outer) / 2, height + 6, z);
            var roof = Project((x + outer) / 2, height, z);
            Line(roof.X, roof.Y, tip.X, tip.Y, C("#6f8198"), Math.Max(0.7f, 18 / z));
            _paint.Color = new SKColor(239, 128, 140, 170); _canvas.DrawCircle(tip, Math.Max(0.8f, 12 / z), _paint);
        }
        var rows = Math.Clamp((int)(b.Height / 3.5f), 3, 23);
        var cols = Math.Clamp((int)(b.Width / 2.7f), 3, 8);
        for (var row = 0; row < rows; row++)
        for (var col = 0; col < cols; col++)
        {
            var seed = b.Seed * 97 + row * 13 + col * 7;
            if (NightDriveLayout.Noise(seed) < 0.3) continue;
            var windowX = x + b.Side * b.Width * (0.08f + col * 0.84f / cols);
            var windowH = height * (0.08f + row * 0.85f / rows);
            var color = NightDriveLayout.Noise(b.Seed + col) > 0.6f
                ? new SKColor(237, 176, 132, (byte)(70 + mid * 115))
                : new SKColor(110, 195, 213, (byte)(65 + mid * 105));
            var a = Project(windowX, windowH, z);
            var c = Project(windowX + b.Side * b.Width * 0.4f / cols, windowH + height * 0.34f / rows, z);
            var rect = new SKRect(Math.Min(a.X, c.X), c.Y, Math.Max(a.X, c.X), a.Y);
            if (rect.Bottom > 0 && rect.Top < 1080) { _paint.Color = color; _canvas.DrawRect(rect, _paint); }
        }
    }

    private void StreetLamps(float distance)
    {
        foreach (var side in new[] { -1, 1 })
        {
            var top = Project(side * 10.5f, 5.8f, distance);
            var bottom = Project(side * 10.5f, 0, distance);
            var scale = 720 / distance;
            Line(top.X, top.Y, bottom.X, bottom.Y, C("#59616c"), Math.Max(1, scale * 0.1f));
            _paint.Color = new SKColor(255, 205, 149, 23); _canvas.DrawCircle(top, scale * 0.65f, _paint);
            _paint.Color = C("#ffe1b4"); _canvas.DrawCircle(top, Math.Max(1, scale * 0.1f), _paint);
            WorldLine(side * 10.5f, 0.02f, distance, side * 10.6f, 0.02f, distance + 3,
                new SKColor(241, 178, 120, 35), Math.Max(1, scale * 0.22f));
        }
    }

    private void ExitRamp(RoadLandmark exit, float mid)
    {
        // Ribbon segments are clipped at the camera before projection, including the frame when it is passed.
        for (var i = 0; i < 48; i++)
        {
            var u = i / 48f; var v = (i + 1) / 48f;
            var zA = exit.Distance + 65 - u * 160; var zB = exit.Distance + 65 - v * 160;
            if (zA < NightDriveLayout.Near || zB > NightDriveLayout.Far) continue;
            if (zA > NightDriveLayout.Far) { u = (exit.Distance + 65 - NightDriveLayout.Far) / 160; zA = NightDriveLayout.Far; }
            if (zB < NightDriveLayout.Near) { v = (exit.Distance + 65 - NightDriveLayout.Near) / 160; zB = NightDriveLayout.Near; }
            float X(float t, float offset) => exit.Side * (NightDriveLayout.ExitCenter(t) + offset);
            float Height(float t) => t * t * 2.4f;
            var a = Project(X(u, -2.5f), Height(u), zA); var b = Project(X(u, 2.5f), Height(u), zA);
            var c = Project(X(v, 2.5f), Height(v), zB); var d = Project(X(v, -2.5f), Height(v), zB);
            Polygon(C("#26394a"), a, b, c, d);
            Line(a.X, a.Y, d.X, d.Y, new SKColor(132, 202, 194, (byte)(100 + mid * 40)), 1.5f);
            Line(b.X, b.Y, c.X, c.Y, new SKColor(121, 152, 174, 160), 2);
            // Guardrail, lane dashes, and supports make the rising ramp read as a road.
            WorldLine(X(u, 2.8f), Height(u) + 0.65f, zA, X(v, 2.8f), Height(v) + 0.65f, zB, C("#617d8d"), 1.5f);
            if (i % 3 == 0)
            {
                WorldLine(X(u, 0), Height(u) + 0.04f, zA, X(v, 0), Height(v) + 0.04f, zB, C("#b1c6c5"), Math.Max(0.6f, 55 / zB));
                if (u > 0.4f) WorldLine(X(u, 0), 0, zA, X(u, 0), Height(u), zA, C("#233347"), Math.Max(1, 220 / zA));
            }
        }
    }

    private void ExitSign(RoadLandmark exit)
    {
        var z = exit.Distance;
        var x = exit.Side * 12.5f;
        var scale = 720 / z;
        var bottom = Project(x, 0, z); var top = Project(x, 5.2f, z);
        Line(bottom.X, bottom.Y, top.X, top.Y, C("#647a86"), Math.Max(1, scale * 0.12f));
        var sign = new SKRect(top.X - scale * 2.4f, top.Y - scale * 1.3f, top.X + scale * 2.4f, top.Y + scale * 0.2f);
        _paint.Color = C("#225f59"); _canvas.DrawRoundRect(sign, scale * 0.1f, scale * 0.1f, _paint);
        _stroke.Color = C("#a5d3c7"); _stroke.StrokeWidth = Math.Max(0.6f, scale * 0.035f); _canvas.DrawRect(sign, _stroke);
        _paint.Typeface = _typeface; _paint.TextAlign = SKTextAlign.Center; _paint.TextSize = scale * 0.61f; _paint.Color = C("#e0f2df");
        _canvas.DrawText("EXIT", top.X - exit.Side * scale * 0.5f, top.Y - scale * 0.28f, _paint);
        var arrowX = top.X + exit.Side * scale * 1.6f; var arrowY = top.Y - scale * 0.62f;
        Line(arrowX - exit.Side * scale * 0.6f, arrowY + scale * 0.4f, arrowX, arrowY, C("#e0f2df"), Math.Max(0.8f, scale * 0.08f));
        Line(arrowX, arrowY, arrowX - exit.Side * scale * 0.4f, arrowY, C("#e0f2df"), Math.Max(0.8f, scale * 0.08f));
        Line(arrowX, arrowY, arrowX, arrowY + scale * 0.4f, C("#e0f2df"), Math.Max(0.8f, scale * 0.08f));
    }

    private void Flyover(RoadLandmark bridge)
    {
        var front = Math.Max(NightDriveLayout.Near, bridge.Distance - 6);
        var back = bridge.Distance + 6;
        // Piers stand outside the driving lanes. The deck rises out of view as the camera passes beneath it.
        foreach (var side in new[] { -1, 1 })
        foreach (var offset in new[] { 17f, 52f })
        {
            var x = side * offset; var z = bridge.Distance + 2;
            Polygon(C("#28384b"), Project(x - 0.85f, 0, z), Project(x + 0.85f, 0, z), Project(x + 0.85f, 11, z), Project(x - 0.85f, 11, z));
            WorldLine(x + side * 0.85f, 0, z, x + side * 0.85f, 11, z, C("#52657b"), 1.5f);
        }
        Polygon(C("#1a2538"), Project(-100, 11, front), Project(100, 11, front), Project(100, 11, back), Project(-100, 11, back));
        Polygon(C("#26374d"), Project(-100, 11, front), Project(100, 11, front), Project(100, 12.2f, front), Project(-100, 12.2f, front));
        foreach (var z in new[] { front })
        {
            WorldLine(-100, 13, z, 100, 13, z, C("#8194a8"), Math.Max(1, 50 / z));
            for (var x = -95; x < 100; x += 5)
                WorldLine(x, 12.2f, z, x, 13, z, C("#61768d"), Math.Max(0.7f, 30 / z));
        }
        for (var x = -96; x < 100; x += 8)
        {
            // The camera is below the deck: show underside beams, rather than the road surface above.
            WorldLine(x, 10.95f, front, x, 10.95f, back, C("#40516a"), Math.Max(0.8f, 45 / bridge.Distance));
            var light = Project(x, 11.1f, front);
            _paint.Color = new SKColor(249, 205, 148, 170); _canvas.DrawCircle(light, Math.Max(0.6f, 35 / front), _paint);
        }
    }
}
