using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.ViewportAdapters;

namespace DotGame.Runtime.Rendering;

public sealed class CameraController
{
    private readonly OrthographicCamera _camera;
    private readonly BoxingViewportAdapter? _viewportAdapter;

    private Vector2? _previousDragPoint;
    private int _previousScrollWheelValue;
    private RectangleF _worldBounds;
    private bool _cameraInitialized;

    private const float CameraPanSpeed = 300f;
    private const float CameraPanSpeedFast = 600f;
    private const float CameraMinZoom = 0.25f;
    private const float CameraMaxZoom = 4f;
    private const float MouseZoomFactor = 1.1f;
    private const float KeyboardZoomRate = 0.75f;

    public CameraController(OrthographicCamera camera, BoxingViewportAdapter? viewportAdapter = null)
    {
        _camera = camera ?? throw new ArgumentNullException(nameof(camera));
        _viewportAdapter = viewportAdapter;
        _previousScrollWheelValue = Mouse.GetState().ScrollWheelValue;
        _worldBounds = RectangleF.Empty;
    }

    public OrthographicCamera Camera => _camera;

    public bool AllowKeyboardPan { get; set; } = true;

    public void HandleInput(GameTime gameTime, KeyboardState keyboard, MouseState mouse)
    {
        if (AllowKeyboardPan)
        {
            var move = Vector2.Zero;

            if (keyboard.IsKeyDown(Keys.Up) || keyboard.IsKeyDown(Keys.W))
                move.Y -= 1f;
            if (keyboard.IsKeyDown(Keys.Down) || keyboard.IsKeyDown(Keys.S))
                move.Y += 1f;
            if (keyboard.IsKeyDown(Keys.Left) || keyboard.IsKeyDown(Keys.A))
                move.X -= 1f;
            if (keyboard.IsKeyDown(Keys.Right) || keyboard.IsKeyDown(Keys.D))
                move.X += 1f;

            if (move != Vector2.Zero)
            {
                move.Normalize();
                var baseSpeed = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift)
                    ? CameraPanSpeedFast
                    : CameraPanSpeed;
                var speed = baseSpeed * (float)gameTime.ElapsedGameTime.TotalSeconds;
                _camera.Position += move * speed;
                ClampCameraToBounds();
            }
        }

        HandleMouseDrag(mouse);
        HandleMouseWheel(mouse);

        var zoomDelta = 0f;
        if (keyboard.IsKeyDown(Keys.OemPlus) || keyboard.IsKeyDown(Keys.Add))
            zoomDelta += 1f;
        if (keyboard.IsKeyDown(Keys.OemMinus) || keyboard.IsKeyDown(Keys.Subtract))
            zoomDelta -= 1f;

        if (Math.Abs(zoomDelta) > float.Epsilon)
        {
            var zoomChange = 1f + zoomDelta * KeyboardZoomRate * (float)gameTime.ElapsedGameTime.TotalSeconds;
            _camera.Zoom = MathHelper.Clamp(_camera.Zoom * zoomChange, CameraMinZoom, CameraMaxZoom);
            ClampCameraToBounds();
        }
    }

    public void HandleViewportResize()
    {
        _viewportAdapter?.Reset();
        ClampCameraToBounds();
    }

    public void SetWorldBounds(RectangleF bounds, bool centerCamera)
    {
        if (bounds == RectangleF.Empty)
            return;

        _worldBounds = bounds;

        if (centerCamera || !_cameraInitialized)
        {
            _camera.Position = _worldBounds.Center;
            _cameraInitialized = true;
        }

        ClampCameraToBounds();
    }

    public void ResetScrollWheel(int currentValue)
    {
        _previousScrollWheelValue = currentValue;
    }

    public void CenterCamera()
    {
        if (_worldBounds != RectangleF.Empty)
        {
            _camera.Position = _worldBounds.Center;
            ClampCameraToBounds();
        }
    }

    private void HandleMouseDrag(MouseState mouse)
    {
        var isMiddleDown = mouse.MiddleButton == ButtonState.Pressed;
        if (!isMiddleDown)
        {
            _previousDragPoint = null;
            return;
        }

        var current = new Vector2(mouse.X, mouse.Y);
        if (_previousDragPoint.HasValue)
        {
            var delta = current - _previousDragPoint.Value;
            if (delta != Vector2.Zero)
            {
                _camera.Position -= delta / _camera.Zoom;
                ClampCameraToBounds();
            }
        }

        _previousDragPoint = current;
    }

    private void HandleMouseWheel(MouseState mouse)
    {
        var scroll = mouse.ScrollWheelValue;
        var delta = scroll - _previousScrollWheelValue;
        if (delta != 0)
        {
            var steps = delta / 120f;
            var zoomFactor = (float)Math.Pow(MouseZoomFactor, steps);
            _camera.Zoom = MathHelper.Clamp(_camera.Zoom * zoomFactor, CameraMinZoom, CameraMaxZoom);
            ClampCameraToBounds();
        }

        _previousScrollWheelValue = scroll;
    }

    private void ClampCameraToBounds()
    {
        if (_worldBounds == RectangleF.Empty)
            return;

        var halfSize = GetViewportHalfExtents();
        var position = _camera.Position;

        if (_worldBounds.Width <= halfSize.X * 2f)
            position.X = _worldBounds.Center.X;
        else
            position.X = MathHelper.Clamp(position.X, _worldBounds.Left + halfSize.X, _worldBounds.Right - halfSize.X);

        if (_worldBounds.Height <= halfSize.Y * 2f)
            position.Y = _worldBounds.Center.Y;
        else
            position.Y = MathHelper.Clamp(position.Y, _worldBounds.Top + halfSize.Y, _worldBounds.Bottom - halfSize.Y);

        _camera.Position = position;
    }

    private Vector2 GetViewportHalfExtents()
    {
        float width;
        float height;

        if (_viewportAdapter != null)
        {
            width = _viewportAdapter.VirtualWidth;
            height = _viewportAdapter.VirtualHeight;
        }
        else
        {
            width = _camera.BoundingRectangle.Width;
            height = _camera.BoundingRectangle.Height;
        }

        var half = new Vector2(width * 0.5f, height * 0.5f);
        return half / _camera.Zoom;
    }
}
