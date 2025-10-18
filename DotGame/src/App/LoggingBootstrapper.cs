using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DotGame.Core.Logging;

namespace Dotgame.Avalonia;

internal static class LoggingBootstrapper
{
    private static bool initialized;

    public static IDisposable Initialize()
    {
        if (initialized)
            return new CompositeDisposable(Array.Empty<IDisposable>());

        var disposables = new List<IDisposable>();
        var sinks = new List<ILogSink>();

        var bufferSink = new BufferedLogSink(500);
        sinks.Add(bufferSink);

        var consoleSink = new ConsoleLogSink();
        sinks.Add(consoleSink);

        var logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Dotgame", "logs");
        var fileName = $"dotgame-editor-{DateTime.UtcNow:yyyyMMdd}.log";
        var fileSink = new FileLogSink(Path.Combine(logDirectory, fileName));
        sinks.Add(fileSink);
        disposables.Add(fileSink);

        LogManager.Initialize(LogLevel.Debug, sinks);

        void AppDomainUnhandled(object? sender, UnhandledExceptionEventArgs args)
        {
            var logger = LogManager.GetLogger("AppDomain");
            if (args.ExceptionObject is Exception exception)
                logger.LogException(exception, "Unhandled application domain exception.", LogLevel.Critical);
            else
                logger.Critical($"Unhandled application domain error: {args.ExceptionObject}");
        }

        void TaskSchedulerUnhandled(object? sender, UnobservedTaskExceptionEventArgs args)
        {
            var logger = LogManager.GetLogger("TaskScheduler");
            logger.LogException(args.Exception, "Unobserved task exception.", LogLevel.Error);
            args.SetObserved();
        }

        AppDomain.CurrentDomain.UnhandledException += AppDomainUnhandled;
        TaskScheduler.UnobservedTaskException += TaskSchedulerUnhandled;

        disposables.Add(new CallbackDisposable(() => AppDomain.CurrentDomain.UnhandledException -= AppDomainUnhandled));
        disposables.Add(new CallbackDisposable(() => TaskScheduler.UnobservedTaskException -= TaskSchedulerUnhandled));

        initialized = true;

        return new CompositeDisposable(disposables);
    }

    private sealed class CompositeDisposable : IDisposable
    {
        private readonly IReadOnlyList<IDisposable> disposables;
        private bool disposed;

        public CompositeDisposable(IReadOnlyList<IDisposable> disposables)
        {
            this.disposables = disposables;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            for (var i = disposables.Count - 1; i >= 0; i--)
                disposables[i].Dispose();

            disposed = true;
        }
    }

    private sealed class CallbackDisposable : IDisposable
    {
        private readonly Action callback;
        private bool disposed;

        public CallbackDisposable(Action callback)
        {
            this.callback = callback;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            callback();
            disposed = true;
        }
    }
}
