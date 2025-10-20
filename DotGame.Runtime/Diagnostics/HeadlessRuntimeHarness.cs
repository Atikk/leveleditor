using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using DotGame.Core.Async.Jobs;
using DotGame.Core.Memory;
using DotGame.Core.Timing;

namespace DotGame.Runtime.Diagnostics;

internal sealed class HeadlessRuntimeHarness : IDisposable
{
    private readonly IJobSystem jobSystem;
    private readonly HeadlessRuntimeOptions options;
    private readonly MemoryAllocatorSet allocators;
    private readonly JobScheduleOptions[] jobProfiles;
    private readonly JobExecuteDelegate jobDelegate;
    private readonly JobSemaphore? jobThrottle;
    private int framesRemaining;
    private int currentFrameSeed;
    private int sink;

    public HeadlessRuntimeHarness(IJobSystem jobSystem, HeadlessRuntimeOptions options, MemoryAllocatorSet allocators)
    {
        this.jobSystem = jobSystem ?? throw new ArgumentNullException(nameof(jobSystem));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.allocators = allocators ?? throw new ArgumentNullException(nameof(allocators));
        framesRemaining = Math.Max(1, options.FrameCount);
        jobDelegate = ExecuteJob;
        jobThrottle = options.MaxConcurrentJobs > 0 ? new JobSemaphore(options.MaxConcurrentJobs) : null;

        var batchSize = Math.Max(1, options.BatchSize);
        jobProfiles = new[]
        {
            CreateProfile("headless-high", JobPriority.High, batchSize),
            CreateProfile("headless-normal", JobPriority.Normal, batchSize),
            CreateProfile("headless-low", JobPriority.Low, batchSize),
            CreateProfile("headless-critical", JobPriority.Critical, batchSize),
        };

        JobScheduleOptions CreateProfile(string name, JobPriority priority, int configuredBatchSize)
        {
            return new JobScheduleOptions(name, priority, JobAffinity.Background, allowInlineExecution: false, configuredBatchSize, jobThrottle);
        }
    }

    public bool StepFrame()
    {
        if (framesRemaining <= 0)
            return false;

        var frameIndex = options.FrameCount - framesRemaining;
        PrepareFrame(frameIndex);
        ExecuteFrameWorkload(frameIndex);
        framesRemaining--;
        return framesRemaining > 0;
    }

    private void PrepareFrame(int frameIndex)
    {
        var seed = unchecked(options.Seed + (frameIndex + 1) * 104729);
        Volatile.Write(ref currentFrameSeed, seed);
    }

    private void ExecuteFrameWorkload(int frameIndex)
    {
        var jobCount = options.JobsPerFrame;
        var handleSize = Unsafe.SizeOf<JobHandle>();
        using var handleBuffer = allocators.Stack.Allocate(jobCount * handleSize, alignment: Math.Max(8, handleSize));
        var handles = MemoryMarshal.Cast<byte, JobHandle>(handleBuffer.Memory.Span);
        var fence = new JobFence();

        for (var i = 0; i < jobCount; i++)
        {
            var profile = jobProfiles[i % jobProfiles.Length];
            var descriptor = new JobBatchDescriptor(jobDelegate, options.JobIterations, profile);
            handles[i] = jobSystem.ScheduleBatch(descriptor, fence: fence);
        }

        fence.Wait();

        if (options.SampleStatistics)
        {
            var stats = jobSystem.GetStatistics();
            Interlocked.Add(ref sink, stats.CompletedJobs);
            Interlocked.Add(ref sink, stats.PendingJobs);
        }
    }

    private void ExecuteJob(in JobExecutionContext context)
    {
        var baseSeed = Volatile.Read(ref currentFrameSeed);
        var value = unchecked(baseSeed + (context.IterationIndex + 1) * 1469598103 + (context.WorkerIndex + 1) * 1099511627);
        var innerIterations = options.InnerLoopIterations;
        for (var i = 0; i < innerIterations; i++)
        {
            value = unchecked((value * 1664525) + 1013904223);
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
        }

        Interlocked.Add(ref sink, value);
    }

    public void Dispose()
    {
        // no-op; harness does not own the job system
    }
}

internal sealed class HeadlessRuntimeOptions
{
    private const int DefaultFrameCount = 600;
    private const int DefaultJobsPerFrame = 96;
    private const int DefaultJobIterations = 8;
    private const int DefaultInnerLoopIterations = 256;
    private const int DefaultBatchSize = 1;
    private const int DefaultMaxConcurrency = 0;
    private const double DefaultFrameRate = 60.0;
    private const int DefaultSeed = 1337;

    public HeadlessRuntimeOptions(int frameCount, int jobsPerFrame, int jobIterations, int innerLoopIterations, int batchSize, double targetFrameRate, int seed, int maxConcurrentJobs, bool sampleStatistics)
    {
        FrameCount = Math.Max(1, frameCount);
        JobsPerFrame = Math.Max(1, jobsPerFrame);
        JobIterations = Math.Max(1, jobIterations);
        InnerLoopIterations = Math.Max(1, innerLoopIterations);
        BatchSize = Math.Max(1, batchSize);
        TargetFrameRate = Math.Clamp(targetFrameRate, 1.0, 240.0);
        Seed = seed;
        MaxConcurrentJobs = Math.Max(0, maxConcurrentJobs);
        SampleStatistics = sampleStatistics;
    }

    public int FrameCount { get; }

    public int JobsPerFrame { get; }

    public int JobIterations { get; }

    public int InnerLoopIterations { get; }

    public int BatchSize { get; }

    public double TargetFrameRate { get; }

    public int Seed { get; }

    public int MaxConcurrentJobs { get; }

    public bool SampleStatistics { get; }

    public static HeadlessRuntimeOptions FromEnvironment()
    {
        return new HeadlessRuntimeOptions(
            frameCount: ReadInt("DOTGAME_RUNTIME_HEADLESS_FRAMES", DefaultFrameCount, 1, 100_000),
            jobsPerFrame: ReadInt("DOTGAME_RUNTIME_HEADLESS_JOBS", DefaultJobsPerFrame, 1, 512),
            jobIterations: ReadInt("DOTGAME_RUNTIME_HEADLESS_ITERATIONS", DefaultJobIterations, 1, 512),
            innerLoopIterations: ReadInt("DOTGAME_RUNTIME_HEADLESS_WORK", DefaultInnerLoopIterations, 1, 10_000),
            batchSize: ReadInt("DOTGAME_RUNTIME_HEADLESS_BATCH", DefaultBatchSize, 1, 64),
            targetFrameRate: ReadDouble("DOTGAME_RUNTIME_HEADLESS_FPS", DefaultFrameRate, 1.0, 240.0),
            seed: ReadInt("DOTGAME_RUNTIME_HEADLESS_SEED", DefaultSeed, int.MinValue + 1, int.MaxValue - 1),
            maxConcurrentJobs: ReadInt("DOTGAME_RUNTIME_HEADLESS_CONCURRENCY", DefaultMaxConcurrency, 0, 512),
            sampleStatistics: ReadBool("DOTGAME_RUNTIME_HEADLESS_SAMPLE_STATS", defaultValue: false));
    }

    private static int ReadInt(string variable, int defaultValue, int min, int max)
    {
        var raw = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(raw))
            return defaultValue;

        return Math.Clamp(int.TryParse(raw, out var parsed) ? parsed : defaultValue, min, max);
    }

    private static double ReadDouble(string variable, double defaultValue, double min, double max)
    {
        var raw = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(raw))
            return defaultValue;

        if (!double.TryParse(raw, out var parsed))
            parsed = defaultValue;

        if (double.IsNaN(parsed) || double.IsInfinity(parsed))
            parsed = defaultValue;

        return Math.Clamp(parsed, min, max);
    }

    private static bool ReadBool(string variable, bool defaultValue)
    {
        var raw = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(raw))
            return defaultValue;

        return raw.Equals("1", StringComparison.OrdinalIgnoreCase) ||
               raw.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               raw.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
               raw.Equals("on", StringComparison.OrdinalIgnoreCase);
    }
}
