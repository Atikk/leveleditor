namespace DotGame.Core.Async.Jobs;

public readonly struct JobStatistics
{
    public JobStatistics(int pendingJobs, int activeWorkers, int completedJobs)
    {
        PendingJobs = pendingJobs;
        ActiveWorkers = activeWorkers;
        CompletedJobs = completedJobs;
    }

    public int PendingJobs { get; }

    public int ActiveWorkers { get; }

    public int CompletedJobs { get; }
}
