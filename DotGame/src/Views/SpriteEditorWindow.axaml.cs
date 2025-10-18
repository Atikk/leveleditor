using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Controls.Shapes;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Platform.Storage;
using global::Avalonia.Threading;
using SkiaSharp;

namespace Dotgame.Avalonia.Views
{
    public sealed class SpriteFrameTimelineItem : IDisposable
    {
        public SpriteFrameTimelineItem(string label, Bitmap preview)
        {
            Label = label;
            Preview = preview;
        }

        public string Label { get; }
        public Bitmap Preview { get; }

        public void Dispose()
        {
            Preview.Dispose();
        }
    }

    public partial class SpriteEditorWindow : Window
    {
        private enum SpriteTool
        {
            Pencil,
            Eraser,
            Fill,
            Line,
            Rectangle,
            Eyedropper
        }

        private const int DefaultCanvasWidth = 64;
        private const int DefaultCanvasHeight = 64;
        private const int DefaultFrameDuration = 120;

        private sealed class FrameData
        {
            public FrameData(Color[,] pixels, double durationMs)
            {
                Pixels = pixels;
                DurationMs = durationMs;
            }

            public Color[,] Pixels { get; }
            public double DurationMs { get; set; }
        }

        private int canvasWidth = DefaultCanvasWidth;
        private int canvasHeight = DefaultCanvasHeight;
        private int zoom = 12;
        private int brushSize = 1;
        private int currentFrame = 0;
        private double frameDurationMs = DefaultFrameDuration;
        private bool showGrid = true;
        private bool onionSkinEnabled;
        private bool mirrorHorizontal;
        private bool mirrorVertical;
        private bool isMouseDown;
        private bool isDrawingShape;
        private bool shapeEraseMode;
        private SpriteTool currentTool = SpriteTool.Pencil;
        private Color selectedColor = Colors.Black;

    private readonly List<FrameData> frames = new();
        private readonly ObservableCollection<SpriteFrameTimelineItem> frameTimelineItems = new();
        private readonly List<(int x, int y)> shapePreviewPixels = new();

        private (int x, int y)? shapeStartPixel;
        private (int x, int y)? shapeCurrentPixel;
        private (int x, int y)? lastDrawPixel;
        private bool suppressColorSync;
    private bool suppressFrameDurationSync;

        private Canvas? pixelCanvas;
        private Border? selectedColorDisplay;
        private WrapPanel? colorPalette;
        private Border? activePaletteSwatch;
        private ListBox? frameTimeline;
        private Image? animationPreview;
        private NumericUpDown? numZoom;
        private NumericUpDown? numCanvasWidth;
        private NumericUpDown? numCanvasHeight;
        private NumericUpDown? numFrameWidth;
        private NumericUpDown? numFrameHeight;
        private NumericUpDown? numBrushSize;
        private NumericUpDown? numFrameDuration;
        private NumericUpDown? numRed;
        private NumericUpDown? numGreen;
        private NumericUpDown? numBlue;
        private NumericUpDown? numAlpha;
        private CheckBox? chkShowGrid;
        private CheckBox? chkOnionSkin;
        private CheckBox? chkMirrorX;
        private CheckBox? chkMirrorY;
        private ToggleButton? toolPencil;
        private ToggleButton? toolEraser;
        private ToggleButton? toolFill;
        private ToggleButton? toolLine;
        private ToggleButton? toolRectangle;
        private ToggleButton? toolEyedropper;
        private Button? btnPlay;
        private Button? btnStop;
        private TextBlock? statusText;

    private DispatcherTimer? animationTimer;
        private int previewPlaybackIndex;
        private bool isPlaying;
    private Bitmap? currentPreviewBitmap;

        public SpriteEditorWindow()
        {
            InitializeComponent();
            frames.Add(CreateBlankFrame());
            AttachUi();
            ResetPalette();
            SelectFrame(0);
            RenderCanvas();
        }

        private void AttachUi()
        {
            pixelCanvas = this.FindControl<Canvas>("PixelCanvas");
            selectedColorDisplay = this.FindControl<Border>("SelectedColorDisplay");
            colorPalette = this.FindControl<WrapPanel>("ColorPalette");
            frameTimeline = this.FindControl<ListBox>("FrameTimeline");
            animationPreview = this.FindControl<Image>("AnimationPreview");
            statusText = this.FindControl<TextBlock>("StatusText");

            numZoom = this.FindControl<NumericUpDown>("NumZoom");
            numCanvasWidth = this.FindControl<NumericUpDown>("NumCanvasWidth");
            numCanvasHeight = this.FindControl<NumericUpDown>("NumCanvasHeight");
            numFrameWidth = this.FindControl<NumericUpDown>("NumFrameWidth");
            numFrameHeight = this.FindControl<NumericUpDown>("NumFrameHeight");
            numBrushSize = this.FindControl<NumericUpDown>("NumBrushSize");
            numFrameDuration = this.FindControl<NumericUpDown>("NumFrameDuration");
            numRed = this.FindControl<NumericUpDown>("NumRed");
            numGreen = this.FindControl<NumericUpDown>("NumGreen");
            numBlue = this.FindControl<NumericUpDown>("NumBlue");
            numAlpha = this.FindControl<NumericUpDown>("NumAlpha");

            chkShowGrid = this.FindControl<CheckBox>("ChkShowGrid");
            chkOnionSkin = this.FindControl<CheckBox>("ChkOnionSkin");
            chkMirrorX = this.FindControl<CheckBox>("ChkMirrorX");
            chkMirrorY = this.FindControl<CheckBox>("ChkMirrorY");

            toolPencil = this.FindControl<ToggleButton>("ToolPencil");
            toolEraser = this.FindControl<ToggleButton>("ToolEraser");
            toolFill = this.FindControl<ToggleButton>("ToolFill");
            toolLine = this.FindControl<ToggleButton>("ToolLine");
            toolRectangle = this.FindControl<ToggleButton>("ToolRectangle");
            toolEyedropper = this.FindControl<ToggleButton>("ToolEyedropper");

            btnPlay = this.FindControl<Button>("BtnPlayAnimation");
            btnStop = this.FindControl<Button>("BtnStopAnimation");

            var btnNewCanvas = this.FindControl<Button>("BtnNewCanvas");
            var btnClear = this.FindControl<Button>("BtnClear");
            var btnLoadSprite = this.FindControl<Button>("BtnLoadSprite");
            var btnSaveSprite = this.FindControl<Button>("BtnSaveSprite");
            var btnImportSheet = this.FindControl<Button>("BtnImportSpriteSheet");
            var btnExportSheet = this.FindControl<Button>("BtnExportSpriteSheet");
            var btnAddFrame = this.FindControl<Button>("BtnAddFrame");
            var btnDuplicateFrame = this.FindControl<Button>("BtnDuplicateFrame");
            var btnDeleteFrame = this.FindControl<Button>("BtnDeleteFrame");
            var btnMoveUp = this.FindControl<Button>("BtnMoveFrameUp");
            var btnMoveDown = this.FindControl<Button>("BtnMoveFrameDown");
            var btnFlipH = this.FindControl<Button>("BtnFlipHorizontal");
            var btnFlipV = this.FindControl<Button>("BtnFlipVertical");
            var btnAddPaletteColor = this.FindControl<Button>("BtnAddPaletteColor");
            var btnRemovePaletteColor = this.FindControl<Button>("BtnRemovePaletteColor");
            var btnResetPalette = this.FindControl<Button>("BtnResetPalette");

            if (frameTimeline != null)
            {
                frameTimeline.ItemsSource = frameTimelineItems;
                frameTimeline.SelectionChanged += FrameTimeline_SelectionChanged;
            }

            if (numZoom != null)
                numZoom.ValueChanged += (_, _) => { zoom = (int)(numZoom.Value ?? 12); RenderCanvas(); };

            if (numBrushSize != null)
                numBrushSize.ValueChanged += (_, _) => { brushSize = Math.Max(1, (int)(numBrushSize.Value ?? 1)); };

            if (numFrameDuration != null)
                numFrameDuration.ValueChanged += (_, _) => OnFrameDurationChanged();

            if (chkShowGrid != null)
                chkShowGrid.IsCheckedChanged += (_, _) => { showGrid = chkShowGrid.IsChecked ?? true; RenderCanvas(); };

            if (chkOnionSkin != null)
                chkOnionSkin.IsCheckedChanged += (_, _) => { onionSkinEnabled = chkOnionSkin.IsChecked ?? false; RenderCanvas(); };

            if (chkMirrorX != null)
                chkMirrorX.IsCheckedChanged += (_, _) => mirrorHorizontal = chkMirrorX.IsChecked ?? false;

            if (chkMirrorY != null)
                chkMirrorY.IsCheckedChanged += (_, _) => mirrorVertical = chkMirrorY.IsChecked ?? false;

            if (btnNewCanvas != null)
                btnNewCanvas.Click += (_, _) => CreateNewCanvas();

            if (btnClear != null)
                btnClear.Click += (_, _) => ClearCurrentFrame();

            if (btnLoadSprite != null)
                btnLoadSprite.Click += BtnLoadSprite_Click;

            if (btnSaveSprite != null)
                btnSaveSprite.Click += BtnSaveSprite_Click;

            if (btnImportSheet != null)
                btnImportSheet.Click += BtnImportSpriteSheet_Click;

            if (btnExportSheet != null)
                btnExportSheet.Click += BtnExportSpriteSheet_Click;

            if (btnAddPaletteColor != null)
                btnAddPaletteColor.Click += (_, _) => AddPaletteColor(selectedColor);

            if (btnRemovePaletteColor != null)
                btnRemovePaletteColor.Click += (_, _) => RemoveSelectedPaletteColor();

            if (btnResetPalette != null)
                btnResetPalette.Click += (_, _) => ResetPalette();

            if (btnAddFrame != null)
                btnAddFrame.Click += (_, _) => AddNewFrame();

            if (btnDuplicateFrame != null)
                btnDuplicateFrame.Click += (_, _) => DuplicateFrame();

            if (btnDeleteFrame != null)
                btnDeleteFrame.Click += (_, _) => DeleteFrame();

            if (btnMoveUp != null)
                btnMoveUp.Click += (_, _) => MoveFrame(-1);

            if (btnMoveDown != null)
                btnMoveDown.Click += (_, _) => MoveFrame(1);

            if (btnFlipH != null)
                btnFlipH.Click += (_, _) => FlipFrame(horizontal: true);

            if (btnFlipV != null)
                btnFlipV.Click += (_, _) => FlipFrame(horizontal: false);

            if (btnPlay != null)
                btnPlay.Click += (_, _) => StartPlayback();

            if (btnStop != null)
                btnStop.Click += (_, _) => StopPlayback();

            HookToolToggle(toolPencil, SpriteTool.Pencil);
            HookToolToggle(toolEraser, SpriteTool.Eraser);
            HookToolToggle(toolFill, SpriteTool.Fill);
            HookToolToggle(toolLine, SpriteTool.Line);
            HookToolToggle(toolRectangle, SpriteTool.Rectangle);
            HookToolToggle(toolEyedropper, SpriteTool.Eyedropper);

            if (numRed != null) numRed.ValueChanged += (_, _) => UpdateCustomColor();
            if (numGreen != null) numGreen.ValueChanged += (_, _) => UpdateCustomColor();
            if (numBlue != null) numBlue.ValueChanged += (_, _) => UpdateCustomColor();
            if (numAlpha != null) numAlpha.ValueChanged += (_, _) => UpdateCustomColor();

            UpdateColorIndicators(selectedColor);
            RefreshFrameLabels();
            UpdatePlaybackStateUi();
            SetStatus("Ready.");
        }

        private void HookToolToggle(ToggleButton? button, SpriteTool tool)
        {
            if (button == null)
                return;

            button.IsCheckedChanged += (_, _) =>
            {
                if (button.IsChecked == true)
                {
                    SelectTool(tool);
                }
            };
        }

        private void SelectTool(SpriteTool tool)
        {
            currentTool = tool;
            toolPencil?.SetCurrentValue(ToggleButton.IsCheckedProperty, tool == SpriteTool.Pencil);
            toolEraser?.SetCurrentValue(ToggleButton.IsCheckedProperty, tool == SpriteTool.Eraser);
            toolFill?.SetCurrentValue(ToggleButton.IsCheckedProperty, tool == SpriteTool.Fill);
            toolLine?.SetCurrentValue(ToggleButton.IsCheckedProperty, tool == SpriteTool.Line);
            toolRectangle?.SetCurrentValue(ToggleButton.IsCheckedProperty, tool == SpriteTool.Rectangle);
            toolEyedropper?.SetCurrentValue(ToggleButton.IsCheckedProperty, tool == SpriteTool.Eyedropper);
        }

        private void FrameTimeline_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (frameTimeline?.SelectedIndex is int index && index >= 0)
            {
                SelectFrame(index);
            }
        }

        private void CreateNewCanvas()
        {
            if (numCanvasWidth != null)
                canvasWidth = Math.Max(1, (int)(numCanvasWidth.Value ?? DefaultCanvasWidth));
            if (numCanvasHeight != null)
                canvasHeight = Math.Max(1, (int)(numCanvasHeight.Value ?? DefaultCanvasHeight));

            frames.Clear();
            frames.Add(CreateBlankFrame());
            RefreshFrameLabels();
            SelectFrame(0);
            RenderCanvas();
            SetStatus($"New canvas {canvasWidth}x{canvasHeight} created.");
        }

        private void ClearCurrentFrame()
        {
            if (currentFrame >= frames.Count)
                return;

            var duration = frames[currentFrame].DurationMs;
            frames[currentFrame] = CreateBlankFrame(duration);
            RenderCanvas();
            SetStatus("Cleared current frame.");
        }

        private void AddNewFrame()
        {
            var insertIndex = currentFrame + 1;
            frames.Insert(insertIndex, CreateBlankFrame());
            RefreshFrameLabels();
            SelectFrame(insertIndex);
            SetStatus("Added new frame.");
        }

        private void DuplicateFrame()
        {
            if (currentFrame >= frames.Count)
                return;

            var clone = CloneFrame(frames[currentFrame]);
            frames.Insert(currentFrame + 1, clone);
            RefreshFrameLabels();
            SelectFrame(currentFrame + 1);
            SetStatus("Duplicated frame.");
        }

        private void DeleteFrame()
        {
            if (frames.Count <= 1)
            {
                SetStatus("At least one frame must remain.");
                return;
            }

            frames.RemoveAt(currentFrame);
            if (currentFrame >= frames.Count)
                currentFrame = frames.Count - 1;

            RefreshFrameLabels();
            SelectFrame(currentFrame);
            SetStatus("Deleted frame.");
        }

        private void MoveFrame(int offset)
        {
            var targetIndex = currentFrame + offset;
            if (targetIndex < 0 || targetIndex >= frames.Count)
                return;

            (frames[currentFrame], frames[targetIndex]) = (frames[targetIndex], frames[currentFrame]);
            RefreshFrameLabels();
            SelectFrame(targetIndex);
            SetStatus(offset < 0 ? "Moved frame up." : "Moved frame down.");
        }

        private void FlipFrame(bool horizontal)
        {
            if (currentFrame >= frames.Count)
                return;

            var frame = frames[currentFrame];
            var pixels = frame.Pixels;
            if (horizontal)
            {
                for (int y = 0; y < canvasHeight; y++)
                {
                    for (int x = 0; x < canvasWidth / 2; x++)
                    {
                        int mirrorX = canvasWidth - 1 - x;
                        (pixels[x, y], pixels[mirrorX, y]) = (pixels[mirrorX, y], pixels[x, y]);
                    }
                }
            }
            else
            {
                for (int x = 0; x < canvasWidth; x++)
                {
                    for (int y = 0; y < canvasHeight / 2; y++)
                    {
                        int mirrorY = canvasHeight - 1 - y;
                        (pixels[x, y], pixels[x, mirrorY]) = (pixels[x, mirrorY], pixels[x, y]);
                    }
                }
            }

            RenderCanvas();
            SetStatus(horizontal ? "Flipped frame horizontally." : "Flipped frame vertically.");
        }

        private void ResetPalette()
        {
            colorPalette?.Children.Clear();
            activePaletteSwatch = null;

            var defaultColors = new[]
            {
                Colors.Black, Colors.White, Colors.Transparent,
                Colors.Red, Colors.Orange, Colors.Yellow, Colors.Lime, Colors.Cyan, Colors.Blue, Colors.Purple,
                Colors.Brown, Colors.Tan, Colors.Pink, Colors.Gold, Colors.Silver, Colors.Gray, Colors.DarkGray
            };

            foreach (var color in defaultColors)
            {
                AddPaletteColor(color, select: false);
            }

            if (colorPalette?.Children.OfType<Border>().FirstOrDefault() is Border first)
            {
                SelectPaletteSwatch(first);
            }
            else
            {
                UpdateColorIndicators(selectedColor);
            }
        }

        private void AddPaletteColor(Color color, bool select = true)
        {
            if (colorPalette == null)
                return;

            var swatch = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(color),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(4),
                Tag = color
            };

            swatch.PointerPressed += (_, __) => SelectPaletteSwatch(swatch);
            colorPalette.Children.Add(swatch);

            if (select)
                SelectPaletteSwatch(swatch);
        }

        private void RemoveSelectedPaletteColor()
        {
            if (colorPalette == null || activePaletteSwatch == null)
                return;

            if (colorPalette.Children.Count <= 1)
            {
                SetStatus("Cannot remove the last palette color.");
                return;
            }

            colorPalette.Children.Remove(activePaletteSwatch);
            activePaletteSwatch = null;

            if (colorPalette.Children.FirstOrDefault() is Border first)
                SelectPaletteSwatch(first);

            SetStatus("Removed palette color.");
        }

        private void SelectPaletteSwatch(Border swatch)
        {
            if (activePaletteSwatch != null)
            {
                activePaletteSwatch.BorderBrush = Brushes.Gray;
                activePaletteSwatch.BorderThickness = new Thickness(1);
            }

            activePaletteSwatch = swatch;
            activePaletteSwatch.BorderBrush = new SolidColorBrush(Color.FromRgb(99, 102, 241));
            activePaletteSwatch.BorderThickness = new Thickness(2);

            if (swatch.Tag is Color color)
            {
                selectedColor = color;
                UpdateColorIndicators(color);
                SetStatus($"Selected color #{color.ToUInt32():X8}.");
            }
        }

        private void UpdateColorIndicators(Color color)
        {
            suppressColorSync = true;
            numRed?.SetCurrentValue(NumericUpDown.ValueProperty, (decimal)color.R);
            numGreen?.SetCurrentValue(NumericUpDown.ValueProperty, (decimal)color.G);
            numBlue?.SetCurrentValue(NumericUpDown.ValueProperty, (decimal)color.B);
            numAlpha?.SetCurrentValue(NumericUpDown.ValueProperty, (decimal)color.A);
            suppressColorSync = false;

            selectedColorDisplay?.SetCurrentValue(Border.BackgroundProperty, new SolidColorBrush(color));
        }

        private void UpdateCustomColor()
        {
            if (suppressColorSync)
                return;

            byte r = (byte)(numRed?.Value ?? selectedColor.R);
            byte g = (byte)(numGreen?.Value ?? selectedColor.G);
            byte b = (byte)(numBlue?.Value ?? selectedColor.B);
            byte a = (byte)(numAlpha?.Value ?? selectedColor.A);

            selectedColor = Color.FromArgb(a, r, g, b);
            selectedColorDisplay?.SetCurrentValue(Border.BackgroundProperty, new SolidColorBrush(selectedColor));

            SetStatus($"Custom color set to #{selectedColor.ToUInt32():X8}.");
        }

        private void OnFrameDurationChanged()
        {
            if (numFrameDuration == null || suppressFrameDurationSync)
                return;

            var rawValue = numFrameDuration.Value ?? DefaultFrameDuration;
            frameDurationMs = Math.Max(20d, (double)rawValue);

            if (currentFrame < frames.Count)
            {
                frames[currentFrame].DurationMs = frameDurationMs;
                RefreshFrameLabels();
            }

            SyncAnimationTimerInterval();
            SetStatus($"Frame duration set to {frameDurationMs:0} ms.");
        }

        private void SyncFrameDurationNumeric(double duration)
        {
            if (numFrameDuration == null)
                return;

            suppressFrameDurationSync = true;
            numFrameDuration.SetCurrentValue(NumericUpDown.ValueProperty, (decimal)duration);
            suppressFrameDurationSync = false;
        }

        private void SyncAnimationTimerInterval()
        {
            if (animationTimer == null || frames.Count == 0)
                return;

            int targetIndex = isPlaying ? previewPlaybackIndex : currentFrame;
            if (targetIndex < 0 || targetIndex >= frames.Count)
                targetIndex = Math.Clamp(targetIndex, 0, frames.Count - 1);

            double duration = frames[targetIndex].DurationMs;
            if (duration <= 0)
                duration = DefaultFrameDuration;

            animationTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(20d, duration));
        }

        private void SelectFrame(int index)
        {
            if (index < 0 || index >= frames.Count)
                return;

            currentFrame = index;
            if (frameTimeline != null && frameTimeline.SelectedIndex != index)
                frameTimeline.SelectedIndex = index;

            frameDurationMs = frames[index].DurationMs;
            SyncFrameDurationNumeric(frameDurationMs);

            isDrawingShape = false;
            shapePreviewPixels.Clear();
            RenderCanvas();
            if (!isPlaying)
                RenderAnimationPreview(index);

            SyncAnimationTimerInterval();

            SetStatus($"Editing frame {index + 1} of {frames.Count}.");
        }

        private FrameData CreateBlankFrame(double? durationOverride = null)
        {
            var pixels = new Color[canvasWidth, canvasHeight];
            for (int x = 0; x < canvasWidth; x++)
                for (int y = 0; y < canvasHeight; y++)
                    pixels[x, y] = Colors.Transparent;
            return new FrameData(pixels, durationOverride ?? frameDurationMs);
        }

        private static FrameData CloneFrame(FrameData source)
        {
            int width = source.Pixels.GetLength(0);
            int height = source.Pixels.GetLength(1);
            var clonePixels = new Color[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    clonePixels[x, y] = source.Pixels[x, y];
            return new FrameData(clonePixels, source.DurationMs);
        }

        private void RefreshFrameLabels()
        {
            foreach (var item in frameTimelineItems)
            {
                item.Dispose();
            }

            frameTimelineItems.Clear();

            for (int i = 0; i < frames.Count; i++)
            {
                var duration = frames[i].DurationMs;
                var label = duration > 0
                    ? $"Frame {i + 1} ({duration:0}ms)"
                    : $"Frame {i + 1}";

                var preview = CreateBitmapFromFrame(frames[i]);
                frameTimelineItems.Add(new SpriteFrameTimelineItem(label, preview));
            }
        }

        private void PixelCanvas_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (pixelCanvas == null)
                return;

            isMouseDown = true;
            lastDrawPixel = null;
            var point = e.GetPosition(pixelCanvas);
            var currentPoint = e.GetCurrentPoint(pixelCanvas);
            HandlePointer(point, currentPoint);
        }

        private void PixelCanvas_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (!isMouseDown || pixelCanvas == null)
                return;

            var point = e.GetPosition(pixelCanvas);
            var currentPoint = e.GetCurrentPoint(pixelCanvas);
            HandlePointer(point, currentPoint);
        }

        private void PixelCanvas_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            isMouseDown = false;
            lastDrawPixel = null;

            if (isDrawingShape && shapeStartPixel.HasValue && shapeCurrentPixel.HasValue)
            {
                ApplyShape(shapeStartPixel.Value, shapeCurrentPixel.Value, shapeEraseMode);
                isDrawingShape = false;
                shapePreviewPixels.Clear();
                RenderCanvas();
            }
        }

        private void HandlePointer(Point canvasPoint, PointerPoint pointerPoint)
        {
            var pixel = GetPixelFromCanvasPoint(canvasPoint);
            if (pixel == null)
                return;

            bool rightButton = pointerPoint.Properties.IsRightButtonPressed;

            switch (currentTool)
            {
                case SpriteTool.Pencil:
                    DrawStroke(pixel.Value, erase: false);
                    break;
                case SpriteTool.Eraser:
                    DrawStroke(pixel.Value, erase: true);
                    break;
                case SpriteTool.Fill:
                    if (!isMouseDown) break;
                    FloodFill(pixel.Value.x, pixel.Value.y, rightButton ? Colors.Transparent : selectedColor);
                    RenderCanvas();
                    SetStatus("Applied fill.");
                    isMouseDown = false;
                    break;
                case SpriteTool.Eyedropper:
                    SampleColor(pixel.Value.x, pixel.Value.y);
                    isMouseDown = false;
                    break;
                case SpriteTool.Line:
                case SpriteTool.Rectangle:
                    HandleShapeTool(pixel.Value, rightButton);
                    break;
            }
        }

        private void HandleShapeTool((int x, int y) pixel, bool erase)
        {
            if (!isDrawingShape)
            {
                shapeStartPixel = pixel;
                shapeCurrentPixel = pixel;
                shapeEraseMode = erase || currentTool == SpriteTool.Eraser;
                isDrawingShape = true;
                UpdateShapePreview();
            }
            else
            {
                shapeCurrentPixel = pixel;
                UpdateShapePreview();
            }
        }

        private void UpdateShapePreview()
        {
            shapePreviewPixels.Clear();
            if (!shapeStartPixel.HasValue || !shapeCurrentPixel.HasValue)
                return;

            var start = shapeStartPixel.Value;
            var end = shapeCurrentPixel.Value;

            if (currentTool == SpriteTool.Line)
            {
                foreach (var p in GetLinePixels(start.x, start.y, end.x, end.y))
                    shapePreviewPixels.Add(p);
            }
            else if (currentTool == SpriteTool.Rectangle)
            {
                foreach (var p in GetRectanglePixels(start, end))
                    shapePreviewPixels.Add(p);
            }

            RenderCanvas();
        }

        private void DrawStroke((int x, int y) pixel, bool erase)
        {
            if (currentFrame >= frames.Count)
                return;

            var pointsToDraw = new HashSet<(int x, int y)>();
            if (lastDrawPixel.HasValue && lastDrawPixel.Value != pixel)
            {
                foreach (var p in GetLinePixels(lastDrawPixel.Value.x, lastDrawPixel.Value.y, pixel.x, pixel.y))
                    pointsToDraw.Add(p);
            }
            pointsToDraw.Add(pixel);

            foreach (var p in pointsToDraw)
                ApplyBrush(p.x, p.y, erase);

            lastDrawPixel = pixel;
            RenderCanvas();
        }

        private void ApplyBrush(int centerX, int centerY, bool erase)
        {
            int radius = brushSize / 2;

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int targetX = centerX + dx;
                    int targetY = centerY + dy;

                    foreach (var point in GetMirroredPoints(targetX, targetY))
                        SetPixel(point.x, point.y, erase ? Colors.Transparent : selectedColor);
                }
            }
        }

        private IEnumerable<(int x, int y)> GetMirroredPoints(int x, int y)
        {
            var points = new HashSet<(int x, int y)>();

            void Add(int px, int py)
            {
                if (px >= 0 && px < canvasWidth && py >= 0 && py < canvasHeight)
                    points.Add((px, py));
            }

            Add(x, y);
            if (mirrorHorizontal)
                Add(canvasWidth - 1 - x, y);
            if (mirrorVertical)
                Add(x, canvasHeight - 1 - y);
            if (mirrorHorizontal && mirrorVertical)
                Add(canvasWidth - 1 - x, canvasHeight - 1 - y);

            return points;
        }

        private void ApplyShape((int x, int y) start, (int x, int y) end, bool erase)
        {
            IEnumerable<(int x, int y)> pixels = currentTool == SpriteTool.Line
                ? GetLinePixels(start.x, start.y, end.x, end.y)
                : GetRectanglePixels(start, end);

            foreach (var p in pixels)
            {
                foreach (var point in GetMirroredPoints(p.x, p.y))
                    SetPixel(point.x, point.y, erase ? Colors.Transparent : selectedColor);
            }

            SetStatus(currentTool == SpriteTool.Line ? "Drew line." : "Drew rectangle.");
        }

        private IEnumerable<(int x, int y)> GetLinePixels(int x0, int y0, int x1, int y1)
        {
            int dx = Math.Abs(x1 - x0);
            int dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                yield return (x0, y0);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        private IEnumerable<(int x, int y)> GetRectanglePixels((int x, int y) start, (int x, int y) end)
        {
            int minX = Math.Min(start.x, end.x);
            int maxX = Math.Max(start.x, end.x);
            int minY = Math.Min(start.y, end.y);
            int maxY = Math.Max(start.y, end.y);

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    yield return (x, y);
                }
            }
        }

        private (int x, int y)? GetPixelFromCanvasPoint(Point point)
        {
            int x = (int)Math.Floor(point.X / zoom);
            int y = (int)Math.Floor(point.Y / zoom);
            if (x < 0 || x >= canvasWidth || y < 0 || y >= canvasHeight)
                return null;
            return (x, y);
        }

        private void SetPixel(int x, int y, Color color)
        {
            if (currentFrame >= frames.Count)
                return;

            if (x < 0 || y < 0 || x >= canvasWidth || y >= canvasHeight)
                return;

            frames[currentFrame].Pixels[x, y] = color;
        }

        private void SampleColor(int x, int y)
        {
            if (currentFrame >= frames.Count)
                return;

            var color = frames[currentFrame].Pixels[x, y];
            selectedColor = color;
            UpdateColorIndicators(color);
            SetStatus($"Picked color #{color.ToUInt32():X8}.");
        }

        private void FloodFill(int startX, int startY, Color replacement)
        {
            if (currentFrame >= frames.Count)
                return;

            var pixels = frames[currentFrame].Pixels;
            var target = pixels[startX, startY];
            if (target.Equals(replacement))
                return;

            var queue = new Queue<(int x, int y)>();
            queue.Enqueue((startX, startY));

            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                if (x < 0 || y < 0 || x >= canvasWidth || y >= canvasHeight)
                    continue;
                if (!pixels[x, y].Equals(target))
                    continue;

                foreach (var point in GetMirroredPoints(x, y))
                    pixels[point.x, point.y] = replacement;

                queue.Enqueue((x + 1, y));
                queue.Enqueue((x - 1, y));
                queue.Enqueue((x, y + 1));
                queue.Enqueue((x, y - 1));
            }
        }

        private void RenderCanvas()
        {
            if (pixelCanvas == null || currentFrame >= frames.Count)
                return;

            pixelCanvas.Children.Clear();
            pixelCanvas.Width = canvasWidth * zoom;
            pixelCanvas.Height = canvasHeight * zoom;

            if (onionSkinEnabled && currentFrame > 0)
                RenderFrame(frames[currentFrame - 1].Pixels, alphaOverride: 70);

            RenderFrame(frames[currentFrame].Pixels);

            if (isDrawingShape && shapePreviewPixels.Count > 0)
                RenderPreviewOverlay();

            if (showGrid)
                RenderGrid();
        }

        private void RenderFrame(Color[,] pixels, int? alphaOverride = null)
        {
            if (pixelCanvas == null)
                return;

            for (int x = 0; x < canvasWidth; x++)
            {
                for (int y = 0; y < canvasHeight; y++)
                {
                    var color = pixels[x, y];
                    if (alphaOverride.HasValue)
                        color = Color.FromArgb((byte)alphaOverride.Value, color.R, color.G, color.B);

                    var rect = new Rectangle
                    {
                        Width = zoom,
                        Height = zoom,
                        Fill = new SolidColorBrush(color)
                    };
                    Canvas.SetLeft(rect, x * zoom);
                    Canvas.SetTop(rect, y * zoom);
                    pixelCanvas.Children.Add(rect);
                }
            }
        }

        private void RenderPreviewOverlay()
        {
            if (pixelCanvas == null)
                return;

            var overlayColor = Color.FromArgb(160, selectedColor.R, selectedColor.G, selectedColor.B);

            foreach (var (x, y) in shapePreviewPixels)
            {
                if (x < 0 || y < 0 || x >= canvasWidth || y >= canvasHeight)
                    continue;

                var rect = new Rectangle
                {
                    Width = zoom,
                    Height = zoom,
                    Fill = new SolidColorBrush(overlayColor)
                };
                Canvas.SetLeft(rect, x * zoom);
                Canvas.SetTop(rect, y * zoom);
                pixelCanvas.Children.Add(rect);
            }
        }

        private void RenderGrid()
        {
            if (pixelCanvas == null)
                return;

            for (int i = 0; i <= canvasWidth; i++)
            {
                var vline = new Line
                {
                    StartPoint = new Point(i * zoom, 0),
                    EndPoint = new Point(i * zoom, canvasHeight * zoom),
                    Stroke = Brushes.LightGray,
                    StrokeThickness = 1
                };
                pixelCanvas.Children.Add(vline);
            }

            for (int i = 0; i <= canvasHeight; i++)
            {
                var hline = new Line
                {
                    StartPoint = new Point(0, i * zoom),
                    EndPoint = new Point(canvasWidth * zoom, i * zoom),
                    Stroke = Brushes.LightGray,
                    StrokeThickness = 1
                };
                pixelCanvas.Children.Add(hline);
            }
        }

        private void RenderAnimationPreview(int frameIndex)
        {
            if (animationPreview == null || frameIndex < 0 || frameIndex >= frames.Count)
                return;

            currentPreviewBitmap?.Dispose();
            currentPreviewBitmap = CreateBitmapFromFrame(frames[frameIndex]);
            animationPreview.Source = currentPreviewBitmap;
        }

        private Bitmap CreateBitmapFromFrame(FrameData frame)
        {
            int width = frame.Pixels.GetLength(0);
            int height = frame.Pixels.GetLength(1);
            var skBitmap = new SKBitmap(width, height);
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var color = frame.Pixels[x, y];
                    skBitmap.SetPixel(x, y, new SKColor(color.R, color.G, color.B, color.A));
                }
            }

            using var image = SKImage.FromBitmap(skBitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream();
            data.SaveTo(stream);
            stream.Position = 0;
            return new Bitmap(stream);
        }

        private async void BtnLoadSprite_Click(object? sender, RoutedEventArgs e)
        {
            var provider = StorageProvider;
            if (provider == null)
            {
                SetStatus("Storage provider unavailable.");
                return;
            }

            var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open Sprite",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Image") { Patterns = new[] { "*.png", "*.bmp", "*.jpg", "*.jpeg" } }
                }
            });

            if (files.Count == 0)
                return;

            await using var stream = await files[0].OpenReadAsync();
            using var skStream = new SKManagedStream(stream);
            using var skBitmap = SKBitmap.Decode(skStream);

            if (skBitmap == null)
            {
                SetStatus("Failed to load sprite.");
                return;
            }

            canvasWidth = skBitmap.Width;
            canvasHeight = skBitmap.Height;
            frames.Clear();
            var pixels = new Color[canvasWidth, canvasHeight];
            for (int x = 0; x < canvasWidth; x++)
                for (int y = 0; y < canvasHeight; y++)
                {
                    var c = skBitmap.GetPixel(x, y);
                    pixels[x, y] = Color.FromArgb(c.Alpha, c.Red, c.Green, c.Blue);
                }
            frames.Add(new FrameData(pixels, frameDurationMs));

            numCanvasWidth?.SetCurrentValue(NumericUpDown.ValueProperty, (decimal)canvasWidth);
            numCanvasHeight?.SetCurrentValue(NumericUpDown.ValueProperty, (decimal)canvasHeight);

            RefreshFrameLabels();
            SelectFrame(0);
            RenderCanvas();
            SetStatus("Loaded sprite.");
        }

        private async void BtnSaveSprite_Click(object? sender, RoutedEventArgs e)
        {
            var provider = StorageProvider;
            if (provider == null)
            {
                SetStatus("Storage provider unavailable.");
                return;
            }

            var file = await provider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Sprite",
                SuggestedFileName = "sprite.png",
                DefaultExtension = "png",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("PNG image") { Patterns = new[] { "*.png" } }
                }
            });

            if (file == null || currentFrame >= frames.Count)
                return;

            await using var stream = await file.OpenWriteAsync();
            using var bitmap = CreateBitmapFromFrame(frames[currentFrame]);
            bitmap.Save(stream);
            stream.SetLength(stream.Position);

            SetStatus("Sprite saved.");
        }

        private async void BtnImportSpriteSheet_Click(object? sender, RoutedEventArgs e)
        {
            var provider = StorageProvider;
            if (provider == null)
            {
                SetStatus("Storage provider unavailable.");
                return;
            }

            var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import Sprite Sheet",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Image") { Patterns = new[] { "*.png", "*.bmp", "*.jpg", "*.jpeg" } }
                }
            });

            if (files.Count == 0)
                return;

            int frameW = Math.Max(1, (int)(numFrameWidth?.Value ?? canvasWidth));
            int frameH = Math.Max(1, (int)(numFrameHeight?.Value ?? canvasHeight));

            await using var stream = await files[0].OpenReadAsync();
            using var skStream = new SKManagedStream(stream);
            using var sheet = SKBitmap.Decode(skStream);

            if (sheet == null || frameW <= 0 || frameH <= 0 || sheet.Width < frameW || sheet.Height < frameH)
            {
                SetStatus("Invalid sprite sheet dimensions.");
                return;
            }

            int cols = sheet.Width / frameW;
            int rows = sheet.Height / frameH;
            if (cols == 0 || rows == 0)
            {
                SetStatus("Frame size does not fit sprite sheet.");
                return;
            }

            canvasWidth = frameW;
            canvasHeight = frameH;
            frames.Clear();

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    var pixels = new Color[frameW, frameH];
                    for (int x = 0; x < frameW; x++)
                    {
                        for (int y = 0; y < frameH; y++)
                        {
                            var skColor = sheet.GetPixel(col * frameW + x, row * frameH + y);
                            pixels[x, y] = Color.FromArgb(skColor.Alpha, skColor.Red, skColor.Green, skColor.Blue);
                        }
                    }
                    frames.Add(new FrameData(pixels, frameDurationMs));
                }
            }

            numCanvasWidth?.SetCurrentValue(NumericUpDown.ValueProperty, (decimal)canvasWidth);
            numCanvasHeight?.SetCurrentValue(NumericUpDown.ValueProperty, (decimal)canvasHeight);

            RefreshFrameLabels();
            SelectFrame(0);
            RenderCanvas();
            SetStatus($"Imported sprite sheet ({frames.Count} frames).");
        }

        private async void BtnExportSpriteSheet_Click(object? sender, RoutedEventArgs e)
        {
            if (frames.Count == 0)
                return;

            var provider = StorageProvider;
            if (provider == null)
            {
                SetStatus("Storage provider unavailable.");
                return;
            }

            var file = await provider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Sprite Sheet",
                SuggestedFileName = "spritesheet.png",
                DefaultExtension = "png",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("PNG image") { Patterns = new[] { "*.png" } }
                }
            });

            if (file == null)
                return;

            int totalWidth = canvasWidth * frames.Count;
            int totalHeight = canvasHeight;
            var sheet = new SKBitmap(totalWidth, totalHeight);

            for (int i = 0; i < frames.Count; i++)
            {
                var frame = frames[i].Pixels;
                for (int x = 0; x < canvasWidth; x++)
                {
                    for (int y = 0; y < canvasHeight; y++)
                    {
                        var color = frame[x, y];
                        sheet.SetPixel(i * canvasWidth + x, y, new SKColor(color.R, color.G, color.B, color.A));
                    }
                }
            }

            using var image = SKImage.FromBitmap(sheet);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            await using var stream = await file.OpenWriteAsync();
            data.SaveTo(stream);
            stream.SetLength(stream.Position);

            SetStatus("Exported sprite sheet.");
        }

        private void StartPlayback()
        {
            if (frames.Count == 0)
                return;

            isPlaying = true;
            previewPlaybackIndex = currentFrame;
            animationTimer ??= new DispatcherTimer();
            animationTimer.Tick -= AnimationTimerOnTick;
            animationTimer.Tick += AnimationTimerOnTick;
            SyncAnimationTimerInterval();
            animationTimer.Start();
            UpdatePlaybackStateUi();
            SetStatus("Playing animation preview.");
        }

        private void StopPlayback()
        {
            if (animationTimer != null)
                animationTimer.Stop();
            isPlaying = false;
            RenderAnimationPreview(currentFrame);
            UpdatePlaybackStateUi();
            SetStatus("Stopped playback.");
        }

        private void AnimationTimerOnTick(object? sender, EventArgs e)
        {
            if (frames.Count == 0)
                return;

            RenderAnimationPreview(previewPlaybackIndex);
            previewPlaybackIndex = (previewPlaybackIndex + 1) % frames.Count;
            SyncAnimationTimerInterval();
        }

        private void UpdatePlaybackStateUi()
        {
            if (btnPlay != null)
                btnPlay.IsEnabled = !isPlaying;
            if (btnStop != null)
                btnStop.IsEnabled = isPlaying;

            SyncAnimationTimerInterval();
        }

        private void SetStatus(string message)
        {
            statusText?.SetCurrentValue(TextBlock.TextProperty, message);
        }

        protected override void OnClosed(EventArgs e)
        {
            animationTimer?.Stop();
            currentPreviewBitmap?.Dispose();
            foreach (var item in frameTimelineItems)
            {
                item.Dispose();
            }
            frameTimelineItems.Clear();
            base.OnClosed(e);
        }
    }
}


