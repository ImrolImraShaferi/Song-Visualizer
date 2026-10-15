using System.Globalization;
using Visualizer.Core;

public static class SceneArguments
{
    public static (string[] Positional, SceneOptions Scene) Parse(string[] args)
    {
        var positional = new List<string>();
        var scene = new SceneOptions(SceneKind.Bars);
        for (var i = 0; i < args.Length; i++)
        {
            var flag = args[i];
            string Value()
            {
                if (i + 1 >= args.Length || args[i + 1].StartsWith("--"))
                    throw new ArgumentException($"Missing value for {flag}.");
                return args[++i];
            }
            scene = flag switch
            {
                "--scene" => scene with { Scene = Value() switch
                {
                    "night-drive" => SceneKind.NightDrive,
                    "skyline" => SceneKind.Skyline,
                    "desert-highway" => SceneKind.DesertHighway,
                    "bars" => SceneKind.Bars,
                    _ => throw new ArgumentException("Scene must be night-drive, skyline, desert-highway, or bars.")
                } },
                "--title" => scene with { Title = Value() },
                "--artist" => scene with { Artist = Value() },
                "--no-rain" => scene with { Rain = false },
                "--bass-strength" => scene with { BassStrength = double.Parse(Value(), CultureInfo.InvariantCulture) },
                "--high-strength" => scene with { HighStrength = double.Parse(Value(), CultureInfo.InvariantCulture) },
                _ when flag.StartsWith("--") => throw new ArgumentException($"Unknown option: {flag}"),
                _ => scene
            };
            if (!flag.StartsWith("--")) positional.Add(flag);
        }
        if (positional.Count > 7) throw new ArgumentException("Too many positional arguments.");
        scene.Validate();
        return (positional.ToArray(), scene);
    }
}
