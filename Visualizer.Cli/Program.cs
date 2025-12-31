using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Visualizer.Core;

if (args.Length < 1)
{
    Console.WriteLine("Usage: Visualizer.Cli <input.wav> [mode] [fps] [width] [height] [frames] [output]");
    Console.WriteLine("  mode: pipe|png (default: pipe)");
    Console.WriteLine("  output: for png -> output directory (default: frames); for pipe -> output mp4 path (default: <input>.mp4)");
    return;
}

var inputPath = args[0];
var mode = args.Length > 1 ? args[1].Trim().ToLowerInvariant() : "pipe";
var fps = args.Length > 2 ? double.Parse(args[2], CultureInfo.InvariantCulture) : 60.0;
var width = args.Length > 3 ? int.Parse(args[3], CultureInfo.InvariantCulture) : 1920;
var height = args.Length > 4 ? int.Parse(args[4], CultureInfo.InvariantCulture) : 1080;
var frames = args.Length > 5 ? int.Parse(args[5], CultureInfo.InvariantCulture) : 0;

// 7th argument is "output": directory for png, mp4 path for pipe
var outputArg = args.Length > 6 ? args[6] : null;

if (mode != "pipe" && mode != "png")
    throw new ArgumentException("mode must be 'pipe' or 'png'.");


var wav = WavAudio.Load(inputPath);
var windowSize = Math.Max(1, wav.SampleRate / 50); // ~20ms
var hopSize = Math.Max(1, windowSize / 2);
var envelope = RmsEnvelope.FromInterleaved(wav.Samples, wav.SampleRate, wav.Channels, windowSize, hopSize);

var timeline = new FrameTimeline(wav.SampleRate, fps);
var totalFrames = frames == 0 ? (long)Math.Ceiling(wav.DurationSeconds * fps) : frames;

using var renderer = new SkiaFrameRenderer(width, height);

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
static Process StartFfmpegPipe(
    string ffmpegPath,
    int width,
    int height,
    int fps,
    string wavPath,
    string outputMp4)
{
    var args =
        $"-y " +
        $"-f rawvideo -pix_fmt rgba -s {width}x{height} -r {fps} -i pipe:0 " +
        $"-i \"{wavPath}\" " +
        $"-c:v libx264 -pix_fmt yuv420p -shortest " +
        $"\"{outputMp4}\"";

    var psi = new ProcessStartInfo
    {
        FileName = ffmpegPath,
        Arguments = args,
        UseShellExecute = false,
        RedirectStandardInput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };

    var p = Process.Start(psi)!;

    // Optional: capture stderr for debugging
    _ = Task.Run(() =>
    {
        while (!p.StandardError.EndOfStream)
            Console.WriteLine(p.StandardError.ReadLine());
    });

    return p;
}

//using var ffmpeg = StartFfmpegPipe(
//    ffmpegPath: "ffmpeg",
//    width: width,
//    height: height,
//    fps: (int)fps,
//    wavPath: inputPath,
//    outputMp4: outputMp4
//);

//using var stdin = ffmpeg.StandardInput.BaseStream;

double smoothed = 0;

if (mode == "pipe")
{
    using var ffmpeg = StartFfmpegPipe(
        ffmpegPath: "ffmpeg",
        width: width,
        height: height,
        fps: (int)fps,
        wavPath: inputPath,
        outputMp4: outputMp4
    );

    using var stdin = ffmpeg.StandardInput.BaseStream;

    for (long frame = 0; frame < totalFrames; frame++)
    {
        var sampleIndex = timeline.GetSampleIndexForFrame(frame);
        var rms = envelope.GetValueAtSample(sampleIndex);

        smoothed = Smooth(smoothed, rms, 0.2);

        var pixels = renderer.RenderToRgba(smoothed);
        stdin.Write(pixels);
    }

    stdin.Flush();
    stdin.Close();

    ffmpeg.WaitForExit();
    if (ffmpeg.ExitCode != 0) throw new Exception("FFmpeg failed.");

    Console.WriteLine($"Video rendered to '{Path.GetFullPath(outputMp4)}'.");
}
else // png
{
    for (long frame = 0; frame < totalFrames; frame++)
    {
        var sampleIndex = timeline.GetSampleIndexForFrame(frame);
        var rms = envelope.GetValueAtSample(sampleIndex);

        smoothed = Smooth(smoothed, rms, 0.2);

        var outputPath = Path.Combine(outputDir, $"frame_{frame:D6}.png");
        renderer.RenderFrame(smoothed, frame, outputPath);
    }

    Console.WriteLine($"Rendered {totalFrames} frames to '{Path.GetFullPath(outputDir)}'.");
}


//double smoothed = 0;
//const double alpha = 0.2; // try 0.15–0.30
//for (long frame = 0; frame < totalFrames; frame++)
//{
//    var sampleIndex = timeline.GetSampleIndexForFrame(frame);
//    var rms = envelope.GetValueAtSample(sampleIndex);

//    smoothed = Smooth(smoothed, rms, alpha);

//    var outputPath = Path.Combine(outputDir, $"frame_{frame:D6}.png");
//    renderer.RenderFrame(smoothed, frame, outputPath);
//}

//Console.WriteLine($"Rendered {totalFrames} frames to '{Path.GetFullPath(outputDir)}'.");

//var ffmpegPath = @"C:\ffmpeg\bin\ffmpeg.exe";
//var framesDir = outputDir;//Path.Combine(Environment.CurrentDirectory, "frames");
//var wavPath = inputPath;// @"C:\path\to\input.wav";
//var outputMp4 = 
//    Path.Combine(
//        Path.GetDirectoryName(inputPath)!,
//        Path.GetFileNameWithoutExtension(inputPath) + ".mp4"
//    );


//FfmpegEncoder.EncodeImageSequence(
//    ffmpegPath: ffmpegPath,
//    framesDirectory: framesDir,
//    framePattern: "frame_%06d.png",
//    startNumber: 0,
//    fps: (int)fps,
//    inputWavPath: wavPath,
//    outputMp4Path: outputMp4
//);

//Console.WriteLine($"Video rendered to '{Path.GetFullPath(outputMp4)}'.");