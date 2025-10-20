using System;
using DotGame.Core.Async.Jobs;
using DotGame.Core.Async.Jobs.Experimental;

namespace DotGame.Runtime.Services;

public static class RuntimeJobSystemFactory
{
    private const string DefaultIdentifier = "async";
    private const int DefaultWorkerCount = 2;
    private const int MaxWorkerCount = 64;

    public static JobSystemActivation CreateFromEnvironment()
    {
        var requested = Environment.GetEnvironmentVariable("DOTGAME_RUNTIME_JOB_SYSTEM");
        var workerSetting = Environment.GetEnvironmentVariable("DOTGAME_RUNTIME_JOB_WORKERS");
        var workerCount = ParseWorkerCount(workerSetting);
        return Create(requested, workerCount);
    }

    public static JobSystemActivation Create(string? descriptor, int workerCount)
    {
        var trimmed = string.IsNullOrWhiteSpace(descriptor) ? null : descriptor.Trim();
        var normalized = (trimmed ?? DefaultIdentifier).ToLowerInvariant();
        var resolved = ResolveIdentifier(normalized, out var recognized);
        var jobSystem = CreateJobSystem(resolved, workerCount);
    return new JobSystemActivation(trimmed, resolved, workerCount, jobSystem, UsedFallback: !recognized && resolved == DefaultIdentifier);
    }

    private static string ResolveIdentifier(string normalized, out bool recognized)
    {
        recognized = true;
        return normalized switch
        {
            "async" or "default" or "tasks" or "task" => DefaultIdentifier,
            "workstealing" or "work-stealing" or "work_stealing" or "ws" => "workstealing",
            "bifurcated" or "bifurcate" or "bifur" or "bf" => "bifurcated",
            _ => ResolveFallback(out recognized)
        };
    }

    private static string ResolveFallback(out bool recognized)
    {
        recognized = false;
        return DefaultIdentifier;
    }

    private static IJobSystem CreateJobSystem(string identifier, int workerCount)
    {
        return identifier switch
        {
            "workstealing" => new WorkStealingJobSystem(workerCount),
            "bifurcated" => new BifurcatedJobSystem(workerCount),
            _ => new AsyncTaskJobSystem(workerCount, "RuntimeJob-"),
        };
    }

    private static int ParseWorkerCount(string? raw)
    {
        if (!int.TryParse(raw, out var value) || value <= 0)
            return DefaultWorkerCount;

        return Math.Clamp(value, 1, MaxWorkerCount);
    }

    public sealed record JobSystemActivation(string? Requested, string Resolved, int WorkerCount, IJobSystem JobSystem, bool UsedFallback);
}
