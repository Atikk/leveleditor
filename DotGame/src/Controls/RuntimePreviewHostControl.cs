using System;
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
    /// Wraps <see cref="MonoGameControl"/> and pumps the MonoGame game loop with a dispatcher timer
    /// so the preview keeps ticking even when the tab is not actively rendering.
    /// </summary>
    public sealed class RuntimePreviewHostControl : ContentControl
    {
        private readonly MonoGameControl _innerControl;
        private DispatcherTimer? _loop;
        private bool _isTicking;
        private long _lastLongTickLog;

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
            if (_loop != null)
                return;

            _loop = new DispatcherTimer(TimeSpan.FromMilliseconds(1000.0 / 60.0), DispatcherPriority.Render, OnLoopTick);
            _loop.Start();
        }

        private void StopLoop()
        {
            if (_loop == null)
                return;

            _loop.Stop();
            _loop.Tick -= OnLoopTick;
            _loop = null;
        }

        private void OnLoopTick(object? sender, EventArgs e)
        {
            if (_isTicking)
                return;

            var previewGame = Game;
            if (previewGame == null)
            {
                _innerControl.InvalidateVisual();
                return;
            }

            _isTicking = true;
            var startTick = Environment.TickCount64;

            try
            {
                previewGame.RunOneFrame();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RuntimePreviewHost] Tick error: {ex}");
            }
            finally
            {
                _isTicking = false;
            }

            var duration = Environment.TickCount64 - startTick;
            if (duration > 16 && ShouldLogLongTick())
            {
                Console.WriteLine($"[RuntimePreviewHost] Tick duration {duration} ms");
            }

            _innerControl.InvalidateVisual();
        }

        private bool ShouldLogLongTick()
        {
            var now = Environment.TickCount64;
            if (_lastLongTickLog != 0 && now - _lastLongTickLog < 1000)
                return false;

            _lastLongTickLog = now;
            return true;
        }
    }
}


