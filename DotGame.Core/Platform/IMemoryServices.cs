namespace DotGame.Core.Platform;

public interface IMemoryServices
{
    int PageSizeBytes { get; }

    MemoryStatistics QueryProcessMemory();
}
