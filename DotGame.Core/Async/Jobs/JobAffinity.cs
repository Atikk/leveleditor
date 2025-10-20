using System;

namespace DotGame.Core.Async.Jobs;

[Flags]
public enum JobAffinity
{
    Any = 0,
    MainThread = 1 << 0,
    Background = 1 << 1,
    IO = 1 << 2,
    Render = 1 << 3
}
