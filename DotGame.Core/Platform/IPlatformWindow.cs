using System;

namespace DotGame.Core.Platform;

public interface IPlatformWindow : IDisposable
{
    IntPtr Handle { get; }

    string Title { get; set; }

    int Width { get; }

    int Height { get; }

    float Scaling { get; }
}
