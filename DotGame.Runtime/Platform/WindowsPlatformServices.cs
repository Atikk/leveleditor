using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using DotGame.Core.Platform;
using DotGame.Core.Timing;

namespace DotGame.Runtime.Platform;

public sealed class WindowsPlatformServices : IPlatformServices
{
    private readonly IFileSystem fileSystem;
    private readonly IThreadServices threadServices;
    private readonly ITimeSource timeSource;

    public WindowsPlatformServices()
    {
        timeSource = new HighResolutionTimeSource();
        fileSystem = new WindowsFileSystem();
        threadServices = new WindowsThreadServices();
    }

    public IFileSystem FileSystem => fileSystem;

    public IThreadServices Threading => threadServices;

    public ITimeSource TimeSource => timeSource;

    private sealed class WindowsFileSystem : IFileSystem
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

    private sealed class WindowsThreadServices : IThreadServices
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
}
