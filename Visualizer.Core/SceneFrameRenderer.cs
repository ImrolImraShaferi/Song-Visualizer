using SkiaSharp;

namespace Visualizer.Core;

/// <summary>Deterministic vector scenery; time controls travel, audio controls scenery details.</summary>
public sealed partial class SceneFrameRenderer : IDisposable
{
    private readonly SKBitmap _bitmap;
    private readonly SKCanvas _canvas;
    private readonly SKPaint _paint = new() { IsAntialias = true };
    private readonly SKPaint _stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private readonly SKTypeface _typeface;
    private readonly SceneOptions _options;
    private bool _disposed;

    public SceneFrameRenderer(int width, int height, SceneOptions options)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _options = options;
        _bitmap = new(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        _canvas = new(_bitmap);
        // The no-dependencies Linux Skia build has no font discovery: load a real font when available.
        var font = new[] { "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/usr/share/fonts/truetype/liberation2/LiberationSans-Regular.ttf" }.FirstOrDefault(File.Exists);
        _typeface = font is null ? SKTypeface.FromFamilyName("sans-serif") : SKTypeface.FromFile(font);
    }

    public ReadOnlySpan<byte> RenderToRgba(AudioFeatures audio, double seconds)
    {
        Draw(audio, seconds);
        return _bitmap.GetPixelSpan();
    }

    public void RenderFrame(AudioFeatures audio, double seconds, string path)
    {
        Draw(audio, seconds);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var image = SKImage.FromBitmap(_bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }

    private static float Hash(int seed) => (float)(Math.Sin(seed * 127.1 + 311.7) * 43758.5453 % 1 + 1) % 1;
    private static SKColor C(string color) => SKColor.Parse(color);
    private void Rect(float x, float y, float w, float h, SKColor color)
    {
        _paint.Color = color; _canvas.DrawRect(x, y, w, h, _paint);
    }
    private void Line(float x, float y, float x2, float y2, SKColor color, float width = 1)
    {
        _stroke.Color = color; _stroke.StrokeWidth = width; _canvas.DrawLine(x, y, x2, y2, _stroke);
    }
    private void Polygon(SKColor color, params SKPoint[] points)
    {
        using var path = new SKPath(); path.AddPoly(points); _paint.Color = color; _canvas.DrawPath(path, _paint);
    }
    private void Gradient(SKRect rect, SKColor top, SKColor bottom)
    {
        using var shader = SKShader.CreateLinearGradient(new(rect.MidX, rect.Top), new(rect.MidX, rect.Bottom), new[] { top, bottom }, null, SKShaderTileMode.Clamp);
        _paint.Shader = shader; _paint.Color = SKColors.White; _canvas.DrawRect(rect, _paint); _paint.Shader = null;
    }

    private void Draw(AudioFeatures audio, double seconds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (!double.IsFinite(audio.Level) || !double.IsFinite(audio.Bass) || !double.IsFinite(audio.Mid) || !double.IsFinite(audio.High))
            throw new ArgumentOutOfRangeException(nameof(audio));
        var bass = (float)Math.Clamp(audio.Bass * _options.BassStrength, 0, 2);
        var mid = (float)Math.Clamp(audio.Mid, 0, 1);
        var high = (float)Math.Clamp(audio.High * _options.HighStrength, 0, 2);
        _canvas.Save();
        try
        {
            _canvas.Scale(_bitmap.Width / 1920f, _bitmap.Height / 1080f);
            if (_options.Scene == SceneKind.Bars)
            {
                _canvas.Clear(SKColors.Black);
                var height = (float)Math.Sqrt(Math.Clamp(audio.Level, 0, 1)) * 1080;
                var barWidth = Math.Max(96, 8 * 1920f / _bitmap.Width);
                Rect((1920 - barWidth) / 2, 1080 - height, barWidth, height, SKColors.LimeGreen);
            }
            else
            {
                Sky(seconds, high);
                DistantCity(bass, mid);
                if (_options.Scene == SceneKind.NightDrive) Road(seconds, bass, mid);
                else Waterfront(seconds, mid);
                if (_options.Rain) Rain(seconds, high);
                // A soft upper fade keeps the title readable without placing a panel over the sky.
                Gradient(new(0, 0, 1920, 240), new SKColor(3, 8, 20, 220), new SKColor(3, 8, 20, 0));
            }
            Metadata();
        }
        finally { _canvas.Restore(); }
    }

    private void Sky(double seconds, float high)
    {
        Gradient(new(0, 0, 1920, 1080), C("#080f26"), C("#473550"));
        for (var i = 0; i < 85; i++)
        {
            _paint.Color = new SKColor(163, 194, 225, (byte)(50 + Hash(i + 41) * 85));
            _canvas.DrawCircle(Hash(i) * 1920, 170 + Hash(i + 17) * 330, 0.6f + Hash(i + 9), _paint);
        }
        _paint.Color = new SKColor(154, 200, 224, 14); _canvas.DrawCircle(1365, 285, 108, _paint);
        _paint.Color = new SKColor(154, 200, 224, 20); _canvas.DrawCircle(1365, 285, 81, _paint);
        _paint.Color = C("#c5d9df"); _canvas.DrawCircle(1365, 285, 48, _paint);
        for (var i = 0; i < 5; i++)
        {
            var x = (float)((i * 490 + seconds * (3 + i) + high * 16) % 2500) - 300;
            var y = 230 + Hash(i + 93) * 170;
            using var cloud = new SKPath();
            cloud.MoveTo(x, y + 30); cloud.CubicTo(x - 40, y, x + 35, y - 50, x + 80, y - 20);
            cloud.CubicTo(x + 130, y - 105, x + 220, y - 65, x + 240, y - 15);
            cloud.CubicTo(x + 330, y - 55, x + 395, y + 20, x + 340, y + 40);
            cloud.CubicTo(x + 220, y + 65, x + 80, y + 50, x, y + 30); cloud.Close();
            _paint.Color = new SKColor(40, 50, 77, (byte)(165 + high * 25)); _canvas.DrawPath(cloud, _paint);
        }
    }

    private void DistantCity(float bass, float mid)
    {
        for (var layer = 0; layer < 2; layer++)
        for (var i = 0; i < 38; i++)
        {
            var x = i * 55 - 50 + layer * 27;
            var bottom = 650 + layer * 60;
            var height = (70 + Hash(i + layer * 71) * 185) * (1 + bass * 0.025f);
            var width = 32 + Hash(i + 33) * 30;
            var top = bottom - height;
            Rect(x, top, width, height, layer == 0 ? C("#202740") : C("#101c30"));
            if (i % 7 == 0) Line(x + width / 2, top, x + width / 2, top - 25, C("#394159"), 2);
            for (var row = 0; row < 10; row++)
            for (var col = 0; col < 3; col++)
                if (top + 15 + row * 17 < bottom && Hash(i * 43 + row * 7 + col) > 0.48)
                    Rect(x + 6 + col * 11, top + 10 + row * 17, 3, 5,
                        new SKColor(160, 190, 195, (byte)(45 + mid * 90)));
        }
    }

    private void Waterfront(double seconds, float mid)
    {
        Gradient(new(0, 707, 1920, 1080), C("#172a3f"), C("#060f23"));
        Line(0, 710, 1920, 710, C("#4d657a"), 2);
        for (var i = 0; i < 320; i++)
        {
            var y = 720 + Hash(i + 7) * 360;
            var x = Hash(i + 801) * 1920 + (float)Math.Sin(seconds * 0.6 + i) * 8;
            var length = 3 + (y - 710) * 0.12f;
            Line(x, y, x + length, y, i % 3 == 0 ? new SKColor(233, 170, 138, (byte)(12 + mid * 40)) : new SKColor(108, 184, 207, (byte)(10 + mid * 35)), 1.5f);
        }
    }

    private void Rain(double seconds, float high)
    {
        for (var i = 0; i < 155; i++)
        {
            var depth = 0.3f + Hash(i + 992) * 0.7f;
            var y = (float)((Hash(i + 214) * 1200 + seconds * (470 + depth * 400) + high * 24) % 1200) - 60;
            var x = (float)((Hash(i + 541) * 2200 - seconds * 35) % 2200 + 2200) % 2200 - 140;
            var length = (12 + high * 17) * depth;
            Line(x, y, x - length * 0.22f, y + length, new SKColor(153, 196, 218, (byte)(22 + depth * 30 + high * 22)), depth * 1.7f);
        }
    }

    private void Metadata()
    {
        void Text(string text, float y, float size, SKColor color)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _paint.Typeface = _typeface; _paint.TextSize = size; _paint.TextAlign = SKTextAlign.Center;
            var width = _paint.MeasureText(text);
            if (width > 1500) _paint.TextSize *= 1500 / width;
            _paint.Color = color; _canvas.DrawText(text, 960, y, _paint);
        }
        Text(_options.Title, 113, 48, C("#eaf2f4"));
        Text(_options.Artist, 156, 23, C("#9bb2c6"));
        if (!string.IsNullOrWhiteSpace(_options.Title)) Line(929, 183, 991, 183, new SKColor(127, 194, 205, 130), 1);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _paint.Dispose(); _stroke.Dispose(); _canvas.Dispose(); _bitmap.Dispose(); _typeface.Dispose();
        _disposed = true;
    }
}
