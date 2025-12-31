using System;
using System.IO;
using SkiaSharp;

namespace Visualizer.Core;

public sealed class SkiaFrameRenderer : IFrameRenderer
{
    private readonly SKPaint _backgroundPaint = new() { Color = SKColors.Black };
    private readonly SKPaint _barPaint = new() { Color = SKColors.LimeGreen, IsAntialias = true };
    private bool _disposed;

    public SkiaFrameRenderer(int width, int height)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");

        Width = width;
        Height = height;
    }

    public int Width { get; }

    public int Height { get; }

    public void RenderFrame(double rms, long frameIndex, string outputPath)
    {
        ThrowIfDisposed();
        if (outputPath is null) throw new ArgumentNullException(nameof(outputPath));

        var clamped = Math.Clamp(rms, 0, 1);
        clamped = Math.Pow(clamped, 0.5);
        var barHeight = (float)(clamped * Height);
        var barWidth = Math.Max(Width / 20f, 8f); // thin vertical bar

        using var bitmap = new SKBitmap(Width, Height);
        using var canvas = new SKCanvas(bitmap);

        canvas.DrawRect(SKRect.Create(0, 0, Width, Height), _backgroundPaint);

        var left = (Width - barWidth) / 2f;
        var right = left + barWidth;
        var top = Height - barHeight;
        var rect = new SKRect(left, top, right, Height);

        canvas.DrawRect(rect, _barPaint);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, quality: 100);
        using var stream = File.Open(outputPath, FileMode.Create, FileAccess.Write);
        data.SaveTo(stream);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(SkiaFrameRenderer));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _backgroundPaint.Dispose();
        _barPaint.Dispose();
        _disposed = true;
    }
}
