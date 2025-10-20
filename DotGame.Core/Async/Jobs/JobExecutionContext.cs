using System.Threading;

namespace DotGame.Core.Async.Jobs;

public readonly struct JobExecutionContext
{
    public JobExecutionContext(CancellationToken cancellationToken, int workerIndex, int iterationIndex, int iterationCount)
    {
        CancellationToken = cancellationToken;
        WorkerIndex = workerIndex;
        IterationIndex = iterationIndex;
        IterationCount = iterationCount;
    }

    public CancellationToken CancellationToken { get; }

    public int WorkerIndex { get; }

    public int IterationIndex { get; }

    public int IterationCount { get; }
}
