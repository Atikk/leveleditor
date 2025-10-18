using System;
using System.Threading;
using System.Threading.Tasks;
using DotGame.Core.Logging;
using DotGame.Core.Timing;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Media;
using global::Avalonia.Threading;
using AvaloniaInside.MonoGame;
using Microsoft.Xna.Framework;

namespace Dotgame.Avalonia.Controls
{
    /// <summary>
    /// Wraps <see cref="MonoGameControl"/> and pumps the MonoGame game loop using the shared
    /// <see cref="FrameLoopController"/> so preview timing aligns with deterministic loop tooling.
    /// </summary>
    public sealed class RuntimePreviewHostControl : ContentControl
    {
        private readonly MonoGameControl _innerControl;
        private readonly ILogger logger = LogManager.GetLogger<RuntimePreviewHostControl>();
        private CancellationTokenSource? loopCancellation;
        private Task? loopTask;
        private FrameTimingInfo latestTiming;
        private bool hasTiming;
        private readonly object timingGate = new();

        public RuntimePreviewHostControl()
        {
            _innerControl = new MonoGameControl();
            Content = _innerControl;

            Focusable = true;
            PointerPressed += OnPointerPressed;
        }

        public Game? Game
        {
            get => _innerControl.Game;
            set => _innerControl.Game = value;
        }

        public IBrush FallbackBackground
        {
            get => _innerControl.FallbackBackground;
            set => _innerControl.FallbackBackground = value;
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            StartLoop();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            StopLoop();
            base.OnDetachedFromVisualTree(e);
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!IsEffectivelyEnabled)
                return;

            Focus();
        }

        private void StartLoop()
        {
            if (loopTask != null)
                return;

            loopCancellation = new CancellationTokenSource();

            var controller = new FrameLoopController(TimeSource.Current, 60.0);
            var listener = new FrameTimingLogListener(LogManager.GetLogger("RuntimePreview"));
            controller.RegisterListener(listener);

            loopTask = Task.Run(() => RunLoop(controller, loopCancellation.Token));
        }

        private void StopLoop()
        {
            var cts = loopCancellation;
            if (cts == null)
                return;

            loopCancellation = null;

            cts.Cancel();

            var task = loopTask;
            loopTask = null;

            if (task != null)
            {
                _ = task.ContinueWith(t =>
                {
                    if (t.Exception != null)
                        logger.Error("Runtime preview loop terminated with errors.", t.Exception.Flatten());
                    cts.Dispose();
                }, TaskScheduler.Default);
            }
            else
            {
                cts.Dispose();
            }

            lock (timingGate)
            {
                hasTiming = false;
                latestTiming = default;
            }
        }

        public bool TryGetLatestTiming(out FrameTimingInfo timing)
        {
            lock (timingGate)
            {
                if (!hasTiming)
                {
                    timing = default;
                    return false;
                }

                timing = latestTiming;
                return true;
            }
        }

        private void RunLoop(FrameLoopController controller, CancellationToken cancellationToken)
        {
            try
            {
                controller.Run(
                    fixedStep => ExecuteFrame(fixedStep, cancellationToken),
                    timing =>
                    {
                        lock (timingGate)
                        {
                            latestTiming = timing;
                            hasTiming = true;
                        }
                    },
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown.
            }
            catch (Exception ex)
            {
                logger.Error("Runtime preview loop encountered an error.", ex);
            }
        }

        private bool ExecuteFrame(TimeSpan _, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return false;

            var previewGame = Game;
            if (previewGame == null)
                return !cancellationToken.IsCancellationRequested;

            using var completion = new ManualResetEventSlim(false);

            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    previewGame.RunOneFrame();
                    _innerControl.InvalidateVisual();
                }
                catch (Exception ex)
                {
                    logger.Error("Runtime preview frame execution failed.", ex);
                }
                finally
                {
                    completion.Set();
                }
            }, DispatcherPriority.Render);

            try
            {
                completion.Wait(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return false;
            }

            return true;
        }
    }
}


