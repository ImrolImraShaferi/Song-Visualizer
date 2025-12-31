using System;
using System.Globalization;
using System.IO;
using Visualizer.Core;

if (args.Length < 1)
{
    Console.WriteLine("Usage: Visualizer.Cli <input.wav> [outputDir] [fps] [width] [height] [frames]");
    return;
}

var inputPath = args[0];
var outputDir = args.Length > 1 ? args[1] : "frames";
var fps = args.Length > 2 ? double.Parse(args[2], CultureInfo.InvariantCulture) : 60.0;
var width = args.Length > 3 ? int.Parse(args[3], CultureInfo.InvariantCulture) : 1920;
var height = args.Length > 4 ? int.Parse(args[4], CultureInfo.InvariantCulture) : 1080;
var frames = args.Length > 5 ? int.Parse(args[5], CultureInfo.InvariantCulture) : 0;

var wav = WavAudio.Load(inputPath);
var windowSize = Math.Max(1, wav.SampleRate / 50); // ~20ms
var hopSize = Math.Max(1, windowSize / 2);
var envelope = RmsEnvelope.FromInterleaved(wav.Samples, wav.SampleRate, wav.Channels, windowSize, hopSize);

var timeline = new FrameTimeline(wav.SampleRate, fps);
var totalFrames = frames == 0 ? (long)Math.Ceiling(wav.DurationSeconds * fps) : frames;

using var renderer = new SkiaFrameRenderer(width, height);

double Smooth(double prev, double current, double alpha)
    => prev + alpha * (current - prev);

double smoothed = 0;
const double alpha = 0.2; // try 0.15–0.30



for (long frame = 0; frame < totalFrames; frame++)
{
    var sampleIndex = timeline.GetSampleIndexForFrame(frame);
    var rms = envelope.GetValueAtSample(sampleIndex);

    smoothed = Smooth(smoothed, rms, alpha);

    var outputPath = Path.Combine(outputDir, $"frame_{frame:D6}.png");
    renderer.RenderFrame(smoothed, frame, outputPath);
}

Console.WriteLine($"Rendered {totalFrames} frames to '{Path.GetFullPath(outputDir)}'.");

var ffmpegPath = @"C:\ffmpeg\bin\ffmpeg.exe";
var framesDir = outputDir;//Path.Combine(Environment.CurrentDirectory, "frames");
var wavPath = inputPath;// @"C:\path\to\input.wav";
var outputMp4 = 
    Path.Combine(
        Path.GetDirectoryName(inputPath)!,
        Path.GetFileNameWithoutExtension(inputPath) + ".mp4"
    );


FfmpegEncoder.EncodeImageSequence(
    ffmpegPath: ffmpegPath,
    framesDirectory: framesDir,
    framePattern: "frame_%06d.png",
    startNumber: 0,
    fps: (int)fps,
    inputWavPath: wavPath,
    outputMp4Path: outputMp4
);

Console.WriteLine($"Video rendered to '{Path.GetFullPath(outputMp4)}'.");