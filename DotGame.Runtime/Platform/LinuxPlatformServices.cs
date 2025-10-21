using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using DotGame.Core.Platform;
using DotGame.Core.Timing;

namespace DotGame.Runtime.Platform;

public sealed class LinuxPlatformServices : IPlatformServices
{
    private readonly IFileSystem fileSystem;
    private readonly IThreadServices threadServices;
    private readonly ITimeSource timeSource;
    private readonly IWindowServices windowServices;
    private readonly IMemoryServices memoryServices;
    private readonly IDiagnosticServices diagnosticServices;

    public LinuxPlatformServices()
    {
        timeSource = new HighResolutionTimeSource();
        fileSystem = new LinuxFileSystem();
        threadServices = new LinuxThreadServices();
        windowServices = new LinuxWindowServices();
        memoryServices = new LinuxMemoryServices();
        diagnosticServices = new LinuxDiagnosticServices();
    }

    public IFileSystem FileSystem => fileSystem;

    public IThreadServices Threading => threadServices;

    public ITimeSource TimeSource => timeSource;

    public IWindowServices Windowing => windowServices;

    public IMemoryServices Memory => memoryServices;

    public IDiagnosticServices Diagnostics => diagnosticServices;

    private sealed class LinuxFileSystem : IFileSystem
    {
        public bool FileExists(string path) => File.Exists(path);

        public bool DirectoryExists(string path) => Directory.Exists(path);

        public Stream OpenRead(string path, FileShare share = FileShare.Read)
        {
            return File.Open(path, FileMode.Open, FileAccess.Read, share);
        }

        public Stream OpenWrite(string path, bool overwrite = true, FileShare share = FileShare.None)
        {
            var mode = overwrite ? FileMode.Create : FileMode.CreateNew;
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            return File.Open(path, mode, FileAccess.Write, share);
        }

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
        {
            return Directory.EnumerateFiles(path, searchPattern, searchOption);
        }

        public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption)
        {
            return Directory.EnumerateDirectories(path, searchPattern, searchOption);
        }

        public string GetAbsolutePath(string path)
        {
            return Path.GetFullPath(path);
        }
    }

    private sealed class LinuxThreadServices : IThreadServices
    {
        public int ProcessorCount => Environment.ProcessorCount;

        public Thread CreateThread(ThreadStart start, bool isBackground = true, string? name = null)
        {
            if (start == null)
                throw new ArgumentNullException(nameof(start));

            var thread = new Thread(start)
            {
                IsBackground = isBackground,
                Name = name
            };

            return thread;
        }

        public void QueueBackgroundWork(Action action, string? name = null)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            ThreadPool.QueueUserWorkItem(_ =>
            {
                if (!string.IsNullOrEmpty(name) && string.IsNullOrEmpty(Thread.CurrentThread.Name))
                    Thread.CurrentThread.Name = name;

                action();
            });
        }

        public void Sleep(TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero)
                return;

            Thread.Sleep(duration);
        }

        public void Yield()
        {
            Thread.Yield();
        }
    }

    private sealed class LinuxWindowServices : IWindowServices
    {
        public bool IsSupported => false;

        public IPlatformWindow CreateWindow(WindowDescriptor descriptor)
        {
            throw new PlatformNotSupportedException("Window creation has not been implemented for Linux platform services yet.");
        }

        public void DestroyWindow(IPlatformWindow window)
        {
            throw new PlatformNotSupportedException("Window management has not been implemented for Linux platform services yet.");
        }

        public void PumpEvents(TimeSpan maxDuration)
        {
            _ = maxDuration;
        }
    }

    private sealed class LinuxMemoryServices : IMemoryServices
    {
        public int PageSizeBytes => Environment.SystemPageSize;

        public MemoryStatistics QueryProcessMemory()
        {
            using var process = Process.GetCurrentProcess();
            var managedHeapBytes = (ulong)GC.GetTotalMemory(forceFullCollection: false);
            return new MemoryStatistics((ulong)process.WorkingSet64, (ulong)process.PrivateMemorySize64, managedHeapBytes);
        }
    }

    private sealed class LinuxDiagnosticServices : IDiagnosticServices
    {
        public PlatformDiagnosticSnapshot CaptureSnapshot()
        {
            using var process = Process.GetCurrentProcess();
            var uptime = DateTime.UtcNow - process.StartTime.ToUniversalTime();
            return new PlatformDiagnosticSnapshot(
                RuntimeInformation.OSDescription,
                RuntimeInformation.ProcessArchitecture.ToString(),
                RuntimeInformation.FrameworkDescription,
                uptime);
        }

        public void WriteTrace(string category, string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            var resolvedCategory = string.IsNullOrWhiteSpace(category) ? "Platform" : category;
            Trace.WriteLine($"[{resolvedCategory}] {message}");
        }
    }
}
