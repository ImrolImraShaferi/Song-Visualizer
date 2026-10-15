using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Visualizer.Core;

var parsed = SceneArguments.Parse(args);
args = parsed.Positional;
var scene = parsed.Scene;

if (args.Length < 1)
{
    Console.WriteLine("Usage: Visualizer.Cli <input.wav> [mode] [fps] [width] [height] [frames] [output]");
    Console.WriteLine("  mode: pipe|png (default: pipe)");
    Console.WriteLine("  output: for png -> output directory (default: frames); for pipe -> output mp4 path (default: <input>.mp4)");
    Console.WriteLine("  --scene bars|night-drive|skyline --title <song> --artist <artist>");
    Console.WriteLine("  --no-rain --bass-strength <0..3> --high-strength <0..3>");
    return;
}

var inputPath = Path.GetFullPath(args[0]);
var mode = args.Length > 1 ? args[1].Trim().ToLowerInvariant() : "pipe";
var fps = args.Length > 2 ? double.Parse(args[2], CultureInfo.InvariantCulture) : 60.0;
var width = args.Length > 3 ? int.Parse(args[3], CultureInfo.InvariantCulture) : 1920;
var height = args.Length > 4 ? int.Parse(args[4], CultureInfo.InvariantCulture) : 1080;
var frames = args.Length > 5 ? int.Parse(args[5], CultureInfo.InvariantCulture) : 0;

// 7th argument is "output": directory for png, mp4 path for pipe
var outputArg = args.Length > 6 ? args[6] : null;

if (mode != "pipe" && mode != "png")
    throw new ArgumentException("mode must be 'pipe' or 'png'.");

if (!double.IsFinite(fps) || fps <= 0) throw new ArgumentOutOfRangeException(nameof(fps));
if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException("dimensions");
if (frames < 0) throw new ArgumentOutOfRangeException(nameof(frames));
if (mode == "pipe" && (width % 2 != 0 || height % 2 != 0))
    throw new ArgumentException("MP4 width and height must be even for yuv420p.");

var wav = WavAudio.Load(inputPath);
var windowSize = Math.Max(1, wav.SampleRate / 50); // ~20ms
var hopSize = Math.Max(1, windowSize / 2);
var envelope = scene.Scene == SceneKind.Bars
    ? RmsEnvelope.FromInterleaved(wav.Samples, wav.SampleRate, wav.Channels, windowSize, hopSize) : null;
if (scene.Scene != SceneKind.Bars) Console.WriteLine("Analyzing bass, mid, and high frequency bands...");
var spectrum = scene.Scene == SceneKind.Bars ? null : SpectralEnvelope.FromInterleaved(wav.Samples, wav.SampleRate, wav.Channels);

var timeline = new FrameTimeline(wav.SampleRate, fps);
var totalFrames = frames == 0 ? checked((long)Math.Ceiling(wav.DurationSeconds * fps)) : frames;

using var renderer = new SceneFrameRenderer(width, height, scene);

double Smooth(double prev, double current, double alpha)
    => prev + alpha * (current - prev);


var defaultMp4 =
    Path.Combine(
        Path.GetDirectoryName(inputPath)!,
        Path.GetFileNameWithoutExtension(inputPath) + ".mp4"
    );

var outputMp4 = mode == "pipe"
    ? (outputArg ?? defaultMp4)
    : defaultMp4; // not used in png mode

var outputDir = mode == "png"
    ? (outputArg ?? "frames")
    : "frames";   // not used in pipe mode
static (Process Process, Task<string> StandardError) StartFfmpegPipe(
    string ffmpegPath,
    int width,
    int height,
    double fps,
    long totalFrames,
    string wavPath,
    string outputMp4)
{
    var psi = new ProcessStartInfo
    {
        FileName = ffmpegPath,
        UseShellExecute = false,
        RedirectStandardInput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };

    foreach (var argument in new[] { "-y", "-f", "rawvideo", "-pix_fmt", "rgba", "-s",
        $"{width}x{height}", "-r", fps.ToString("R", CultureInfo.InvariantCulture), "-i", "pipe:0",
        "-i", wavPath, "-c:v", "libx264", "-pix_fmt", "yuv420p",
        "-t", (totalFrames / fps).ToString("R", CultureInfo.InvariantCulture), outputMp4 })
        psi.ArgumentList.Add(argument);

    var p = Process.Start(psi)!;

    return (p, p.StandardError.ReadToEndAsync());
}

double smoothed = 0;
AudioFeatures Features(long frame)
{
    if (spectrum is not null) return spectrum.GetValueAtTime(frame / fps);
    smoothed = Smooth(smoothed, envelope!.GetValueAtSample(timeline.GetSampleIndexForFrame(frame)), 0.2);
    return new AudioFeatures(smoothed, 0, 0, 0);
}

if (mode == "pipe")
{
    var (process, standardError) = StartFfmpegPipe(
        ffmpegPath: "ffmpeg",
        width: width,
        height: height,
        fps: fps,
        totalFrames: totalFrames,
        wavPath: inputPath,
        outputMp4: outputMp4
    );

    using var ffmpeg = process;
    try
    {
        using var stdin = ffmpeg.StandardInput.BaseStream;
        for (long frame = 0; frame < totalFrames; frame++)
        {
            stdin.Write(renderer.RenderToRgba(Features(frame), frame / fps));
        }

        stdin.Close();
        ffmpeg.WaitForExit();
        var stderr = await standardError;
        if (ffmpeg.ExitCode != 0)
            throw new InvalidOperationException($"FFmpeg failed with exit code {ffmpeg.ExitCode}\n{stderr}");
    }
    finally
    {
        if (!ffmpeg.HasExited) ffmpeg.Kill(entireProcessTree: true);
        ffmpeg.WaitForExit();
        await standardError;
    }

    Console.WriteLine($"Video rendered to '{Path.GetFullPath(outputMp4)}'.");
}
else // png
{
    for (long frame = 0; frame < totalFrames; frame++)
    {
        var outputPath = Path.Combine(outputDir, $"frame_{frame:D6}.png");
        renderer.RenderFrame(Features(frame), frame / fps, outputPath);
    }

    Console.WriteLine($"Rendered {totalFrames} frames to '{Path.GetFullPath(outputDir)}'.");
}
