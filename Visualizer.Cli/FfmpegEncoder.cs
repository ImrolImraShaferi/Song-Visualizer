using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Globalization;

public sealed class FfmpegEncoder
{
    public static void EncodeImageSequence(
        string ffmpegPath,
        string framesDirectory,
        string framePattern,   // e.g. "frame_%06d.png"
        int startNumber,        // 0 or 1
        int fps,
        string inputWavPath,
        string outputMp4Path)
    {
        if (fps <= 0) throw new ArgumentOutOfRangeException(nameof(fps));
        if (startNumber < 0) throw new ArgumentOutOfRangeException(nameof(startNumber));
        if (string.IsNullOrWhiteSpace(ffmpegPath))
            throw new FileNotFoundException("FFmpeg executable name is required.", ffmpegPath);

        if (!Directory.Exists(framesDirectory))
            throw new DirectoryNotFoundException(framesDirectory);

        if (!File.Exists(inputWavPath))
            throw new FileNotFoundException("Input WAV not found.", inputWavPath);

        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        foreach (var argument in new[] { "-y", "-framerate", fps.ToString(CultureInfo.InvariantCulture),
            "-start_number", startNumber.ToString(CultureInfo.InvariantCulture), "-i",
            Path.Combine(framesDirectory, framePattern), "-i", inputWavPath, "-c:v", "libx264",
            "-pix_fmt", "yuv420p", "-shortest", outputMp4Path })
            psi.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = psi };

        var stderr = new StringBuilder();
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                stderr.AppendLine(e.Data);
        };

        process.Start();
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"FFmpeg failed with exit code {process.ExitCode}\n{stderr}");
        }
    }
}
