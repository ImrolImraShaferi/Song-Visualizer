"""Run after `dotnet build Visualizer.sln`; requires dotnet, ffmpeg and ffprobe."""
import json
import subprocess
import tempfile
from pathlib import Path


def run(*args, **kwargs):
    return subprocess.run(args, check=True, **kwargs)


exe = Path(__file__).resolve().parents[1] / "Visualizer.Cli/bin/Debug/net8.0/Visualizer.Cli.dll"
with tempfile.TemporaryDirectory(prefix="visualizer-regression-") as directory:
    root = Path(directory)
    wav = root / 'audio "quoted" sample.wav'
    mp4 = root / 'movie "quoted" sample.mp4'
    run("ffmpeg", "-v", "error", "-f", "lavfi", "-i",
        "sine=frequency=440:duration=1", "-c:a", "pcm_s16le", str(wav))

    # Relative input and default output must work in the current directory.
    run("dotnet", str(exe), wav.name, "pipe", "10", "320", "180", cwd=root)
    assert wav.with_suffix(".mp4").is_file()

    run("dotnet", str(exe), str(wav), "pipe", "29.97", "320", "180", "0", str(mp4),
        "--scene", "night-drive", "--title", "Phases", "--artist", "Imra")
    info = json.loads(subprocess.check_output([
        "ffprobe", "-v", "error", "-count_frames", "-show_streams", "-of", "json", str(mp4)
    ]))
    video = next(stream for stream in info["streams"] if stream["codec_type"] == "video")
    assert video["r_frame_rate"] == "2997/100", video
    assert video["nb_read_frames"] == "30", video
    assert any(stream["codec_type"] == "audio" for stream in info["streams"])
    run("ffmpeg", "-v", "error", "-i", str(mp4), "-f", "null", "-")

    # Explicit frame limits must truncate longer audio too.
    run("dotnet", str(exe), str(wav), "pipe", "10", "320", "180", "2", str(mp4))
    duration = float(subprocess.check_output([
        "ffprobe", "-v", "error", "-show_entries", "format=duration",
        "-of", "default=noprint_wrappers=1:nokey=1", str(mp4)
    ]))
    assert 0.19 <= duration <= 0.25, duration

    run("dotnet", str(exe), str(wav), "png", "29.97", "320", "180", "0", str(root / "frames"))
    assert len(list((root / "frames").glob("*.png"))) == 30

    run("dotnet", str(exe), str(wav), "png", "30", "320", "180", "2", str(root / "skyline"),
        "--scene", "skyline", "--no-rain", "--title", "Phases", "--artist", "Imra")
    assert len(list((root / "skyline").glob("*.png"))) == 2

    for values in [("NaN", "320", "180", "0"), ("10", "321", "180", "0"),
                   ("10", "320", "180", "-1")]:
        result = subprocess.run(["dotnet", str(exe), str(wav), "pipe", *values,
                                 str(root / "bad.mp4")], capture_output=True)
        assert result.returncode != 0
        assert not (root / "bad.mp4").exists()

print("Passed CLI smoke checks: quoted paths, fractional FPS, complete frames, frame limits, PNGs, invalid arguments")
