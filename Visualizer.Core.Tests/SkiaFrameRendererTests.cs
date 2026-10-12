using SkiaSharp;
using Visualizer.Core;
using Xunit;

namespace Visualizer.Core.Tests;

public class SkiaFrameRendererTests
{
    [Fact]
    public void RenderFrame_AcceptsFilenameWithoutDirectory()
    {
        var filename = $"renderer-test-{Guid.NewGuid():N}.png";
        try
        {
            using var renderer = new SkiaFrameRenderer(32, 32);
            renderer.RenderFrame(1, 0, filename);
            using var bitmap = SKBitmap.Decode(filename);
            Assert.Equal(32, bitmap.Width);
            Assert.Equal(SKColors.LimeGreen, bitmap.GetPixel(16, 16));
            Assert.Equal(SKColors.Black, bitmap.GetPixel(0, 0));
        }
        finally
        {
            File.Delete(filename);
        }
    }

    [Fact]
    public void RenderToRgba_RejectsDisposedRenderer()
    {
        var renderer = new SkiaFrameRenderer(32, 32);
        renderer.Dispose();
        Assert.Throws<ObjectDisposedException>(() => { renderer.RenderToRgba(1); });
    }
}
