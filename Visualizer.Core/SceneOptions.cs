namespace Visualizer.Core;

public enum SceneKind { Bars, NightDrive, Skyline }

public sealed record SceneOptions(SceneKind Scene = SceneKind.NightDrive, string Title = "", string Artist = "",
    bool Rain = true, double BassStrength = 1, double HighStrength = 1)
{
    public void Validate()
    {
        if (!Enum.IsDefined(Scene)) throw new ArgumentOutOfRangeException(nameof(Scene));
        if (Title is null || Artist is null) throw new ArgumentNullException(nameof(Title));
        if (!double.IsFinite(BassStrength) || BassStrength < 0 || BassStrength > 3) throw new ArgumentOutOfRangeException(nameof(BassStrength));
        if (!double.IsFinite(HighStrength) || HighStrength < 0 || HighStrength > 3) throw new ArgumentOutOfRangeException(nameof(HighStrength));
    }
}
