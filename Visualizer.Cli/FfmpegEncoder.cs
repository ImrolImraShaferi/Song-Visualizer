using System;
using System.Diagnostics;
using System.IO;
using System.Text;

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
        if (!File.Exists(ffmpegPath))
            throw new FileNotFoundException("FFmpeg executable not found.", ffmpegPath);

        if (!Directory.Exists(framesDirectory))
            throw new DirectoryNotFoundException(framesDirectory);

        if (!File.Exists(inputWavPath))
            throw new FileNotFoundException("Input WAV not found.", inputWavPath);

        var args = new StringBuilder();
        args.Append($"-y ");
        args.Append($"-framerate {fps} ");
        args.Append($"-start_number {startNumber} ");
        args.Append($"-i \"{Path.Combine(framesDirectory, framePattern)}\" ");
        args.Append($"-i \"{inputWavPath}\" ");
        args.Append($"-c:v libx264 ");
        args.Append($"-pix_fmt yuv420p ");
        args.Append($"-shortest ");
        args.Append($"\"{outputMp4Path}\"");

        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args.ToString(),
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

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
