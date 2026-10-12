using System.Diagnostics;
using Visualizer.Core;
using Xunit;

namespace Visualizer.Core.Tests;

public class FfmpegEncoderTests
{
    [Fact]
    public void EncodeImageSequence_ResolvesFfmpegOnPathAndPreservesQuotedPaths()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"visualizer-{Guid.NewGuid():N}", "frames with spaces");
        Directory.CreateDirectory(directory);
        try
        {
            // Quotes are valid filename characters on Linux; spaces exercise quoting on Windows.
            var filename = OperatingSystem.IsWindows() ? "input sample.wav" : "input \"sample\".wav";
            var wavPath = Path.Combine(directory, filename);
            var outputPath = Path.ChangeExtension(wavPath, ".mp4");
            var audio = new ProcessStartInfo("ffmpeg") { UseShellExecute = false };
            foreach (var arg in new[] { "-v", "error", "-f", "lavfi", "-i", "sine=frequency=440:duration=1", "-c:a", "pcm_s16le", wavPath })
                audio.ArgumentList.Add(arg);
            using (var process = Process.Start(audio)!)
            {
                process.WaitForExit();
                Assert.Equal(0, process.ExitCode);
            }

            using var renderer = new SkiaFrameRenderer(32, 32);
            for (var frame = 0; frame < 10; frame++)
                renderer.RenderFrame(0.5, frame, Path.Combine(directory, $"frame_{frame:D6}.png"));

            FfmpegEncoder.EncodeImageSequence("ffmpeg", directory, "frame_%06d.png", 0, 10, wavPath, outputPath);
            Assert.True(new FileInfo(outputPath).Length > 0);
            var decode = new ProcessStartInfo("ffmpeg") { UseShellExecute = false };
            foreach (var arg in new[] { "-v", "error", "-i", outputPath, "-f", "null", "-" })
                decode.ArgumentList.Add(arg);
            using var decoder = Process.Start(decode)!;
            decoder.WaitForExit();
            Assert.Equal(0, decoder.ExitCode);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(directory)!, recursive: true);
        }
    }
}
