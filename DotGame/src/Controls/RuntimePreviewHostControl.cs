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
        // Simple input bridge to forward Avalonia key events into MonoGame's
        // KeyboardState for the hosted EditorGame. Only common movement keys
        // are mapped here; it can be extended as needed.
        private readonly System.Collections.Generic.HashSet<Microsoft.Xna.Framework.Input.Keys> _pressedKeys = new();

        private readonly object _inputGate = new();

        private void HookInputForwarding()
        {
            // Attach once
            this.KeyDown += RuntimePreviewHostControl_KeyDown;
            this.KeyUp += RuntimePreviewHostControl_KeyUp;
        }

        private void UnhookInputForwarding()
        {
            try
            {
                this.KeyDown -= RuntimePreviewHostControl_KeyDown;
                this.KeyUp -= RuntimePreviewHostControl_KeyUp;
            }
            catch { }
        }

        private void RuntimePreviewHostControl_KeyDown(object? sender, global::Avalonia.Input.KeyEventArgs e)
        {
            var maybe = MapKey(e.Key);
            if (maybe.HasValue)
            {
                lock (_inputGate)
                {
                    _pressedKeys.Add(maybe.Value);
                }
                e.Handled = true;
            }
        }

        private void RuntimePreviewHostControl_KeyUp(object? sender, global::Avalonia.Input.KeyEventArgs e)
        {
            var maybe = MapKey(e.Key);
            if (maybe.HasValue)
            {
                lock (_inputGate)
                {
                    _pressedKeys.Remove(maybe.Value);
                }
                e.Handled = true;
            }
        }

        private static Microsoft.Xna.Framework.Input.Keys? MapKey(global::Avalonia.Input.Key k)
        {
            return k switch
            {
                Key.W => Microsoft.Xna.Framework.Input.Keys.W,
                Key.A => Microsoft.Xna.Framework.Input.Keys.A,
                Key.S => Microsoft.Xna.Framework.Input.Keys.S,
                Key.D => Microsoft.Xna.Framework.Input.Keys.D,
                Key.Up => Microsoft.Xna.Framework.Input.Keys.Up,
                Key.Down => Microsoft.Xna.Framework.Input.Keys.Down,
                Key.Left => Microsoft.Xna.Framework.Input.Keys.Left,
                Key.Right => Microsoft.Xna.Framework.Input.Keys.Right,
                Key.Space => Microsoft.Xna.Framework.Input.Keys.Space,
                Key.Escape => Microsoft.Xna.Framework.Input.Keys.Escape,
                _ => null,
            };
        }

        private Microsoft.Xna.Framework.Input.KeyboardState BuildKeyboardState()
        {
            lock (_inputGate)
            {
                var arr = new Microsoft.Xna.Framework.Input.Keys[_pressedKeys.Count];
                _pressedKeys.CopyTo(arr);
                return new Microsoft.Xna.Framework.Input.KeyboardState(arr);
            }
        }

    // Basic mouse state captured from Avalonia pointer events.
    private int _scrollWheel;
    private Microsoft.Xna.Framework.Input.MouseState _currentMouseState = new Microsoft.Xna.Framework.Input.MouseState(0, 0, 0, Microsoft.Xna.Framework.Input.ButtonState.Released, Microsoft.Xna.Framework.Input.ButtonState.Released, Microsoft.Xna.Framework.Input.ButtonState.Released, Microsoft.Xna.Framework.Input.ButtonState.Released, Microsoft.Xna.Framework.Input.ButtonState.Released);

        private void RuntimePreviewHostControl_PointerMoved(object? sender, global::Avalonia.Input.PointerEventArgs e)
        {
            var p = e.GetPosition(this);
            var props = e.GetCurrentPoint(this).Properties;
            lock (_inputGate)
            {
                _currentMouseState = new Microsoft.Xna.Framework.Input.MouseState(
                    (int)p.X,
                    (int)p.Y,
                    _scrollWheel,
                    props.IsLeftButtonPressed ? Microsoft.Xna.Framework.Input.ButtonState.Pressed : Microsoft.Xna.Framework.Input.ButtonState.Released,
                    props.IsMiddleButtonPressed ? Microsoft.Xna.Framework.Input.ButtonState.Pressed : Microsoft.Xna.Framework.Input.ButtonState.Released,
                    props.IsRightButtonPressed ? Microsoft.Xna.Framework.Input.ButtonState.Pressed : Microsoft.Xna.Framework.Input.ButtonState.Released,
                    props.IsXButton1Pressed ? Microsoft.Xna.Framework.Input.ButtonState.Pressed : Microsoft.Xna.Framework.Input.ButtonState.Released,
                    props.IsXButton2Pressed ? Microsoft.Xna.Framework.Input.ButtonState.Pressed : Microsoft.Xna.Framework.Input.ButtonState.Released);
            }
        }

        private void RuntimePreviewHostControl_PointerReleased(object? sender, global::Avalonia.Input.PointerReleasedEventArgs e)
        {
            var p = e.GetPosition(this);
            var props = e.GetCurrentPoint(this).Properties;
            lock (_inputGate)
            {
                _currentMouseState = new Microsoft.Xna.Framework.Input.MouseState(
                    (int)p.X,
                    (int)p.Y,
                    _scrollWheel,
                    props.IsLeftButtonPressed ? Microsoft.Xna.Framework.Input.ButtonState.Pressed : Microsoft.Xna.Framework.Input.ButtonState.Released,
                    props.IsMiddleButtonPressed ? Microsoft.Xna.Framework.Input.ButtonState.Pressed : Microsoft.Xna.Framework.Input.ButtonState.Released,
                    props.IsRightButtonPressed ? Microsoft.Xna.Framework.Input.ButtonState.Pressed : Microsoft.Xna.Framework.Input.ButtonState.Released,
                    props.IsXButton1Pressed ? Microsoft.Xna.Framework.Input.ButtonState.Pressed : Microsoft.Xna.Framework.Input.ButtonState.Released,
                    props.IsXButton2Pressed ? Microsoft.Xna.Framework.Input.ButtonState.Pressed : Microsoft.Xna.Framework.Input.ButtonState.Released);
            }
        }
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
            PointerReleased += RuntimePreviewHostControl_PointerReleased;
            PointerMoved += RuntimePreviewHostControl_PointerMoved;
            PointerWheelChanged += RuntimePreviewHostControl_PointerWheelChanged;
        }

        public Game? Game
        {
            get
            {
                try
                {
                    return _innerControl.Game;
                }
                catch (Exception ex)
                {
                    logger.Warn("Exception reading inner Game property.", ex);
                    return null;
                }
            }
            set
            {
                try
                {
                    // Detach previous input forwarding if present
                    if (_innerControl.Game is Dotgame.Avalonia.MonoGameLayer.EditorGame prevEditor)
                    {
                        prevEditor.InputProvider = null;
                        UnhookInputForwarding();
                    }

                    _innerControl.Game = value;

                    // If the new Game is an EditorGame, attach input provider and hook keys
                    if (value is Dotgame.Avalonia.MonoGameLayer.EditorGame editor)
                    {
                        editor.InputProvider = new EditorInputProvider(this);
                        HookInputForwarding();
                    }
                }
                catch (Exception ex)
                {
                    logger.Error("Failed to assign Game to inner control.", ex);
                }
            }
        }

        private sealed class EditorInputProvider : Dotgame.Avalonia.MonoGameLayer.EditorGame.IEditorInputProvider
        {
            private readonly RuntimePreviewHostControl _host;

            public EditorInputProvider(RuntimePreviewHostControl host)
            {
                _host = host;
            }

            public Microsoft.Xna.Framework.Input.KeyboardState GetKeyboardState()
            {
                return _host.BuildKeyboardState();
            }

            public Microsoft.Xna.Framework.Input.MouseState GetMouseState()
            {
                lock (_host._inputGate)
                {
                    return _host._currentMouseState;
                }
            }
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
            var p = e.GetPosition(this);
            var props = e.GetCurrentPoint(this).Properties;
            lock (_inputGate)
            {
                _currentMouseState = new Microsoft.Xna.Framework.Input.MouseState(
                    (int)p.X,
                    (int)p.Y,
                    _scrollWheel,
                    props.IsLeftButtonPressed ? Microsoft.Xna.Framework.Input.ButtonState.Pressed : Microsoft.Xna.Framework.Input.ButtonState.Released,
                    props.IsMiddleButtonPressed ? Microsoft.Xna.Framework.Input.ButtonState.Pressed : Microsoft.Xna.Framework.Input.ButtonState.Released,
                    props.IsRightButtonPressed ? Microsoft.Xna.Framework.Input.ButtonState.Pressed : Microsoft.Xna.Framework.Input.ButtonState.Released,
                    props.IsXButton1Pressed ? Microsoft.Xna.Framework.Input.ButtonState.Pressed : Microsoft.Xna.Framework.Input.ButtonState.Released,
                    props.IsXButton2Pressed ? Microsoft.Xna.Framework.Input.ButtonState.Pressed : Microsoft.Xna.Framework.Input.ButtonState.Released);
            }
        }

        private void RuntimePreviewHostControl_PointerWheelChanged(object? sender, global::Avalonia.Input.PointerWheelEventArgs e)
        {
            var delta = (int)Math.Round(e.Delta.Y);
            var p = e.GetPosition(this);
            lock (_inputGate)
            {
                _scrollWheel += delta;
                _currentMouseState = new Microsoft.Xna.Framework.Input.MouseState(
                    (int)p.X,
                    (int)p.Y,
                    _scrollWheel,
                    _currentMouseState.LeftButton,
                    _currentMouseState.MiddleButton,
                    _currentMouseState.RightButton,
                    _currentMouseState.XButton1,
                    _currentMouseState.XButton2);
            }
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

            Game? previewGame;
            try
            {
                previewGame = Game;
            }
            catch (Exception ex)
            {
                logger.Warn("Exception while retrieving Game for frame execution.", ex);
                return !cancellationToken.IsCancellationRequested;
            }

            if (previewGame == null)
                return !cancellationToken.IsCancellationRequested;

            using var completion = new ManualResetEventSlim(false);

            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    try
                    {
                        previewGame.RunOneFrame();
                    }
                    catch (Exception ex)
                    {
                        logger.Error("Runtime preview frame execution failed.", ex);
                    }

                    try
                    {
                        _innerControl.InvalidateVisual();
                    }
                    catch (Exception ex)
                    {
                        logger.Warn("InvalidateVisual on inner control failed.", ex);
                    }
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


