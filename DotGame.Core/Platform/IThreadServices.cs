using System;
using System.Threading;

namespace DotGame.Core.Platform;

public interface IThreadServices
{
    int ProcessorCount { get; }

    Thread CreateThread(ThreadStart start, bool isBackground = true, string? name = null);

    void QueueBackgroundWork(Action action, string? name = null);

    void Sleep(TimeSpan duration);

    void Yield();
}
