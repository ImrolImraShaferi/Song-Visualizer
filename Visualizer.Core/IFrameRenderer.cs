using System;

namespace Visualizer.Core;

public interface IFrameRenderer : IDisposable
{
    int Width { get; }

    int Height { get; }

    void RenderFrame(double rms, long frameIndex, string outputPath);
}
