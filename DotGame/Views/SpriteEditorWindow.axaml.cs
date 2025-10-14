using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SkiaSharp;
using IOPath = System.IO.Path;

namespace DotGameAvalonia.Views
{
    public partial class SpriteEditorWindow : Window
    {
        private int canvasWidth = 64;
        private int canvasHeight = 64;
        private int zoom = 8;
        private Color selectedColor = Colors.Black;
        private List<Color[,]> frames = new List<Color[,]>();
        private bool isMouseDown = false;
        private bool showGrid = true;
        private int currentFrame = 0;

        private Canvas? pixelCanvas;
        private Border? selectedColorDisplay;
        private WrapPanel? colorPalette;
        private CheckBox? chkShowGrid;
        private NumericUpDown? numZoom, numCurrentFrame, numCanvasWidth, numCanvasHeight;

        public SpriteEditorWindow()
        {
            InitializeComponent();
            frames.Add(new Color[canvasWidth, canvasHeight]);
            InitializeFrame(0);
            AttachEvents();
            CreateColorPalette();
            RenderCanvas();
        }

        private void InitializeFrame(int frameIndex)
        {
            var pixels = frames[frameIndex];
            for (int x = 0; x < canvasWidth; x++)
                for (int y = 0; y < canvasHeight; y++)
                    pixels[x, y] = Colors.Transparent;
        }

        private void AttachEvents()
        {
            pixelCanvas = this.FindControl<Canvas>("PixelCanvas");
            selectedColorDisplay = this.FindControl<Border>("SelectedColorDisplay");
            colorPalette = this.FindControl<WrapPanel>("ColorPalette");
            chkShowGrid = this.FindControl<CheckBox>("ChkShowGrid");
            numZoom = this.FindControl<NumericUpDown>("NumZoom");
            numCurrentFrame = this.FindControl<NumericUpDown>("NumCurrentFrame");
            numCanvasWidth = this.FindControl<NumericUpDown>("NumCanvasWidth");
            numCanvasHeight = this.FindControl<NumericUpDown>("NumCanvasHeight");

            var numRed = this.FindControl<NumericUpDown>("NumRed");
            var numGreen = this.FindControl<NumericUpDown>("NumGreen");
            var numBlue = this.FindControl<NumericUpDown>("NumBlue");

            if (numRed != null)
                numRed.ValueChanged += (s, e) => UpdateCustomColor();
            if (numGreen != null)
                numGreen.ValueChanged += (s, e) => UpdateCustomColor();
            if (numBlue != null)
                numBlue.ValueChanged += (s, e) => UpdateCustomColor();

            if (numZoom != null)
                numZoom.ValueChanged += (s, e) => { zoom = (int)(numZoom.Value ?? 8); RenderCanvas(); };

            if (chkShowGrid != null)
                chkShowGrid.IsCheckedChanged += (s, e) => { showGrid = chkShowGrid.IsChecked ?? true; RenderCanvas(); };

            var btnNewCanvas = this.FindControl<Button>("BtnNewCanvas");
            var btnClear = this.FindControl<Button>("BtnClear");
            var btnLoadSprite = this.FindControl<Button>("BtnLoadSprite");
            var btnSaveSprite = this.FindControl<Button>("BtnSaveSprite");
            var btnPrevFrame = this.FindControl<Button>("BtnPrevFrame");
            var btnNextFrame = this.FindControl<Button>("BtnNextFrame");

            if (btnNewCanvas != null)
                btnNewCanvas.Click += BtnNewCanvas_Click;
            if (btnClear != null)
                btnClear.Click += BtnClear_Click;
            if (btnLoadSprite != null)
                btnLoadSprite.Click += BtnLoadSprite_Click;
            if (btnSaveSprite != null)
                btnSaveSprite.Click += BtnSaveSprite_Click;
            if (btnPrevFrame != null)
                btnPrevFrame.Click += (s, e) => { if (currentFrame > 0) { currentFrame--; if (numCurrentFrame != null) numCurrentFrame.Value = currentFrame; RenderCanvas(); } };
            if (btnNextFrame != null)
                btnNextFrame.Click += (s, e) => 
                { 
                    currentFrame++; 
                    if (currentFrame >= frames.Count) 
                    {
                        frames.Add(new Color[canvasWidth, canvasHeight]);
                        InitializeFrame(currentFrame);
                    }
                    if (numCurrentFrame != null) numCurrentFrame.Value = currentFrame; 
                    RenderCanvas();
                };
        }

        private void CreateColorPalette()
        {
            if (colorPalette == null) return;

            var colors = new[]
            {
                Colors.Black, Colors.White, Colors.Red, Colors.Lime,
                Colors.Blue, Colors.Yellow, Colors.Cyan, Colors.Magenta,
                Colors.Gray, Colors.Silver, Colors.Maroon, Colors.Green,
                Colors.Navy, Colors.Olive, Colors.Teal, Colors.Purple,
                Colors.DarkRed, Colors.Orange, Colors.Gold, Colors.Pink,
                Colors.Brown, Colors.Tan, Colors.Violet, Colors.Indigo,
                Colors.DarkGreen, Colors.DarkBlue, Colors.DarkCyan, Colors.DarkMagenta,
                Colors.Transparent, Colors.LightGray, Colors.DarkGray, Colors.DimGray
            };

            foreach (var color in colors)
            {
                var border = new Border
                {
                    Width = 30,
                    Height = 30,
                    Background = new SolidColorBrush(color),
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(2)
                };

                border.PointerPressed += (s, e) =>
                {
                    selectedColor = color;
                    if (selectedColorDisplay != null)
                        selectedColorDisplay.Background = new SolidColorBrush(color);
                };

                colorPalette.Children.Add(border);
            }

            if (selectedColorDisplay != null)
                selectedColorDisplay.Background = new SolidColorBrush(selectedColor);
        }

        private void UpdateCustomColor()
        {
            var numRed = this.FindControl<NumericUpDown>("NumRed");
            var numGreen = this.FindControl<NumericUpDown>("NumGreen");
            var numBlue = this.FindControl<NumericUpDown>("NumBlue");

            if (numRed == null || numGreen == null || numBlue == null) return;

            byte r = (byte)(numRed.Value ?? 0);
            byte g = (byte)(numGreen.Value ?? 0);
            byte b = (byte)(numBlue.Value ?? 0);

            selectedColor = Color.FromRgb(r, g, b);
            if (selectedColorDisplay != null)
                selectedColorDisplay.Background = new SolidColorBrush(selectedColor);
        }

        private void BtnNewCanvas_Click(object? sender, RoutedEventArgs e)
        {
            if (numCanvasWidth != null && numCanvasHeight != null)
            {
                canvasWidth = (int)(numCanvasWidth.Value ?? 64);
                canvasHeight = (int)(numCanvasHeight.Value ?? 64);
                frames.Clear();
                frames.Add(new Color[canvasWidth, canvasHeight]);
                currentFrame = 0;
                InitializeFrame(0);
                if (numCurrentFrame != null) numCurrentFrame.Value = 0;
                RenderCanvas();
            }
        }

        private void BtnClear_Click(object? sender, RoutedEventArgs e)
        {
            if (currentFrame < frames.Count)
            {
                frames[currentFrame] = new Color[canvasWidth, canvasHeight];
                InitializeFrame(currentFrame);
                RenderCanvas();
            }
        }

        private async void BtnLoadSprite_Click(object? sender, RoutedEventArgs e)
        {
            var dialog = new Window
            {
                Title = "Load Sprite",
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var stack = new StackPanel { Margin = new Thickness(20), Spacing = 10 };
            stack.Children.Add(new TextBlock { Text = "Enter sprite path:" });
            var txtPath = new TextBox { Watermark = "e.g. sprites/character.png" };
            stack.Children.Add(txtPath);

            var btnPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Spacing = 10 };
            var btnOk = new Button { Content = "Load", Width = 80 };
            var btnCancel = new Button { Content = "Cancel", Width = 80 };
            btnPanel.Children.Add(btnOk);
            btnPanel.Children.Add(btnCancel);
            stack.Children.Add(btnPanel);

            dialog.Content = stack;

            btnOk.Click += (s, ev) =>
            {
                var path = txtPath.Text ?? "";
                if (!IOPath.IsPathRooted(path))
                    path = IOPath.Combine("/home/runner/workspace", path);

                if (System.IO.File.Exists(path))
                {
                    try
                    {
                        var bitmap = new Bitmap(path);
                        using var skBitmap = BitmapToSKBitmap(bitmap);
                        
                        canvasWidth = skBitmap.Width;
                        canvasHeight = skBitmap.Height;
                        frames.Clear();
                        frames.Add(new Color[canvasWidth, canvasHeight]);
                        currentFrame = 0;

                        for (int x = 0; x < canvasWidth; x++)
                        {
                            for (int y = 0; y < canvasHeight; y++)
                            {
                                var skColor = skBitmap.GetPixel(x, y);
                                frames[0][x, y] = Color.FromArgb(skColor.Alpha, skColor.Red, skColor.Green, skColor.Blue);
                            }
                        }

                        if (numCanvasWidth != null) numCanvasWidth.Value = canvasWidth;
                        if (numCanvasHeight != null) numCanvasHeight.Value = canvasHeight;
                        if (numCurrentFrame != null) numCurrentFrame.Value = 0;
                        
                        RenderCanvas();
                        dialog.Close();
                    }
                    catch { }
                }
            };
            btnCancel.Click += (s, ev) => dialog.Close();

            await dialog.ShowDialog(this);
        }

        private async void BtnSaveSprite_Click(object? sender, RoutedEventArgs e)
        {
            var dialog = new Window
            {
                Title = "Save Sprite",
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var stack = new StackPanel { Margin = new Thickness(20), Spacing = 10 };
            stack.Children.Add(new TextBlock { Text = "Save sprite to:" });
            var txtPath = new TextBox { Text = "sprites/mysprite.png" };
            stack.Children.Add(txtPath);

            var btnPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Spacing = 10 };
            var btnOk = new Button { Content = "Save", Width = 80 };
            var btnCancel = new Button { Content = "Cancel", Width = 80 };
            btnPanel.Children.Add(btnOk);
            btnPanel.Children.Add(btnCancel);
            stack.Children.Add(btnPanel);

            dialog.Content = stack;

            btnOk.Click += (s, ev) =>
            {
                var path = txtPath.Text ?? "sprites/mysprite.png";
                if (!IOPath.IsPathRooted(path))
                    path = IOPath.Combine("/home/runner/workspace", path);

                System.IO.Directory.CreateDirectory(IOPath.GetDirectoryName(path) ?? ".");

                if (currentFrame < frames.Count)
                {
                    var pixels = frames[currentFrame];
                    var skBitmap = new SKBitmap(canvasWidth, canvasHeight);
                    for (int x = 0; x < canvasWidth; x++)
                    {
                        for (int y = 0; y < canvasHeight; y++)
                        {
                            var color = pixels[x, y];
                            skBitmap.SetPixel(x, y, new SKColor(color.R, color.G, color.B, color.A));
                        }
                    }

                    using var image = SKImage.FromBitmap(skBitmap);
                    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                    using var stream = System.IO.File.OpenWrite(path);
                    data.SaveTo(stream);
                }

                dialog.Close();
            };
            btnCancel.Click += (s, ev) => dialog.Close();

            await dialog.ShowDialog(this);
        }

        private void PixelCanvas_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            isMouseDown = true;
            var point = e.GetPosition(pixelCanvas);
            DrawPixel(point, e.GetCurrentPoint(this).Properties.IsRightButtonPressed);
        }

        private void PixelCanvas_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (isMouseDown && pixelCanvas != null)
            {
                var point = e.GetPosition(pixelCanvas);
                DrawPixel(point, e.GetCurrentPoint(this).Properties.IsRightButtonPressed);
            }
        }

        private void PixelCanvas_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            isMouseDown = false;
        }

        private void DrawPixel(Point location, bool erase)
        {
            int x = (int)(location.X / zoom);
            int y = (int)(location.Y / zoom);

            if (x >= 0 && y >= 0 && x < canvasWidth && y < canvasHeight && currentFrame < frames.Count)
            {
                frames[currentFrame][x, y] = erase ? Colors.Transparent : selectedColor;
                RenderCanvas();
            }
        }

        private void RenderCanvas()
        {
            if (pixelCanvas == null || currentFrame >= frames.Count) return;
            pixelCanvas.Children.Clear();

            pixelCanvas.Width = canvasWidth * zoom;
            pixelCanvas.Height = canvasHeight * zoom;

            var pixels = frames[currentFrame];
            for (int x = 0; x < canvasWidth; x++)
            {
                for (int y = 0; y < canvasHeight; y++)
                {
                    var rect = new Rectangle
                    {
                        Width = zoom,
                        Height = zoom,
                        Fill = new SolidColorBrush(pixels[x, y])
                    };
                    Canvas.SetLeft(rect, x * zoom);
                    Canvas.SetTop(rect, y * zoom);
                    pixelCanvas.Children.Add(rect);
                }
            }

            if (showGrid)
            {
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
        }

        private SKBitmap BitmapToSKBitmap(Bitmap bitmap)
        {
            using var stream = new System.IO.MemoryStream();
            bitmap.Save(stream);
            stream.Position = 0;
            return SKBitmap.Decode(stream);
        }
    }
}
