using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace DotGameAvalonia.Views
{
    public partial class AnimationEditorWindow : Window
    {
        private sealed class FrameRegion
        {
            public required int Index { get; init; }
            public required int X { get; init; }
            public required int Y { get; init; }
            public required int Width { get; init; }
            public required int Height { get; init; }
            public Border? Border { get; set; }
            public Border? Badge { get; set; }
            public TextBlock? BadgeText { get; set; }
            public bool IsSelected { get; set; }

            public override string ToString() => $"Frame {Index + 1} (x={X}, y={Y})";
        }

        private Button? btnLoadSheet;
        private Button? btnSave;
        private Button? btnPlay;
        private Button? btnStop;
        private Button? btnClearSelection;
        private Button? btnRemoveSelected;
        private NumericUpDown? numFrameWidth;
        private NumericUpDown? numFrameHeight;
        private NumericUpDown? numFrameDuration;
        private CheckBox? chkLoop;
        private WrapPanel? frameWrapPanel;
        private ListBox? selectedFramesList;
        private TextBlock? spriteInfoText;
        private Image? previewImage;

        private readonly ObservableCollection<FrameRegion> selectedFrames = new();
        private readonly List<FrameRegion> allFrames = new();
        private Bitmap? spriteSheet;
        private string? spriteSheetPath;
        private DispatcherTimer? playbackTimer;
    private DispatcherTimer? dimensionDebounceTimer;
        private int previewIndex;

    private static readonly IBrush HighlightBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
    private static readonly IBrush DefaultBorderBrush = new SolidColorBrush(Color.FromRgb(209, 213, 219));

        public AnimationEditorWindow()
        {
            InitializeComponent();
            CaptureControls();
            AttachEvents();
            selectedFrames.CollectionChanged += SelectedFrames_CollectionChanged;
            InitializeDebounceTimer();
            UpdateActionStates();
        }

        private void CaptureControls()
        {
            btnLoadSheet = this.FindControl<Button>("BtnLoadSheet");
            btnSave = this.FindControl<Button>("BtnSave");
            btnPlay = this.FindControl<Button>("BtnPlay");
            btnStop = this.FindControl<Button>("BtnStop");
            btnClearSelection = this.FindControl<Button>("BtnClearSelection");
            btnRemoveSelected = this.FindControl<Button>("BtnRemoveSelected");
            numFrameWidth = this.FindControl<NumericUpDown>("NumFrameWidth");
            numFrameHeight = this.FindControl<NumericUpDown>("NumFrameHeight");
            numFrameDuration = this.FindControl<NumericUpDown>("NumFrameDuration");
            chkLoop = this.FindControl<CheckBox>("ChkLoop");
            frameWrapPanel = this.FindControl<WrapPanel>("FrameWrapPanel");
            selectedFramesList = this.FindControl<ListBox>("SelectedFramesList");
            spriteInfoText = this.FindControl<TextBlock>("SpriteInfoText");
            previewImage = this.FindControl<Image>("PreviewImage");

            if (selectedFramesList != null)
                selectedFramesList.ItemsSource = selectedFrames;
        }

        private void InitializeDebounceTimer()
        {
            dimensionDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(220)
            };
            dimensionDebounceTimer.Tick += DimensionDebounceTimer_Tick;
        }

        private void DimensionDebounceTimer_Tick(object? sender, EventArgs e)
        {
            if (dimensionDebounceTimer == null)
                return;

            dimensionDebounceTimer.Stop();
            BuildFrameGrid();
        }

        private void AttachEvents()
        {
            if (btnLoadSheet != null)
                btnLoadSheet.Click += BtnLoadSheet_Click;
            if (btnSave != null)
                btnSave.Click += BtnSave_Click;
            if (btnPlay != null)
                btnPlay.Click += BtnPlay_Click;
            if (btnStop != null)
                btnStop.Click += BtnStop_Click;
            if (btnClearSelection != null)
                btnClearSelection.Click += BtnClearSelection_Click;
            if (btnRemoveSelected != null)
                btnRemoveSelected.Click += BtnRemoveSelected_Click;
            if (numFrameWidth != null)
                numFrameWidth.ValueChanged += FrameDimensionChanged;
            if (numFrameHeight != null)
                numFrameHeight.ValueChanged += FrameDimensionChanged;
            if (numFrameDuration != null)
                numFrameDuration.ValueChanged += FrameDurationChanged;
            if (selectedFramesList != null)
            {
                selectedFramesList.DoubleTapped += SelectedFramesList_DoubleTapped;
                selectedFramesList.KeyDown += SelectedFramesList_KeyDown;
                selectedFramesList.SelectionChanged += SelectedFramesList_SelectionChanged;
            }
        }

        private void SelectedFrames_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshSelectionBadges();
            UpdateActionStates();
        }

        private async void BtnLoadSheet_Click(object? sender, RoutedEventArgs e)
        {
            var provider = StorageProvider;
            if (provider == null)
            {
                await ShowMessageAsync("Storage provider is not available on this platform.");
                return;
            }

            var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Choose Sprite Sheet",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new("Image Files") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg" } }
                }
            });

            if (files == null || files.Count == 0)
                return;

            var path = files[0].Path?.LocalPath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                await ShowMessageAsync("The selected sprite sheet could not be opened.");
                return;
            }

            try
            {
                using var stream = File.OpenRead(path);
                spriteSheet = new Bitmap(stream);
                spriteSheetPath = path;
                StopPreview();
                BuildFrameGrid();
                UpdateSpriteInfo();
            }
            catch (Exception ex)
            {
                spriteSheet = null;
                spriteSheetPath = null;
                frameWrapPanel?.Children.Clear();
                await ShowMessageAsync($"Unable to load sprite sheet: {ex.Message}");
            }
        }

        private void FrameDimensionChanged(object? sender, NumericUpDownValueChangedEventArgs e)
        {
            if (spriteSheet == null)
                return;

            DebounceFrameGridRebuild();
        }

        private void FrameDurationChanged(object? sender, NumericUpDownValueChangedEventArgs e)
        {
            if (playbackTimer != null && playbackTimer.IsEnabled)
            {
                playbackTimer.Interval = TimeSpan.FromMilliseconds(GetFrameDuration());
            }
        }

        private void DebounceFrameGridRebuild()
        {
            if (dimensionDebounceTimer == null)
            {
                BuildFrameGrid();
                return;
            }

            dimensionDebounceTimer.Stop();
            dimensionDebounceTimer.Start();
        }

        private void BuildFrameGrid()
        {
            if (spriteSheet == null || frameWrapPanel == null || numFrameWidth == null || numFrameHeight == null)
                return;

            int frameWidth = Math.Max(1, (int)(numFrameWidth.Value ?? 0));
            int frameHeight = Math.Max(1, (int)(numFrameHeight.Value ?? 0));

            if (frameWidth <= 0 || frameHeight <= 0)
                return;

            StopPreview();
            selectedFrames.CollectionChanged -= SelectedFrames_CollectionChanged;
            try
            {
                foreach (var frame in allFrames)
                    frame.IsSelected = false;
                selectedFrames.Clear();
            }
            finally
            {
                selectedFrames.CollectionChanged += SelectedFrames_CollectionChanged;
            }

            allFrames.Clear();
            frameWrapPanel.Children.Clear();

            var pixelSize = spriteSheet.PixelSize;
            int index = 0;

            for (int y = 0; y + frameHeight <= pixelSize.Height; y += frameHeight)
            {
                for (int x = 0; x + frameWidth <= pixelSize.Width; x += frameWidth)
                {
                    var region = new FrameRegion
                    {
                        Index = index,
                        X = x,
                        Y = y,
                        Width = frameWidth,
                        Height = frameHeight
                    };

                    var rect = new PixelRect(x, y, frameWidth, frameHeight);
                    var cropped = new CroppedBitmap(spriteSheet, rect);

                    var image = new Image
                    {
                        Source = cropped,
                        Stretch = Stretch.Uniform,
                        Width = Math.Max(48, Math.Min(160, frameWidth * 2)),
                        Height = Math.Max(48, Math.Min(160, frameHeight * 2))
                    };

                    var badge = new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(200, 58, 134, 255)),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(6, 2, 6, 2),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(6),
                        IsVisible = false
                    };

                    var badgeText = new TextBlock
                    {
                        Foreground = Brushes.White,
                        FontWeight = FontWeight.Bold,
                        FontSize = 14
                    };
                    badge.Child = badgeText;

                    var grid = new Grid();
                    grid.Children.Add(image);
                    grid.Children.Add(badge);

                    var border = new Border
                    {
                        Child = grid,
                        BorderBrush = DefaultBorderBrush,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(8),
                        Margin = new Thickness(4),
                        Background = Brushes.White,
                        Padding = new Thickness(4)
                    };

                    border.PointerPressed += (_, args) =>
                    {
                        if (args.GetCurrentPoint(border).Properties.IsLeftButtonPressed)
                        {
                            ToggleFrameSelection(region);
                            args.Handled = true;
                        }
                    };

                    region.Border = border;
                    region.Badge = badge;
                    region.BadgeText = badgeText;
                    allFrames.Add(region);
                    frameWrapPanel.Children.Add(border);
                    index++;
                }
            }

            RefreshSelectionBadges();
            UpdateSpriteInfo();
            UpdateActionStates();
        }

        private void ToggleFrameSelection(FrameRegion region)
        {
            if (region.IsSelected)
            {
                region.IsSelected = false;
                selectedFrames.Remove(region);
            }
            else
            {
                region.IsSelected = true;
                selectedFrames.Add(region);
            }
        }

        private void RefreshSelectionBadges()
        {
            foreach (var frame in allFrames)
            {
                if (!frame.IsSelected)
                {
                    if (frame.Border != null)
                    {
                        frame.Border.BorderBrush = DefaultBorderBrush;
                        frame.Border.BorderThickness = new Thickness(1);
                    }
                    if (frame.Badge != null)
                        frame.Badge.IsVisible = false;
                }
            }

            for (int i = 0; i < selectedFrames.Count; i++)
            {
                var frame = selectedFrames[i];
                frame.IsSelected = true;
                if (frame.Border != null)
                {
                    frame.Border.BorderBrush = HighlightBrush;
                    frame.Border.BorderThickness = new Thickness(3);
                }
                if (frame.Badge != null && frame.BadgeText != null)
                {
                    frame.Badge.IsVisible = true;
                    frame.BadgeText.Text = (i + 1).ToString();
                }
            }
        }

        private void BtnClearSelection_Click(object? sender, RoutedEventArgs e)
        {
            foreach (var frame in allFrames)
                frame.IsSelected = false;
            selectedFrames.Clear();
        }

        private void BtnRemoveSelected_Click(object? sender, RoutedEventArgs e)
        {
            if (selectedFramesList?.SelectedItem is FrameRegion frame)
                ToggleFrameSelection(frame);
        }

        private void SelectedFramesList_DoubleTapped(object? sender, RoutedEventArgs e)
        {
            if (selectedFramesList?.SelectedItem is FrameRegion frame)
                ToggleFrameSelection(frame);
        }

        private void SelectedFramesList_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && selectedFramesList?.SelectedItem is FrameRegion frame)
            {
                ToggleFrameSelection(frame);
                e.Handled = true;
            }
        }

        private void SelectedFramesList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            UpdateActionStates();
        }

        private async void BtnSave_Click(object? sender, RoutedEventArgs e)
        {
            if (spriteSheet == null || spriteSheetPath == null)
            {
                await ShowMessageAsync("Load a sprite sheet before saving an animation.");
                return;
            }

            if (selectedFrames.Count == 0)
            {
                await ShowMessageAsync("Select at least one frame to create an animation.");
                return;
            }

            var provider = StorageProvider;
            if (provider == null)
            {
                await ShowMessageAsync("Storage provider is not available on this platform.");
                return;
            }

            var defaultFileName = Path.GetFileNameWithoutExtension(spriteSheetPath) + ".anim.json";
            var file = await provider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Animation",
                SuggestedFileName = defaultFileName,
                FileTypeChoices = new List<FilePickerFileType>
                {
                    new("Animation JSON") { Patterns = new[] { "*.json" } }
                }
            });

            if (file?.Path?.LocalPath == null)
                return;

            var savePath = file.Path.LocalPath;
            Directory.CreateDirectory(Path.GetDirectoryName(savePath) ?? Environment.CurrentDirectory);

            var duration = GetFrameDuration();
            var relativeSpritePath = MakeRelativePath(savePath, spriteSheetPath);

            var export = new
            {
                spriteSheet = relativeSpritePath,
                frameWidth = selectedFrames.First().Width,
                frameHeight = selectedFrames.First().Height,
                frameDuration = duration,
                loop = chkLoop?.IsChecked ?? true,
                frames = selectedFrames.Select((frame, order) => new
                {
                    order,
                    x = frame.X,
                    y = frame.Y,
                    width = frame.Width,
                    height = frame.Height,
                    duration
                }).ToList(),
                createdUtc = DateTime.UtcNow
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(savePath, JsonSerializer.Serialize(export, options));
            await ShowMessageAsync($"Animation saved to {savePath}.", "Animation Saved");
        }

        private void BtnPlay_Click(object? sender, RoutedEventArgs e)
        {
            if (spriteSheet == null || selectedFrames.Count == 0)
            {
                return;
            }

            if (playbackTimer == null)
            {
                playbackTimer = new DispatcherTimer();
                playbackTimer.Tick += PlaybackTimer_Tick;
            }

            previewIndex = 0;
            ShowPreviewFrame(previewIndex);
            playbackTimer.Interval = TimeSpan.FromMilliseconds(GetFrameDuration());
            playbackTimer.Start();
            UpdateActionStates();
        }

        private void BtnStop_Click(object? sender, RoutedEventArgs e)
        {
            StopPreview();
        }

        private void PlaybackTimer_Tick(object? sender, EventArgs e)
        {
            if (spriteSheet == null || selectedFrames.Count == 0)
            {
                StopPreview();
                return;
            }

            previewIndex++;
            if (previewIndex >= selectedFrames.Count)
            {
                if (chkLoop?.IsChecked == true)
                {
                    previewIndex = 0;
                }
                else
                {
                    StopPreview();
                    return;
                }
            }

            ShowPreviewFrame(previewIndex);
        }

        private void ShowPreviewFrame(int index)
        {
            if (spriteSheet == null || previewImage == null || index < 0 || index >= selectedFrames.Count)
                return;

            var frame = selectedFrames[index];
            var rect = new PixelRect(frame.X, frame.Y, frame.Width, frame.Height);
            previewImage.Source = new CroppedBitmap(spriteSheet, rect);
        }

        private void StopPreview()
        {
            if (playbackTimer != null)
            {
                playbackTimer.Stop();
                playbackTimer.Tick -= PlaybackTimer_Tick;
                playbackTimer = null;
            }
            previewIndex = 0;
            UpdateActionStates();
        }

        private void UpdateSpriteInfo()
        {
            if (spriteInfoText == null)
                return;

            if (spriteSheet == null)
            {
                spriteInfoText.Text = "Load a sprite sheet to begin.";
                return;
            }

            int frameWidth = Math.Max(1, (int)(numFrameWidth?.Value ?? 0));
            int frameHeight = Math.Max(1, (int)(numFrameHeight?.Value ?? 0));
            var size = spriteSheet.PixelSize;
            int columns = frameWidth == 0 ? 0 : size.Width / frameWidth;
            int rows = frameHeight == 0 ? 0 : size.Height / frameHeight;
            spriteInfoText.Text = $"{Path.GetFileName(spriteSheetPath)} – {size.Width}x{size.Height}px | Frames: {columns * rows} ({columns} cols × {rows} rows)";
        }

        private void UpdateActionStates()
        {
            bool hasFrames = selectedFrames.Count > 0;
            if (btnSave != null)
                btnSave.IsEnabled = hasFrames && spriteSheet != null;
            if (btnPlay != null)
                btnPlay.IsEnabled = hasFrames && spriteSheet != null;
            if (btnStop != null)
                btnStop.IsEnabled = playbackTimer != null && playbackTimer.IsEnabled;
            if (btnClearSelection != null)
                btnClearSelection.IsEnabled = hasFrames;
            if (btnRemoveSelected != null)
                btnRemoveSelected.IsEnabled = hasFrames && selectedFramesList?.SelectedItem != null;
        }

        private int GetFrameDuration()
        {
            return Math.Max(16, (int)(numFrameDuration?.Value ?? 120));
        }

        private static string MakeRelativePath(string fromFile, string toFile)
        {
            try
            {
                var fromDir = Path.GetDirectoryName(fromFile);
                if (fromDir == null)
                    return toFile;

                var relative = Path.GetRelativePath(fromDir, toFile);
                return relative.Replace('\\', '/');
            }
            catch
            {
                return toFile;
            }
        }

        private Task ShowMessageAsync(string message, string title = "Animation Builder")
        {
            var dialog = new Window
            {
                Title = title,
                Width = 420,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(20),
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            return dialog.ShowDialog(this);
        }

        protected override void OnClosed(EventArgs e)
        {
            StopPreview();
            if (dimensionDebounceTimer != null)
            {
                dimensionDebounceTimer.Tick -= DimensionDebounceTimer_Tick;
                dimensionDebounceTimer.Stop();
                dimensionDebounceTimer = null;
            }
            base.OnClosed(e);
        }
    }
}
