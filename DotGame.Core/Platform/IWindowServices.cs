using System;

namespace DotGame.Core.Platform;

public interface IWindowServices
{
    bool IsSupported { get; }

    IPlatformWindow CreateWindow(WindowDescriptor descriptor);

    void DestroyWindow(IPlatformWindow window);

    void PumpEvents(TimeSpan maxDuration);
}
