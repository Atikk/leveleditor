using DotGame.Core.Timing;

namespace DotGame.Core.Platform;

public interface IPlatformServices
{
    IFileSystem FileSystem { get; }

    IThreadServices Threading { get; }

    ITimeSource TimeSource { get; }
}
