using System;
using System.Globalization;
using System.IO;
using Visualizer.Core;

if (args.Length < 1)
{
    Console.WriteLine("Usage: Visualizer.Cli <input.wav> [outputDir] [fps] [width] [height]");
    return;
}

var inputPath = args[0];
var outputDir = args.Length > 1 ? args[1] : "frames";
var fps = args.Length > 2 ? double.Parse(args[2], CultureInfo.InvariantCulture) : 60.0;
var width = args.Length > 3 ? int.Parse(args[3], CultureInfo.InvariantCulture) : 1920;
var height = args.Length > 4 ? int.Parse(args[4], CultureInfo.InvariantCulture) : 1080;

var wav = WavAudio.Load(inputPath);
var windowSize = Math.Max(1, wav.SampleRate / 50); // ~20ms
var hopSize = Math.Max(1, windowSize / 2);
var envelope = RmsEnvelope.FromInterleaved(wav.Samples, wav.SampleRate, wav.Channels, windowSize, hopSize);

var timeline = new FrameTimeline(wav.SampleRate, fps);
var totalFrames = (long)Math.Ceiling(wav.DurationSeconds * fps);

using var renderer = new SkiaFrameRenderer(width, height);

for (long frame = 0; frame < totalFrames; frame++)
{
    var sampleIndex = timeline.GetSampleIndexForFrame(frame);
    var rms = envelope.GetValueAtSample(sampleIndex);
    var outputPath = Path.Combine(outputDir, $"frame_{frame:D6}.png");
    renderer.RenderFrame(rms, frame, outputPath);
}

Console.WriteLine($"Rendered {totalFrames} frames to '{Path.GetFullPath(outputDir)}'.");
