using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Controls.Shapes;
using global::Avalonia.Data;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Markup.Xaml;
using global::Avalonia.Platform.Storage;
using global::Avalonia.VisualTree;
using global::Avalonia.Layout;
using Dotgame.Avalonia.Models;
using Dotgame.Avalonia.Controls;
using global::Avalonia.Threading;
using DotGame.Core.Async;
using DotGame.Core.Async.Jobs;
using DotGame.Core.Diagnostics;
using DotGame.Runtime.Diagnostics;
using DotGame.Core.Logging;
using DotGame.Core.Resources;
using Dotgame.Avalonia.MonoGameLayer;
using Dotgame.Avalonia.Services;
using DotGame.Runtime.Services;
using GameDataEntrySummary = Dotgame.Avalonia.Services.GameDataPreviewService.GameDataEntrySummary;
using SkiaSharp;
using IOPath = System.IO.Path;

namespace Dotgame.Avalonia.Views
{
    public partial class EditorWindow : Window
    {

        // Test hook: a minimal preview-game abstraction used by unit tests to
        // exercise the map-request path without pulling in the full MonoGame
        // runtime or UI stack.
        public interface IPreviewGame
        {
            void RequestMapSwap(Map mapSnapshot);
        }

        /// <summary>
        /// Try to request a preview map swap on an arbitrary preview-game object.
        /// This wraps the call in a safe try/catch so tests can assert the call
        /// path completes without throwing.
        /// </summary>
        public static bool TryRequestPreviewMap(IPreviewGame previewGame, Map mapSnapshot, out string? errorMessage)
        {
            if (previewGame == null)
            {
                errorMessage = "previewGame was null";
                return false;
            }

            try
            {
                previewGame.RequestMapSwap(mapSnapshot ?? throw new ArgumentNullException(nameof(mapSnapshot)));
                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Test-only helper that mirrors the `NotifyPreviewMapUpdate` decision logic
        /// (respecting a fallback-preview condition) and requests a map swap on the
        /// provided preview-game abstraction. This enables unit tests to exercise
        /// the codepath without creating an Avalonia window or a MonoGame runtime.
        /// </summary>
        public static bool NotifyPreviewMapUpdateForTest(IPreviewGame previewGame, Map mapSnapshot, bool usingFallbackPreviewMap, Func<bool> shouldUseFallbackPreviewMap, out string? errorMessage)
        {
            if (previewGame == null)
            {
                errorMessage = "previewGame was null";
                return false;
            }

            if (shouldUseFallbackPreviewMap == null)
            {
                errorMessage = "shouldUseFallbackPreviewMap delegate was null";
                return false;
            }

            try
            {
                if (usingFallbackPreviewMap && shouldUseFallbackPreviewMap())
                {
                    // The editor decided a fallback map is still required; do not
                    // request a swap in this case.
                    errorMessage = null;
                    return false;
                }

                // Clone the map to mimic the editor behavior and request swap.
                var snapshot = mapSnapshot?.Clone() ?? throw new ArgumentNullException(nameof(mapSnapshot));
                previewGame.RequestMapSwap(snapshot);
                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }
        // TileEntry moved to TileTypes.cs as a top-level type. EditorWindow now uses Dotgame.Avalonia.Views.TileEntry.

        // LayerState moved to TileTypes.cs as a top-level type.

        // TilesetState moved to TileTypes.cs as a top-level type.

        private sealed class MapLayerDto
        {
            public string? Id { get; set; }
            public string? Name { get; set; }
            public bool IsVisible { get; set; } = true;
            public double Opacity { get; set; } = 1.0;
            public JsonElement[][]? Tiles { get; set; }
        }

        private sealed class TilesetDto
        {
            public string? Texture { get; set; }
            public int? Columns { get; set; }
            public int? TileCount { get; set; }
            public int? Margin { get; set; }
            public int? Spacing { get; set; }
            public int? FirstId { get; set; }
            public int? TileWidth { get; set; }
            public int? TileHeight { get; set; }
        }

        private sealed class MapFileDto
        {
            public int Cols { get; set; }
            public int Rows { get; set; }
            public int TileW { get; set; }
            public int TileH { get; set; }
            public JsonElement[][]? Map { get; set; }
            public List<MapLayerDto>? Layers { get; set; }
            public int? ActiveLayerIndex { get; set; }
            public TilesetDto? Tileset { get; set; }
        }

        private sealed class LogEntryViewModel
        {
            public LogEntryViewModel(in LogEvent logEvent)
            {
                Timestamp = logEvent.Timestamp;
                Level = logEvent.Level;
                Category = logEvent.Category;
                Message = logEvent.Message;
                ExceptionMessage = logEvent.Exception?.Message;
                Accent = ResolveBrush(logEvent.Level);
            }

            public DateTimeOffset Timestamp { get; }

            public LogLevel Level { get; }

            public string Category { get; }

            public string Message { get; }

            public string? ExceptionMessage { get; }

            public IBrush Accent { get; }

            public string TimestampText => Timestamp.ToLocalTime().ToString("HH:mm:ss");

            public string LevelText => Level.ToString().ToUpperInvariant();

            public string Header => $"{TimestampText} [{LevelText}] {Category}";

            public bool HasException => !string.IsNullOrWhiteSpace(ExceptionMessage);

            private static IBrush ResolveBrush(LogLevel level)
            {
                return level switch
                {
                    LogLevel.Trace => Brushes.Gray,
                    LogLevel.Debug => Brushes.SlateBlue,
                    LogLevel.Information => Brushes.SeaGreen,
                    LogLevel.Warning => Brushes.DarkOrange,
                    LogLevel.Error => Brushes.OrangeRed,
                    LogLevel.Critical => Brushes.DarkRed,
                    _ => Brushes.Gray
                };
            }
        }

        private sealed class TelemetryMetadataEntry
        {
            public TelemetryMetadataEntry(string key, string value)
            {
                Key = key;
                Value = value;
                Display = string.IsNullOrWhiteSpace(value) ? key : $"{key}: {value}";
            }

            public string Key { get; }

            public string Value { get; }

            public string Display { get; }
        }

        private sealed class TelemetryJobSummaryViewModel
        {
            public TelemetryJobSummaryViewModel(string name, int samples, double pendingAverage, int pendingPeak, double activeAverage, int activePeak, int configuredWorkers, int completedFinal)
            {
                Name = name;
                Samples = samples;
                PendingAverage = pendingAverage;
                PendingPeak = pendingPeak;
                ActiveAverage = activeAverage;
                ActivePeak = activePeak;
                ConfiguredWorkers = configuredWorkers;
                CompletedFinal = completedFinal;
                Summary = $"{name}: samples={samples}, pending(avg={pendingAverage:F3}, max={pendingPeak}), active(avg={activeAverage:F3}, peak={activePeak}), configured={configuredWorkers}, completed={completedFinal}";
            }

            public string Name { get; }

            public int Samples { get; }

            public double PendingAverage { get; }

            public int PendingPeak { get; }

            public double ActiveAverage { get; }

            public int ActivePeak { get; }

            public int ConfiguredWorkers { get; }

            public int CompletedFinal { get; }

            public string Summary { get; }
        }

        private sealed class TelemetrySessionViewModel
        {
            public TelemetrySessionViewModel(string sessionName, RuntimeTelemetryExport export, TelemetryJobSystemSelection jobSystem, DeterministicTelemetrySession.TelemetryExportResult files)
            {
                SessionName = sessionName;
                ExportedAt = export.ExportedAt;
                JsonPath = files.JsonPath;
                Metadata = BuildMetadata(export.Metadata);
                Jobs = BuildJobs(export.JobSystems);
                HasMetadata = Metadata.Count > 0;
                HasJobs = Jobs.Count > 0;
                Header = $"{sessionName} • {ExportedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
                Summary = BuildSummary(jobSystem, export);
            }

            public string SessionName { get; }

            public DateTimeOffset ExportedAt { get; }

            public string JsonPath { get; }

            public string Header { get; }

            public string Summary { get; }

            public IReadOnlyList<TelemetryMetadataEntry> Metadata { get; }

            public IReadOnlyList<TelemetryJobSummaryViewModel> Jobs { get; }

            public bool HasMetadata { get; }

            public bool HasJobs { get; }

            private static List<TelemetryMetadataEntry> BuildMetadata(IReadOnlyDictionary<string, string> metadata)
            {
                var entries = new List<TelemetryMetadataEntry>(metadata.Count);
                foreach (var pair in metadata.OrderBy(static p => p.Key, StringComparer.OrdinalIgnoreCase))
                {
                    entries.Add(new TelemetryMetadataEntry(pair.Key, pair.Value));
                }

                return entries;
            }

            private static List<TelemetryJobSummaryViewModel> BuildJobs(IReadOnlyDictionary<string, IReadOnlyList<RuntimeTelemetryRecorder.JobSystemTelemetrySample>> jobSystems)
            {
                var results = new List<TelemetryJobSummaryViewModel>(jobSystems.Count);
                foreach (var pair in jobSystems.OrderBy(static p => p.Key, StringComparer.OrdinalIgnoreCase))
                {
                    var samples = pair.Value;
                    if (samples == null || samples.Count == 0)
                        continue;

                    // compute simple statistics over the collected samples
                    var name = pair.Key;
                    var pendingAverage = samples.Average(s => (double)s.PendingJobs);
                    var pendingPeak = samples.Max(s => s.PendingJobs);
                    var activeAverage = samples.Average(s => (double)s.ActiveWorkers);
                    var activePeak = samples.Max(s => s.ActiveWorkers);
                    var last = samples[^1];

                    results.Add(new TelemetryJobSummaryViewModel(
                        name,
                        samples.Count,
                        pendingAverage,
                        pendingPeak,
                        activeAverage,
                        activePeak,
                        last.ConfiguredWorkers,
                        last.CompletedJobs));
                }

                return results;
            }

            private static string BuildSummary(TelemetryJobSystemSelection jobSystem, RuntimeTelemetryExport export)
            {
                var workerLabel = jobSystem.WorkerCount == 1 ? "worker" : "workers";
                var platform = export.Platform.HasValue
                    ? $"{export.Platform.Value.OperatingSystem} ({export.Platform.Value.ProcessArchitecture})"
                    : jobSystem.Resolved;
                var frameCount = export.Frames.Count;
                var fallback = jobSystem.UsedFallback ? " (fallback)" : string.Empty;
                return $"Captured {frameCount} frame{(frameCount == 1 ? string.Empty : "s")} on {platform} using '{jobSystem.Resolved}'{fallback} ({jobSystem.WorkerCount} {workerLabel}).";
            }
        }

    private readonly List<TileEntry> tiles = new();
    private readonly ITileService _tileService;
    private readonly DotGame.Core.Services.ITileService _coreTileService;
        private TileEntry? selectedTile;
        private Bitmap? spriteSheetImage;
        private string? spriteSheetPath;
        private TilesetState? activeTilesetState;
        private int gridSize = 20;
        private int brushSize = 1;
        private readonly ObservableCollection<LayerState> layers = new();
        private readonly ObservableCollection<string> historyEntries = new();
    private readonly ObservableCollection<GameDataEntrySummary> dialogueSummaries = new();
    private readonly ObservableCollection<GameDataEntrySummary> questSummaries = new();
    private readonly ObservableCollection<GameDataEntrySummary> cutsceneSummaries = new();
    private readonly ObservableCollection<LogEntryViewModel> logEntries = new();
    private readonly ObservableCollection<TelemetrySessionViewModel> telemetrySessions = new();
    private readonly DeterministicTelemetryCaptureService telemetryCaptureService = new();
    private readonly ILogger logger = LogManager.GetLogger<EditorWindow>();
        private int activeLayerIndex;
        private bool isMouseDown;
        private Border? selectedTileBorder;
    private EditorGame? previewGame;
    private readonly object previewGameLock = new();
        private readonly AsyncTaskScheduler scheduler;
        private readonly IJobSystem jobSystem;
        private readonly RuntimeJobSystemFactory.JobSystemActivation jobSystemActivation;
        private readonly ResourceManager resourceManager;
        private readonly GameDataPreviewService gameDataPreviewService;
        private readonly DispatcherTimer resourcePumpTimer;
        private readonly EventHandler resourcePumpHandler;
    private bool gameDataLoaded;
    private bool usingFallbackPreviewMap;
        private bool telemetryCaptureInProgress;
    private Border? diagnosticsPanel;
    private Button? diagnosticsToggleButton;

        private Canvas? mapCanvas;
        private WrapPanel? tilePalette;
        private NumericUpDown? numTileWidth;
        private NumericUpDown? numTileHeight;
        private NumericUpDown? numSpacing;
        private NumericUpDown? numMargin;
        private NumericUpDown? numBrushSize;
        private ComboBox? cmbGridSize;
        private ToggleButton? toolBrush;
        private ToggleButton? toolEraser;
        private ToggleButton? toolFill;
        private ToggleButton? toolRect;
        private ToggleButton? toolLine;
        private ToggleButton? toolPicker;
        private ToggleButton? toolSelect;
        private ToggleButton? toolStamp;
        private ToggleButton? toolCollision;
        private Button? btnUndo;
        private Button? btnRedo;
        private Button? btnZoomOut;
        private Button? btnZoomReset;
        private Button? btnZoomIn;
        private Button? btnAddLayer;
        private Button? btnRemoveLayer;
        private Button? btnLayerUp;
        private Button? btnLayerDown;
        private TextBlock? statusToolText;
        private TextBlock? statusCoordText;
        private TextBlock? statusTileText;
    private TextBlock? statusZoomText;
    private TextBlock? statusAlertText;
        private StackPanel? propertiesPanel;
        private ListBox? historyList;
        private ListBox? layerList;
    private ListBox? logList;
        private CheckBox? gridVisibilityCheck;
        private CheckBox? passabilityOverlayCheck;

    // Combined history service that handles tile and passability history
    private DotGame.Core.CombinedHistoryService? history;

        // Passability change types are provided by CombinedHistoryService to enable sharing between core and UI.

        // TileChange and CompositeTileChange are provided by DotGame.Core.TileHistoryManager
    private ScrollViewer? viewportScroll;
    private TabControl? viewportTabControl;
    private RuntimePreviewHostControl? runtimePreviewHost;
    private Border? runtimePreviewStatusOverlay;
    private TextBlock? runtimePreviewStatusText;
    private TextBlock? runtimePreviewStatusHintText;
    private Button? runtimePreviewStatusLoadMapButton;
    private Button? runtimePreviewStatusDismissButton;
    private Stopwatch? tabSwitchStopwatch;
    private int tabSwitchLayoutPassCount;
    private string? tabSwitchProfileTarget;
    private bool runtimePreviewInitialized;
    private TextBlock? gameDataStatusText;
    private Button? gameDataStatusDismissButton;
    private ListBox? dialogueList;
    private ListBox? questList;
    private ListBox? cutsceneList;
    private ItemsControl? telemetrySessionsList;
    private TextBlock? telemetryStatusText;
        private double zoomLevel = 1.0;
        private bool suppressToolToggle;
        private bool suppressLayerSelection;
        private float currentCellSize = 32f;

        private const double MinZoom = 0.25;
        private const double MaxZoom = 4.0;
    private const int MaxHistoryEntries = 200;
    private const int MaxLogEntries = 300;
        private const string RuntimePreviewDefaultHint = "Press Esc or choose Dismiss when ready.";
    private const string DefaultPreviewMapFileName = "default_map.json";

            private int warningCount;
            private int errorCount;
            private EventHandler<LogEvent>? bufferedLogHandler;

        private static string ResolveDefaultPreviewMapPath()
        {
            var overrideFromEnv = Environment.GetEnvironmentVariable("DOTGAME_PREVIEW_MAP");
            if (!string.IsNullOrWhiteSpace(overrideFromEnv))
                return overrideFromEnv.Trim();

            try
            {
                var configPath = IOPath.Combine(AppContext.BaseDirectory ?? Environment.CurrentDirectory, "default_preview_map.txt");
                if (File.Exists(configPath))
                {
                    var configured = File.ReadAllText(configPath).Trim();
                    if (!string.IsNullOrWhiteSpace(configured))
                        return configured;
                }
            }
            catch
            {
                // ignore config read issues
            }

            var workspaceCandidate = IOPath.Combine(AppContext.BaseDirectory ?? Environment.CurrentDirectory, "maps", DefaultPreviewMapFileName);
            if (File.Exists(workspaceCandidate))
                return workspaceCandidate;

            try
            {
                var downloads = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                var fallbackCandidate = IOPath.Combine(downloads, DefaultPreviewMapFileName);
                if (File.Exists(fallbackCandidate))
                    return fallbackCandidate;
            }
            catch
            {
                // fall back to base directory
            }

            return IOPath.Combine(AppContext.BaseDirectory ?? Environment.CurrentDirectory, DefaultPreviewMapFileName);
        }

        private enum EditorMode
        {
            Tiles,
            Characters,
            Doodads,
            Triggers
        }

        private enum EditorTool
        {
            Brush,
            Eraser,
            Fill,
            Rect,
            Line,
            Picker,
            Select,
            Stamp,
            Collision
        }

    private EditorMode currentMode = EditorMode.Tiles;
    private EditorTool currentTool = EditorTool.Brush;
    private readonly ObservableCollection<Character> characters = new();
    private readonly ObservableCollection<Doodad> doodads = new();
    private readonly ObservableCollection<BehaviorTrigger> triggers = new();
        private Character? pendingCharacterTemplate;
        private Doodad? pendingDoodadTemplate;
    private BehaviorTrigger? pendingTriggerTemplate;
        private Character? selectedCharacter;
        private Doodad? selectedDoodad;
    private BehaviorTrigger? selectedTrigger;
        private bool suppressGridSizeEvent;
        private bool suppressCharacterSelection;
        private bool suppressDoodadSelection;
    private bool suppressTriggerSelection;

    private ListBox? characterList;
    private ListBox? doodadList;
    private ListBox? triggerList;
    private TabControl? assetTabs;

        private StackPanel? TilesToolsPanel => this.FindControl<StackPanel>("TilesTools");
        private StackPanel? CharactersToolsPanel => this.FindControl<StackPanel>("CharactersTools");
        private StackPanel? DoodadsToolsPanel => this.FindControl<StackPanel>("DoodadsTools");

        private Map map;
        // editor-side passability grid. Indexed as [x,y] to match ActiveTiles convention.
        private bool[,]? editorPassability;

        private LayerState ActiveLayer
        {
            get
            {
                if (layers.Count == 0)
                {
                    layers.Add(CreateLayer("Base Layer"));
                    activeLayerIndex = 0;
                }

                activeLayerIndex = Math.Clamp(activeLayerIndex, 0, layers.Count - 1);
                return layers[activeLayerIndex];
            }
        }

        private IEnumerable<LayerState> VisibleLayers => layers.Where(layer => layer.IsVisible);

        private TileEntry?[,] ActiveTiles => ActiveLayer.Tiles;

        private bool InBounds(int x, int y)
        {
            var tiles = ActiveLayer.Tiles;
            if (tiles == null)
                return false;
            return x >= 0 && y >= 0 && x < tiles.GetLength(0) && y < tiles.GetLength(1);
        }

        private void InitializeLayerSystem()
        {
            layers.Clear();
            layers.Add(CreateLayer("Base Layer"));
            activeLayerIndex = 0;
            triggers.Clear();
        }

        private LayerState CreateLayer(string name, TileEntry?[,]? tiles = null, string? id = null, bool isVisible = true, double opacity = 1.0)
        {
            var buffer = tiles ?? CreateTileBuffer(gridSize);
            var layer = new LayerState(id ?? Guid.NewGuid().ToString("N"), name, buffer)
            {
                IsVisible = isVisible
            };
            layer.Opacity = opacity;
            return layer;
        }

        private TileEntry?[,] CreateTileBuffer(int width, int height)
        {
            var coreBuf = _coreTileService.CreateTileBuffer(width, height);
            return ConvertCoreBufferToUi(coreBuf);
        }

        private TileEntry?[,] CreateTileBuffer(int size) => CreateTileBuffer(size, size);

        private void SetActiveLayer(int index, bool scrollIntoView = true)
        {
            if (layers.Count == 0)
                return;

            activeLayerIndex = Math.Clamp(index, 0, layers.Count - 1);
            UpdateLayerSelectionUI(scrollIntoView);
            UpdateStatusTool();
            RefreshPropertiesPanel();
        }

        private void UpdateLayerSelectionUI(bool scrollIntoView)
        {
            if (layerList == null)
                return;

            suppressLayerSelection = true;
            layerList.SelectedIndex = Math.Clamp(activeLayerIndex, 0, layers.Count - 1);
            if (scrollIntoView && layerList.SelectedItem != null)
            {
                layerList.ScrollIntoView(layerList.SelectedItem);
            }
            suppressLayerSelection = false;
        }

        private void ResizeAllLayers(int newSize)
        {
            if (newSize <= 0)
                newSize = 1;

            foreach (var layer in layers)
            {
                var current = layer.Tiles;
                if (current.GetLength(0) == newSize && current.GetLength(1) == newSize)
                    continue;

                var resized = new TileEntry?[newSize, newSize];
                int maxX = Math.Min(newSize, current.GetLength(0));
                int maxY = Math.Min(newSize, current.GetLength(1));
                for (int x = 0; x < maxX; x++)
                {
                    for (int y = 0; y < maxY; y++)
                        resized[x, y] = current[x, y];
                }

                layer.Tiles = resized;
            }

            RefreshPropertiesPanel();
        }

        private void ClearLayerTiles(LayerState layer)
        {
            var tiles = layer.Tiles;
            layer.Tiles = CreateTileBuffer(tiles.GetLength(0), tiles.GetLength(1));
        }

        private void PushHistory(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            historyEntries.Insert(0, $"[{timestamp}] {message}");

            while (historyEntries.Count > MaxHistoryEntries)
                historyEntries.RemoveAt(historyEntries.Count - 1);
        }

        private TileEntry? GetTopmostTile(int x, int y, out LayerState? owningLayer)
        {
            var layerObjs = layers.Cast<object>().ToList();
            var core = _coreTileService.GetTopmostTile(layerObjs, x, y, out var coreOwning);
            owningLayer = coreOwning as LayerState;
            return ConvertCoreEntryToUi(core, map?.SourceDirectory ?? AppContext.BaseDirectory, activeTilesetState);
        }

        private TileEntry? GetTopmostTile(int x, int y) => GetTopmostTile(x, y, out _);

        private string? GetSerializedTileAt(int x, int y)
        {
            return GetTopmostTile(x, y)?.GetSerializedValue();
        }

        private object? GetExportableTileValue(int x, int y, bool preferTileIds)
        {
            var tile = GetTopmostTile(x, y);
            return tile?.GetSerializableValue(preferTileIds);
        }

        private TileEntry? LoadTileEntry(string storedValue, string baseDirectory, TilesetState? tilesetState = null)
        {
            var core = _coreTileService.LoadTileEntry(storedValue, baseDirectory, tilesetState, msg => PushHistory(msg));
            return ConvertCoreEntryToUi(core, baseDirectory, tilesetState);
        }

        private TileEntry?[,] CreateTileBufferFromSerialized(JsonElement[][]? source, string baseDirectory, int fallbackCols, int fallbackRows, TilesetState? tilesetState)
        {
            var coreBuf = _coreTileService.CreateTileBufferFromSerialized(source, baseDirectory, fallbackCols, fallbackRows, tilesetState);
            return ConvertCoreBufferToUi(coreBuf, baseDirectory, tilesetState);
        }

        private TileEntry? CreateTileEntryFromNumber(JsonElement element, TilesetState? tilesetState)
        {
            var core = _coreTileService.CreateTileEntryFromNumber(element, tilesetState);
            return ConvertCoreEntryToUi(core, map?.SourceDirectory ?? AppContext.BaseDirectory, tilesetState);
        }

        private bool ShouldUseFallbackPreviewMap()
        {
            if (!string.IsNullOrWhiteSpace(map.SourceDirectory))
                return false;

            return !EditorHasTilesPlaced();
        }

        private bool EditorHasTilesPlaced()
        {
            return _coreTileService.EditorHasTilesPlaced(layers.Cast<object>());
        }

        private TileEntry? ConvertCoreEntryToUi(DotGame.Core.Services.CoreTileEntry? core, string? baseDirectory, TilesetState? tilesetState)
        {
            if (core == null)
                return null;

            var serialized = core.SerializedValue ?? (core.TileId?.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (serialized == null)
                return null;

            return _tileService.LoadTileEntry(serialized, baseDirectory ?? AppContext.BaseDirectory, tilesetState, msg => PushHistory(msg));
        }

        private TileEntry?[,] ConvertCoreBufferToUi(DotGame.Core.Services.CoreTileEntry?[,]? coreBuf, string? baseDirectory = null, TilesetState? tilesetState = null)
        {
            if (coreBuf == null)
                return null!;

            int w = coreBuf.GetLength(0);
            int h = coreBuf.GetLength(1);
            var outBuf = new TileEntry?[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    outBuf[x, y] = ConvertCoreEntryToUi(coreBuf[x, y], baseDirectory ?? AppContext.BaseDirectory, tilesetState);

            return outBuf;
        }

        private bool TryLoadFallbackPreviewMap(out Map? fallbackMap)
        {
            fallbackMap = null;

            try
            {
                var candidate = ResolveDefaultPreviewMapPath();
                LogPreviewStatus($"Checking fallback preview map at '{candidate}'. Exists={File.Exists(candidate)}");

                if (!File.Exists(candidate))
                {
                    LogPreviewStatus($"Fallback preview map not found at '{candidate}'.");
                    PushHistory($"Preview fallback map not found: {candidate}");
                    return false;
                }

                fallbackMap = Map.LoadFromJson(candidate);
                PushHistory($"Preview fallback map loaded from {candidate}.");
                return true;
            }
            catch (Exception ex)
            {
                LogPreviewStatus($"Failed to load fallback preview map: {ex.Message}");
                PushHistory($"Failed to load preview fallback map: {ex.Message}");
                return false;
            }
        }

        private void AddLayerAfterActive()
        {
            var newLayer = CreateLayer($"Layer {layers.Count + 1}");
            var insertIndex = layers.Count == 0 ? 0 : Math.Clamp(activeLayerIndex + 1, 0, layers.Count);
            layers.Insert(insertIndex, newLayer);
            SetActiveLayer(insertIndex);
            PushHistory($"Added layer '{newLayer.Name}'");
            RenderMap();
            SyncMapFromEditorState();
        }

        private void RemoveActiveLayer()
        {
            if (layers.Count <= 1)
            {
                logger.Warn("Cannot remove the final layer.");
                return;
            }

            var removed = ActiveLayer;
            var oldIndex = activeLayerIndex;
            layers.RemoveAt(oldIndex);
            SetActiveLayer(Math.Clamp(oldIndex - 1, 0, layers.Count - 1));
            PushHistory($"Removed layer '{removed.Name}'");
            RenderMap();
            SyncMapFromEditorState();
        }

        private void MoveActiveLayer(int delta)
        {
            if (layers.Count <= 1 || delta == 0)
                return;

            var newIndex = activeLayerIndex + delta;
            if (newIndex < 0 || newIndex >= layers.Count)
                return;

            var layerName = ActiveLayer.Name;
            layers.Move(activeLayerIndex, newIndex);
            SetActiveLayer(newIndex);
            PushHistory(delta > 0
                ? $"Moved layer '{layerName}' up"
                : $"Moved layer '{layerName}' down");
            RenderMap();
            SyncMapFromEditorState();
        }

        private void ClearActiveLayer()
        {
            ClearLayerTiles(ActiveLayer);
            PushHistory($"Cleared layer '{ActiveLayer.Name}'");
            RenderMap();
            SyncMapFromEditorState();
        }


        // Parameterless ctor for XAML and codepaths that don't supply services.
        public EditorWindow()
            : this(tileService: null)
        {
        }

        // Allow tests or composition roots to inject a custom ITileService. If null, use the default implementation.
        public EditorWindow(ITileService? tileService = null)
        {
            jobSystemActivation = RuntimeJobSystemFactory.CreateFromEnvironment();
            jobSystem = jobSystemActivation.JobSystem;
            var requested = string.IsNullOrWhiteSpace(jobSystemActivation.Requested) ? "default" : jobSystemActivation.Requested;
            logger.Info($"Job system selection -> requested='{requested}', resolved='{jobSystemActivation.Resolved}', workers={jobSystemActivation.WorkerCount}.");
            if (jobSystemActivation.UsedFallback)
            {
                var warning = $"Unknown job system identifier '{requested}'. Falling back to '{jobSystemActivation.Resolved}'.";
                logger.Warn(warning);
                PushHistory(warning);
            }

            scheduler = new AsyncTaskScheduler(workerCount: Math.Max(1, jobSystemActivation.WorkerCount), workerNamePrefix: "EditorWorker-");
            if (tileService != null)
                _tileService = tileService;
            else
            {
                // Try resolve from the application service container; fallback to direct construction.
                var resolved = DotGame.Core.Platform.ServiceContainer.Resolve<Dotgame.Avalonia.Services.ITileService>();
                _tileService = resolved ?? new TileService();
            }

            // Ensure a core-facing ITileService is available. Prefer a registered core implementation,
            // otherwise wrap the UI tile service with the adapter so core consumers get a concrete instance.
            var coreResolved = DotGame.Core.Platform.ServiceContainer.Resolve<DotGame.Core.Services.ITileService>();
            if (coreResolved != null)
            {
                _coreTileService = coreResolved;
            }
            else
            {
                // Create an adapter around the UI tile service and register it for future resolves.
                var adapter = new Dotgame.Avalonia.Services.Adapters.TileServiceAdapter(_tileService);
                try
                {
                    DotGame.Core.Platform.ServiceContainer.RegisterSingleton<DotGame.Core.Services.ITileService>(adapter);
                }
                catch
                {
                    // best-effort registration; ignore if registration fails
                }

                _coreTileService = adapter;
            }
            resourceManager = new ResourceManager(scheduler);
            scheduler.UnhandledException += ex => Dispatcher.UIThread.Post(() => PushHistory($"Scheduler error: {ex.Message}"));
            PushHistory($"Job system active -> {jobSystemActivation.Resolved} (workers={jobSystemActivation.WorkerCount}).");
            gameDataPreviewService = new GameDataPreviewService(resourceManager);
            resourcePumpHandler = (_, _) =>
            {
                try
                {
                    resourceManager.PumpMainThread();
                }
                catch (Exception ex)
                {
                    PushHistory($"Resource pump error: {ex.Message}");
                }
            };
            resourcePumpTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            resourcePumpTimer.Tick += resourcePumpHandler;
            resourcePumpTimer.Start();

            InitializeComponent();
            map = new Map();
            layers.CollectionChanged += Layers_CollectionChanged;
            InitializeLayerSystem();
            AttachEvents();
            InitializeEditorUI();
            InitializeLoggingHooks();
            SyncMapFromEditorState();
            UpdateUndoRedoButtonState();
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            InitializeRuntimePreview();

            // Register an EditorWindow-backed preview service so core consumers
            // can resolve and control the runtime preview via the canonical
            // DotGame.Core.Services.IPreviewService contract.
            try
            {
                var svc = new Dotgame.Avalonia.Services.EditorWindowPreviewService(this);
                DotGame.Core.Platform.ServiceContainer.RegisterSingleton<DotGame.Core.Services.IPreviewService>(svc);
            }
            catch
            {
                // best-effort registration
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void AttachEvents()
        {
            mapCanvas = this.FindControl<Canvas>("MapCanvas");
            tilePalette = this.FindControl<WrapPanel>("TilePalette");
            numTileWidth = this.FindControl<NumericUpDown>("NumTileWidth");
            numTileHeight = this.FindControl<NumericUpDown>("NumTileHeight");
            numSpacing = this.FindControl<NumericUpDown>("NumSpacing");
            numMargin = this.FindControl<NumericUpDown>("NumMargin");
            numBrushSize = this.FindControl<NumericUpDown>("NumBrushSize");
            cmbGridSize = this.FindControl<ComboBox>("CmbGridSize");
            toolBrush = this.FindControl<ToggleButton>("ToolBrush");
            toolEraser = this.FindControl<ToggleButton>("ToolEraser");
            toolFill = this.FindControl<ToggleButton>("ToolFill");
            toolRect = this.FindControl<ToggleButton>("ToolRect");
            toolLine = this.FindControl<ToggleButton>("ToolLine");
            toolPicker = this.FindControl<ToggleButton>("ToolPicker");
            toolSelect = this.FindControl<ToggleButton>("ToolSelect");
            toolStamp = this.FindControl<ToggleButton>("ToolStamp");
            toolCollision = this.FindControl<ToggleButton>("ToolCollision");
            btnUndo = this.FindControl<Button>("BtnUndo");
            btnRedo = this.FindControl<Button>("BtnRedo");
            if (btnUndo != null) btnUndo.Click += (_, _) =>
            {
                if (history != null && history.TileUndoCount > 0) UndoTiles(); else UndoPassability();
            };
            if (btnRedo != null) btnRedo.Click += (_, _) =>
            {
                if (history != null && history.TileRedoCount > 0) RedoTiles(); else RedoPassability();
            };
            btnZoomOut = this.FindControl<Button>("BtnZoomOut");
            btnZoomReset = this.FindControl<Button>("BtnZoomReset");
            btnZoomIn = this.FindControl<Button>("BtnZoomIn");
            btnAddLayer = this.FindControl<Button>("BtnAddLayer");
            btnRemoveLayer = this.FindControl<Button>("BtnRemoveLayer");
            btnLayerUp = this.FindControl<Button>("BtnLayerUp");
            btnLayerDown = this.FindControl<Button>("BtnLayerDown");
            statusToolText = this.FindControl<TextBlock>("StatusToolText");
            statusCoordText = this.FindControl<TextBlock>("StatusCoordText");
            statusTileText = this.FindControl<TextBlock>("StatusTileText");
            statusZoomText = this.FindControl<TextBlock>("StatusZoomText");
            statusAlertText = this.FindControl<TextBlock>("StatusAlertText");
            propertiesPanel = this.FindControl<StackPanel>("PropertiesPanel");
            historyList = this.FindControl<ListBox>("HistoryList");
            layerList = this.FindControl<ListBox>("LayerList");
            logList = this.FindControl<ListBox>("LogList");
            characterList = this.FindControl<ListBox>("CharacterList");
            doodadList = this.FindControl<ListBox>("DoodadList");
            triggerList = this.FindControl<ListBox>("TriggerList");
            gridVisibilityCheck = this.FindControl<CheckBox>("GridVisibilityCheck");
            passabilityOverlayCheck = this.FindControl<CheckBox>("PassabilityOverlayCheck");
            viewportScroll = this.FindControl<ScrollViewer>("ViewportScroll");
            viewportTabControl = this.FindControl<TabControl>("ViewportTabControl");
            runtimePreviewHost = this.FindControl<RuntimePreviewHostControl>("RuntimePreviewHost");
            runtimePreviewStatusOverlay = this.FindControl<Border>("RuntimePreviewStatusOverlay");
            runtimePreviewStatusText = this.FindControl<TextBlock>("RuntimePreviewStatusText");
            runtimePreviewStatusHintText = this.FindControl<TextBlock>("RuntimePreviewStatusHintText");
            runtimePreviewStatusLoadMapButton = this.FindControl<Button>("RuntimePreviewStatusLoadMapButton");
            runtimePreviewStatusDismissButton = this.FindControl<Button>("RuntimePreviewStatusDismissButton");
            assetTabs = this.FindControl<TabControl>("AssetTabs");
            gameDataStatusText = this.FindControl<TextBlock>("GameDataStatusText");
            gameDataStatusDismissButton = this.FindControl<Button>("GameDataStatusDismissButton");
            dialogueList = this.FindControl<ListBox>("DialogueList");
            questList = this.FindControl<ListBox>("QuestList");
            cutsceneList = this.FindControl<ListBox>("CutsceneList");
            telemetrySessionsList = this.FindControl<ItemsControl>("TelemetrySessionsList");
            telemetryStatusText = this.FindControl<TextBlock>("TelemetryStatusText");
            diagnosticsPanel = this.FindControl<Border>("DiagnosticsPanel");
            diagnosticsToggleButton = this.FindControl<Button>("BtnDiagnosticsToggle");
            var btnCloseDiagnostics = this.FindControl<Button>("BtnCloseDiagnostics");

            if (runtimePreviewHost != null)
            {
                runtimePreviewHost.Focusable = true;
                runtimePreviewHost.PointerPressed += RuntimePreviewHost_PointerPressed;
                runtimePreviewHost.AttachedToVisualTree += RuntimePreviewHost_AttachedToVisualTree;
                runtimePreviewHost.DetachedFromVisualTree += RuntimePreviewHost_DetachedFromVisualTree;
                LogPreviewStatus("RuntimePreviewHost registered and ready for attachment events.");
            }

            if (viewportTabControl != null)
            {
                viewportTabControl.SelectionChanged += ViewportTabControl_SelectionChanged;
            }

            // keyboard shortcuts for undo/redo
            this.KeyDown += EditorWindow_KeyDown;
            // Ensure undo/redo UI reflects current stacks
            UpdateUndoRedoButtonState();

            HookToolToggle(toolBrush, EditorTool.Brush);
            HookToolToggle(toolEraser, EditorTool.Eraser);
            HookToolToggle(toolFill, EditorTool.Fill);
            HookToolToggle(toolRect, EditorTool.Rect);
            HookToolToggle(toolLine, EditorTool.Line);
            HookToolToggle(toolPicker, EditorTool.Picker);
            HookToolToggle(toolSelect, EditorTool.Select);
            HookToolToggle(toolStamp, EditorTool.Stamp);
            HookToolToggle(toolCollision, EditorTool.Collision);


            var btnLoadSpriteSheet = this.FindControl<Button>("BtnLoadSpriteSheet");
            var btnSplitSheet = this.FindControl<Button>("BtnSplitSheet");
            var btnLoadTiles = this.FindControl<Button>("BtnLoadTiles");
            var btnClearGrid = this.FindControl<Button>("BtnClearGrid");
            var btnSaveMap = this.FindControl<Button>("BtnSaveMap");
            var btnLoadMap = this.FindControl<Button>("BtnLoadMap");
            var btnMonoGamePreview = this.FindControl<Button>("BtnMonoGamePreview");
            var btnReloadGameData = this.FindControl<Button>("BtnReloadGameData");
            var btnCaptureTelemetry = this.FindControl<Button>("BtnCaptureTelemetry");
            var btnSpriteEditor = this.FindControl<Button>("BtnSpriteEditor");
            var btnTileSelect = this.FindControl<Button>("BtnTileSelect");
            var btnTileFill = this.FindControl<Button>("BtnTileFill");
            var btnAddCharacter = this.FindControl<Button>("BtnAddCharacter");
            var btnEditCharacter = this.FindControl<Button>("BtnEditCharacter");
            var btnAddDoodad = this.FindControl<Button>("BtnAddDoodad");
            var btnEditDoodad = this.FindControl<Button>("BtnEditDoodad");
            var btnAddTrigger = this.FindControl<Button>("BtnAddTrigger");
            var btnRemoveTrigger = this.FindControl<Button>("BtnRemoveTrigger");

            if (btnLoadSpriteSheet != null)
                btnLoadSpriteSheet.Click += BtnLoadSpriteSheet_Click;
            if (btnSplitSheet != null)
                btnSplitSheet.Click += BtnSplitSheet_Click;
            if (btnLoadTiles != null)
                btnLoadTiles.Click += BtnLoadTiles_Click;
            if (btnClearGrid != null)
                btnClearGrid.Click += BtnClearGrid_Click;
            if (btnSaveMap != null)
                btnSaveMap.Click += BtnSaveMap_Click;
            if (btnLoadMap != null)
                btnLoadMap.Click += BtnLoadMap_Click;
            if (btnMonoGamePreview != null)
                btnMonoGamePreview.Click += BtnMonoGamePreview_Click;
            if (btnReloadGameData != null)
                btnReloadGameData.Click += BtnReloadGameData_Click;
            if (btnCaptureTelemetry != null)
                btnCaptureTelemetry.Click += BtnCaptureTelemetry_Click;
            if (diagnosticsToggleButton != null)
                diagnosticsToggleButton.Click += (_, _) => ToggleDiagnosticsPanel();
            if (btnCloseDiagnostics != null)
                btnCloseDiagnostics.Click += (_, _) => ToggleDiagnosticsPanel(false);
            if (btnSpriteEditor != null)
                btnSpriteEditor.Click += BtnSpriteEditor_Click;
            if (btnTileSelect != null)
                btnTileSelect.Click += BtnTileSelect_Click;
            if (btnTileFill != null)
                btnTileFill.Click += BtnTileFill_Click;
            if (btnAddCharacter != null)
                btnAddCharacter.Click += BtnAddCharacter_Click;
            if (btnEditCharacter != null)
                btnEditCharacter.Click += BtnEditCharacter_Click;
            if (btnAddDoodad != null)
                btnAddDoodad.Click += BtnAddDoodad_Click;
            if (btnEditDoodad != null)
                btnEditDoodad.Click += BtnEditDoodad_Click;
            if (btnAddTrigger != null)
                btnAddTrigger.Click += BtnAddTrigger_Click;
            if (btnRemoveTrigger != null)
                btnRemoveTrigger.Click += BtnRemoveTrigger_Click;
            if (btnAddLayer != null)
                btnAddLayer.Click += (_, _) => AddLayerAfterActive();
            if (btnRemoveLayer != null)
                btnRemoveLayer.Click += (_, _) => RemoveActiveLayer();
            if (btnLayerUp != null)
                btnLayerUp.Click += (_, _) => MoveActiveLayer(1);
            if (btnLayerDown != null)
                btnLayerDown.Click += (_, _) => MoveActiveLayer(-1);
            
            if (cmbGridSize != null)
                cmbGridSize.SelectionChanged += CmbGridSize_SelectionChanged;

            if (numBrushSize != null)
                numBrushSize.ValueChanged += (_, _) => brushSize = (int)(numBrushSize.Value ?? 1);

            if (btnZoomOut != null)
                btnZoomOut.Click += (_, _) => AdjustZoom(0.9);
            if (btnZoomIn != null)
                btnZoomIn.Click += (_, _) => AdjustZoom(1.1);
            if (btnZoomReset != null)
                btnZoomReset.Click += (_, _) => ResetZoom();

            if (gridVisibilityCheck != null)
                gridVisibilityCheck.IsCheckedChanged += (_, _) => RenderMap();

            if (layerList != null)
            {
                layerList.ItemsSource = layers;
                layerList.DisplayMemberBinding = new Binding(nameof(LayerState.Name));
                layerList.SelectionChanged += LayerList_SelectionChanged;
                layerList.SelectedIndex = activeLayerIndex;
                UpdateLayerSelectionUI(scrollIntoView: false);
            }

            if (dialogueList != null)
            {
                dialogueList.ItemsSource = dialogueSummaries;
                dialogueList.DisplayMemberBinding = new Binding(nameof(GameDataEntrySummary.Summary));
            }

            if (questList != null)
            {
                questList.ItemsSource = questSummaries;
                questList.DisplayMemberBinding = new Binding(nameof(GameDataEntrySummary.Summary));
            }

            if (cutsceneList != null)
            {
                cutsceneList.ItemsSource = cutsceneSummaries;
                cutsceneList.DisplayMemberBinding = new Binding(nameof(GameDataEntrySummary.Summary));
            }

            if (historyList != null)
                historyList.ItemsSource = historyEntries;

            if (telemetrySessionsList != null)
                telemetrySessionsList.ItemsSource = telemetrySessions;

            if (characterList != null)
            {
                characterList.ItemsSource = characters;
                characterList.SelectionChanged += CharacterList_SelectionChanged;
            }

            if (doodadList != null)
            {
                doodadList.ItemsSource = doodads;
                doodadList.SelectionChanged += DoodadList_SelectionChanged;
            }

            if (triggerList != null)
            {
                triggerList.ItemsSource = triggers;
            }

            if (gameDataStatusDismissButton != null)
                gameDataStatusDismissButton.Click += GameDataStatusDismissButton_Click;

            if (assetTabs != null)
                assetTabs.SelectionChanged += AssetTabs_SelectionChanged;
            SelectPrimaryTool(EditorTool.Brush);
            UpdateStatusTool();
            UpdateStatusZoom();
            UpdateStatusCursor(null);
            UpdateStatusTileInfo(-1, -1);
            RefreshPropertiesPanel();

            SyncMapFromEditorState();
            RenderMap();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (bufferedLogHandler != null)
            {
                var sink = LogManager.GetBufferedSink();
                if (sink != null)
                    sink.LogReceived -= bufferedLogHandler;
                bufferedLogHandler = null;
            }

            resourcePumpTimer.Stop();
            resourcePumpTimer.Tick -= resourcePumpHandler;

            StopTabLayoutProfiling();

            // Detach runtime preview host handlers now to avoid duplicate subscriptions
            // if the preview is restarted during the window lifetime. Then stop the
            // runtime preview and dispose resources.
            try
            {
                if (runtimePreviewHost != null)
                {
                    runtimePreviewHost.PointerPressed -= RuntimePreviewHost_PointerPressed;
                    runtimePreviewHost.AttachedToVisualTree -= RuntimePreviewHost_AttachedToVisualTree;
                    runtimePreviewHost.DetachedFromVisualTree -= RuntimePreviewHost_DetachedFromVisualTree;
                }
            }
            catch { /* best-effort */ }

            // Centralized shutdown of the runtime preview to avoid races when the
            // host or game is torn down. This will null out the host.Game, call
            // Exit/Dispose on the EditorGame instance if present, and clear the
            // cached preview reference.
            StopRuntimePreview();

            resourceManager.Dispose();
            scheduler.Dispose();
            jobSystem.Dispose();

            base.OnClosed(e);
        }

        private void EnsureGameDataLoaded()
        {
            if (gameDataLoaded)
                return;

            if (gameDataPreviewService.IsLoading)
            {
                LogPreviewStatus("Game data load already in progress; skipping additional request.");
                return;
            }

            ReloadGameData();
        }

        private void ViewportTabControl_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (viewportTabControl != null)
                BeginTabLayoutProfiling(viewportTabControl.SelectedItem as TabItem);

            if (viewportTabControl?.SelectedIndex == 1 && runtimePreviewHost != null)
            {
                runtimePreviewHost.Focus();
                LogPreviewStatus("Preview tab selected; focus requested for runtime host.");
            }
        }

        private void BeginTabLayoutProfiling(TabItem? activeTab)
        {
            StopTabLayoutProfiling();

            if (viewportTabControl == null)
                return;

            tabSwitchProfileTarget = activeTab?.Header?.ToString() ?? $"Index {viewportTabControl.SelectedIndex}";
            tabSwitchStopwatch = Stopwatch.StartNew();
            tabSwitchLayoutPassCount = 0;
            viewportTabControl.LayoutUpdated += ViewportTabControl_LayoutUpdated;
            logger.Debug($"[LayoutProfile] Measuring layout for tab '{tabSwitchProfileTarget}'.");
        }

        private void StopTabLayoutProfiling()
        {
            if (viewportTabControl != null)
                viewportTabControl.LayoutUpdated -= ViewportTabControl_LayoutUpdated;

            tabSwitchStopwatch = null;
            tabSwitchLayoutPassCount = 0;
            tabSwitchProfileTarget = null;
        }

        private void ViewportTabControl_LayoutUpdated(object? sender, EventArgs e)
        {
            if (tabSwitchStopwatch == null)
                return;

            tabSwitchLayoutPassCount++;
            var elapsed = tabSwitchStopwatch.Elapsed.TotalMilliseconds;
            logger.Debug($"[LayoutProfile] Tab '{tabSwitchProfileTarget ?? "Unknown"}' layout pass {tabSwitchLayoutPassCount} @ {elapsed:F2} ms.");

            if (tabSwitchLayoutPassCount >= 1)
            {
                tabSwitchStopwatch.Stop();
                logger.Debug($"[LayoutProfile] Tab '{tabSwitchProfileTarget ?? "Unknown"}' layout complete in {elapsed:F2} ms across {tabSwitchLayoutPassCount} pass{(tabSwitchLayoutPassCount == 1 ? string.Empty : "es") }.");
                StopTabLayoutProfiling();
            }
        }

        private void RuntimePreviewHost_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            runtimePreviewHost?.Focus();
            LogPreviewStatus("Pointer pressed on runtime preview host; focus requested.");
        }

        private void RuntimePreviewHost_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            LogPreviewStatus("RuntimePreviewHost attached to visual tree; scheduling preview initialization.");
            Dispatcher.UIThread.Post(InitializeRuntimePreview);
        }

        private void RuntimePreviewHost_DetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            runtimePreviewInitialized = false;
            LogPreviewStatus("RuntimePreviewHost detached from visual tree; preview flagged as uninitialized.");
        }

        private void ReloadGameData()
        {
            if (gameDataPreviewService.IsLoading)
            {
                PushHistory("Game data load already in progress.");
                UpdateGameDataStatus("Game data reload already in progress...");
                return;
            }

            gameDataLoaded = false;
            PushHistory("Loading game data...");
            UpdateGameDataStatus("Loading game data...");
            ClearGameDataSummaries();

            gameDataPreviewService.ReloadAsync(
                report =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        gameDataLoaded = true;
                        var statusMessage = $"Loaded {report.DialogueCount} dialogue(s), {report.QuestCount} quest(s), {report.CutsceneCount} cutscene(s).";
                        if (report.HasErrors)
                            statusMessage += $" Encountered {report.Errors.Count} issue(s). See history for details.";

                        PushHistory($"Game data ready: {report.DialogueCount} dialogue(s), {report.QuestCount} quest(s), {report.CutsceneCount} cutscene(s).");
                        if (report.HasErrors)
                        {
                            foreach (var error in report.Errors)
                                PushHistory($"Game data issue for '{error.FilePath}': {error.Message}");
                        }

                        UpdateGameDataStatus(statusMessage);
                        ApplyGameDataSummaries();
                    });
                },
                ex =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        gameDataLoaded = false;
                        var errorMessage = $"Failed to load game data: {ex.Message}";
                        PushHistory(errorMessage);
                        UpdateGameDataStatus(errorMessage);
                        ClearGameDataSummaries();
                    });
                });
        }

        private void UpdateGameDataStatus(string message)
        {
            if (gameDataStatusText == null)
                return;

            void Apply()
            {
                var normalized = message ?? string.Empty;
                if (!string.Equals(gameDataStatusText.Text, normalized, StringComparison.Ordinal))
                    gameDataStatusText.Text = normalized;

                if (gameDataStatusDismissButton != null)
                    gameDataStatusDismissButton.IsVisible = !string.IsNullOrWhiteSpace(normalized);
            }

            if (Dispatcher.UIThread.CheckAccess())
            {
                Apply();
            }
            else
            {
                Dispatcher.UIThread.Post(Apply);
            }
        }

        private void GameDataStatusDismissButton_Click(object? sender, RoutedEventArgs e)
        {
            UpdateGameDataStatus(string.Empty);
        }

        private void ApplyGameDataSummaries()
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(ApplyGameDataSummaries);
                return;
            }

            ResetCollection(dialogueSummaries, gameDataPreviewService.GetDialogueSummaries());
            ResetCollection(questSummaries, gameDataPreviewService.GetQuestSummaries());
            ResetCollection(cutsceneSummaries, gameDataPreviewService.GetCutsceneSummaries());
        }

        private void ClearGameDataSummaries()
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(ClearGameDataSummaries);
                return;
            }

            dialogueSummaries.Clear();
            questSummaries.Clear();
            cutsceneSummaries.Clear();
        }

        private static void ResetCollection<T>(ObservableCollection<T> target, IEnumerable<T> values)
        {
            var snapshot = values as IList<T> ?? values.ToList();

            if (target.Count == snapshot.Count)
            {
                bool differs = false;
                for (int i = 0; i < target.Count; i++)
                {
                    if (!EqualityComparer<T>.Default.Equals(target[i], snapshot[i]))
                    {
                        differs = true;
                        break;
                    }
                }

                if (!differs)
                    return;
            }

            target.Clear();
            foreach (var value in snapshot)
                target.Add(value);
        }

        private void InitializeRuntimePreview()
        {
            LogPreviewStatus("Entering InitializeRuntimePreview method.");

            if (runtimePreviewInitialized)
            {
                LogPreviewStatus("InitializeRuntimePreview skipped; preview already initialized.");
                UpdateRuntimePreviewStatus(string.Empty, false, false);
                return;
            }

            if (runtimePreviewHost == null)
            {
                LogPreviewStatus("InitializeRuntimePreview aborted; runtimePreviewHost was null.");
                UpdateRuntimePreviewStatus("Preview host control is unavailable.", true, true);
                return;
            }

            LogPreviewStatus($"runtimePreviewHost.GetVisualRoot() state: {(runtimePreviewHost.GetVisualRoot() == null ? "null" : "ready")}.");

            if (runtimePreviewHost.GetVisualRoot() == null)
            {
                LogPreviewStatus("InitializeRuntimePreview deferred; visual root not ready yet.");
                UpdateRuntimePreviewStatus("Preview surface not ready yet...", true, false);
                return;
            }

            LogPreviewStatus("Checking for existing cached EditorGame instance.");

            EditorGame? existingGame;
            lock (previewGameLock)
            {
                existingGame = previewGame;
            }

            if (existingGame != null)
            {
                runtimePreviewHost.Game = existingGame;
                runtimePreviewInitialized = true;
                LogPreviewStatus("Initialized runtime preview with cached EditorGame instance.");
                UpdateRuntimePreviewStatus(string.Empty, false, false);
                return;
            }

            LogPreviewStatus("No cached EditorGame instance found; proceeding with initialization.");
            UpdateRuntimePreviewStatus("Initializing runtime preview...", true, false);

            EnsureGameDataLoaded();
            SyncMapFromEditorState(suppressPreviewUpdate: true);

            Map snapshot;
            usingFallbackPreviewMap = false;
            var expectingFallback = ShouldUseFallbackPreviewMap();

            Map? fallback = null;
            if (expectingFallback)
            {
                TryLoadFallbackPreviewMap(out fallback);
            }

            if (fallback != null)
            {
                snapshot = fallback;
                usingFallbackPreviewMap = true;
                LogPreviewStatus($"Runtime preview using fallback map '{ResolveDefaultPreviewMapPath()}'.");
                UpdateRuntimePreviewStatus(string.Empty, false, false);
            }
            else
            {
                snapshot = map.Clone();
                if (expectingFallback)
                {
                    var expectedPath = ResolveDefaultPreviewMapPath();
                    UpdateRuntimePreviewStatus($"No preview map available.\nPlace a JSON map at:\n{expectedPath}\nor choose one now.", true, true, showLoadMapButton: true, hintMessage: "Would you like to load a map now?");
                }
                else
                {
                    UpdateRuntimePreviewStatus(string.Empty, false, false);
                }
            }

            EditorGame editorGame;
            try
            {
                editorGame = CreateEditorPreviewGame(snapshot);
            }
            catch (Exception ex)
            {
                LogPreviewStatus($"Failed to create EditorGame: {ex.Message}");
                UpdateRuntimePreviewStatus($"Preview failed to initialize.\n{ex.Message}", true, true);
                return;
            }
            // If we are creating a fresh EditorGame, ensure any previous preview is
            // cleanly stopped before replacing it. This avoids races where two
            // EditorGame instances could be assigned to the host concurrently.
            StopRuntimePreview();

            lock (previewGameLock)
            {
                previewGame = editorGame;
            }

            try
            {
                runtimePreviewHost.Game = editorGame;
                runtimePreviewInitialized = true;
                UpdateRuntimePreviewStatus(string.Empty, false, false);
                LogPreviewStatus("Runtime preview initialized with new EditorGame instance.");
                try
                {
                    // Give the runtime host immediate focus so input is directed to the preview.
                    runtimePreviewHost?.Focus();
                }
                catch
                {
                    // best-effort
                }
            }
            catch (Exception ex)
            {
                LogPreviewStatus($"Failed to assign EditorGame to host: {ex.Message}");
                UpdateRuntimePreviewStatus($"Unable to start preview.\n{ex.Message}", true, true);
            }
        }


        private EditorGame CreateEditorPreviewGame(Map mapSnapshot)
        {
            var editorGame = new EditorGame(mapSnapshot, resolverOverride: null, schedulerOverride: scheduler, resourceManagerOverride: resourceManager, jobSystemOverride: jobSystem);
            LogPreviewStatus("EditorGame instance created for runtime preview.");
            editorGame.TriggerActivated += (trigger, entity) =>
            {
                var rawName = trigger.Name ?? string.Empty;
                var triggerName = string.IsNullOrWhiteSpace(rawName) ? "UnnamedTrigger" : rawName.Trim();
                var message = $"[Preview] Trigger '{triggerName}' fired by '{entity.Name}'.";

                if (gameDataPreviewService.TryDescribeTrigger(rawName, out var description))
                {
                    message += " " + description;
                }
                else
                {
                    message += " No game data preview available.";
                }

                logger.Info(message);
                Dispatcher.UIThread.Post(() => PushHistory(message));
            };

            return editorGame;
        }

        private static void LogPreviewStatus(string message)
        {
            LogManager.GetLogger("RuntimePreview").Debug(message);
        }

        private void UpdateRuntimePreviewStatus(string message, bool isVisible, bool isError, bool showLoadMapButton = false, string? hintMessage = null)
        {
            void Apply()
            {
                if (runtimePreviewStatusOverlay == null || runtimePreviewStatusText == null)
                    return;

                runtimePreviewStatusOverlay.IsVisible = isVisible;
                var hasMessage = !string.IsNullOrWhiteSpace(message);
                var allowDismiss = isVisible && (hasMessage || isError || showLoadMapButton);

                runtimePreviewStatusOverlay.IsHitTestVisible = allowDismiss;
                runtimePreviewStatusText.Text = isVisible ? message : string.Empty;
                runtimePreviewStatusText.Foreground = isError ? Brushes.Tomato : Brushes.WhiteSmoke;
                if (runtimePreviewStatusHintText != null)
                {
                    if (!string.IsNullOrWhiteSpace(hintMessage))
                    {
                        runtimePreviewStatusHintText.Text = hintMessage;
                        runtimePreviewStatusHintText.IsVisible = isVisible;
                    }
                    else if (allowDismiss && (isError || showLoadMapButton))
                    {
                        runtimePreviewStatusHintText.Text = RuntimePreviewDefaultHint;
                        runtimePreviewStatusHintText.IsVisible = true;
                    }
                    else
                    {
                        runtimePreviewStatusHintText.IsVisible = false;
                    }
                }

                if (runtimePreviewStatusDismissButton != null)
                    runtimePreviewStatusDismissButton.IsVisible = allowDismiss;

                if (runtimePreviewStatusLoadMapButton != null)
                    runtimePreviewStatusLoadMapButton.IsVisible = isVisible && showLoadMapButton;
            }

            if (Dispatcher.UIThread.CheckAccess())
            {
                Apply();
            }
            else
            {
                Dispatcher.UIThread.Post(Apply);
            }
        }

        private void StopRuntimePreview()
        {
            // Safely stop and dispose the running preview game, and clear the host.Game
            EditorGame? game;
            lock (previewGameLock)
            {
                game = previewGame;
            }

            try
            {
                if (game != null)
                {
                    LogPreviewStatus("Stopping runtime preview: calling Exit on EditorGame.");
                    try { game.Exit(); } catch { /* best-effort */ }
                }
            }
            catch (Exception ex)
            {
                LogPreviewStatus($"Exception while exiting preview game: {ex.Message}");
            }

            try
            {
                if (runtimePreviewHost != null)
                {
                    // Clear the Game assignment. Do not detach event handlers here; leave
                    // handler lifecycle to the window Close path to avoid re-attaching on
                    // preview restarts and causing duplicate subscriptions.
                    runtimePreviewHost.Game = null;
                }
            }
            catch (Exception ex)
            {
                LogPreviewStatus($"Exception while clearing preview host: {ex.Message}");
            }

            runtimePreviewInitialized = false;

            try
            {
                if (game != null)
                {
                    LogPreviewStatus("Disposing EditorGame instance.");
                    try { game.Dispose(); } catch { /* best-effort */ }
                }
            }
            catch (Exception ex)
            {
                LogPreviewStatus($"Exception while disposing preview game: {ex.Message}");
            }

            lock (previewGameLock)
            {
                previewGame = null;
            }
        }

        // Public wrappers so external UI services can control the preview without
        // accessing internal/private methods directly.
        public void StartPreview(string? mapSerialized = null)
        {
            // If no serialized map is provided, fall back to the existing
            // initialization path which uses the editor's current `map` or
            // the configured fallback preview map.
            if (string.IsNullOrWhiteSpace(mapSerialized))
            {
                if (Dispatcher.UIThread.CheckAccess())
                    InitializeRuntimePreview();
                else
                    Dispatcher.UIThread.Post(InitializeRuntimePreview);

                return;
            }

            void StartWithSnapshot()
            {
                // Attempt to interpret the provided string as either a file
                // path or raw JSON content. If we cannot produce a Map from
                // the input, fall back to the normal initialization.
                Map? snapshot = null;

                try
                {
                    if (File.Exists(mapSerialized))
                    {
                        snapshot = Map.LoadFromJson(mapSerialized);
                    }
                    else
                    {
                        var trimmed = mapSerialized.TrimStart();
                        if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
                        {
                            // Parse JSON in-memory without touching the filesystem
                            snapshot = Map.LoadFromJsonString(mapSerialized, AppContext.BaseDirectory);
                        }
                        else
                        {
                            // Not a path and not JSON; fall back to normal init
                            InitializeRuntimePreview();
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogPreviewStatus($"Failed to load preview map: {ex.Message}");
                    // Fall back to the normal initialization path which will
                    // display status overlays as appropriate.
                    InitializeRuntimePreview();
                    return;
                }

                if (snapshot == null)
                {
                    InitializeRuntimePreview();
                    return;
                }

                // Ensure a clean preview state, create a fresh EditorGame for
                // the provided snapshot and attach it to the preview host.
                StopRuntimePreview();

                EditorGame editorGame;
                try
                {
                    editorGame = CreateEditorPreviewGame(snapshot);
                }
                catch (Exception ex)
                {
                    LogPreviewStatus($"Failed to create EditorGame for serialized map: {ex.Message}");
                    UpdateRuntimePreviewStatus($"Preview failed to initialize.\n{ex.Message}", true, true);
                    return;
                }

                lock (previewGameLock)
                {
                    previewGame = editorGame;
                }

                try
                {
                    if (runtimePreviewHost != null)
                        runtimePreviewHost.Game = editorGame;
                    runtimePreviewInitialized = true;
                    try { runtimePreviewHost?.Focus(); } catch { }
                }
                catch (Exception ex)
                {
                    LogPreviewStatus($"Failed to assign EditorGame to host: {ex.Message}");
                    UpdateRuntimePreviewStatus($"Unable to start preview.\n{ex.Message}", true, true);
                }
                
            }

            if (Dispatcher.UIThread.CheckAccess())
                StartWithSnapshot();
            else
                Dispatcher.UIThread.Post(StartWithSnapshot);
        }

        public void StopPreview()
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                StopRuntimePreview();
            }
            else
            {
                Dispatcher.UIThread.Post(StopRuntimePreview);
            }
        }

        public bool IsPreviewRunning => runtimePreviewInitialized;

        private void RuntimePreviewStatusDismissButton_Click(object? sender, RoutedEventArgs e)
        {
            UpdateRuntimePreviewStatus(string.Empty, false, false);
            runtimePreviewHost?.Focus();
        }

        private void RuntimePreviewStatusLoadMapButton_Click(object? sender, RoutedEventArgs e)
        {
            BtnLoadMap_Click(sender, e);
            UpdateRuntimePreviewStatus(string.Empty, false, false);
            runtimePreviewHost?.Focus();
        }

        private void HookToolToggle(ToggleButton? button, EditorTool tool)
        {
            if (button == null)
                return;

            button.IsCheckedChanged += (_, _) =>
            {
                if (button.IsChecked == true && !suppressToolToggle)
                {
                    SelectPrimaryTool(tool);
                }
                else if (button.IsChecked != true && !suppressToolToggle && currentTool == tool)
                {
                    suppressToolToggle = true;
                    button.IsChecked = true;
                    suppressToolToggle = false;
                }
            };
        }

        private void SelectPrimaryTool(EditorTool tool)
        {
            if (currentTool == tool && suppressToolToggle)
                return;

            currentTool = tool;
            suppressToolToggle = true;
            toolBrush?.SetValue(ToggleButton.IsCheckedProperty, tool == EditorTool.Brush);
            toolEraser?.SetValue(ToggleButton.IsCheckedProperty, tool == EditorTool.Eraser);
            toolFill?.SetValue(ToggleButton.IsCheckedProperty, tool == EditorTool.Fill);
            toolRect?.SetValue(ToggleButton.IsCheckedProperty, tool == EditorTool.Rect);
            toolLine?.SetValue(ToggleButton.IsCheckedProperty, tool == EditorTool.Line);
            toolPicker?.SetValue(ToggleButton.IsCheckedProperty, tool == EditorTool.Picker);
            toolSelect?.SetValue(ToggleButton.IsCheckedProperty, tool == EditorTool.Select);
            toolStamp?.SetValue(ToggleButton.IsCheckedProperty, tool == EditorTool.Stamp);
            toolCollision?.SetValue(ToggleButton.IsCheckedProperty, tool == EditorTool.Collision);
            suppressToolToggle = false;
            UpdateStatusTool();
        }

        private void UpdateStatusTool()
        {
            if (statusToolText == null)
                return;

            string modeName = currentMode switch
            {
                EditorMode.Characters => "Characters",
                EditorMode.Doodads => "Doodads",
                EditorMode.Triggers => "Triggers",
                _ => "Tiles"
            };

            string toolName = currentTool switch
            {
                EditorTool.Brush => "Brush",
                EditorTool.Eraser => "Eraser",
                EditorTool.Fill => "Fill",
                EditorTool.Rect => "Rect",
                EditorTool.Line => "Line",
                EditorTool.Picker => "Picker",
                EditorTool.Select => "Select",
                EditorTool.Stamp => "Stamp",
                EditorTool.Collision => "Collision",
                _ => "Unknown"
            };

            statusToolText.Text = $"Tool: {toolName} ({modeName})";
        }

        private void UpdateStatusZoom()
        {
            if (statusZoomText == null)
                return;

            statusZoomText.Text = $"Zoom: {Math.Round(zoomLevel * 100)}%";
        }

        private void UpdateStatusCursor(Point? mapPoint)
        {
            if (statusCoordText == null)
                return;

            if (mapPoint.HasValue)
            {
                var gridCoords = ComputeGridCoordinates(mapPoint.Value);
                if (gridCoords.HasValue)
                {
                    statusCoordText.Text = $"Cursor: {gridCoords.Value.x},{gridCoords.Value.y}";
                    UpdateStatusTileInfo(gridCoords.Value.x, gridCoords.Value.y);
                    return;
                }
            }

            statusCoordText.Text = "Cursor: --";
            UpdateStatusTileInfo(-1, -1);
        }

        private void UpdateStatusTileInfo(int gridX, int gridY)
        {
            if (statusTileText == null)
                return;

            if (!InBounds(gridX, gridY))
            {
                statusTileText.Text = "Tile: --";
                return;
            }

            var topTile = GetTopmostTile(gridX, gridY, out var owningLayer);
            string tileState = topTile != null ? $"Tile ({owningLayer?.Name ?? "Layer"})" : "Empty";
            var character = GetCharacterAt(gridX, gridY);
            var doodad = GetDoodadAt(gridX, gridY);
            var trigger = GetTriggerAt(gridX, gridY);

            if (character != null)
                tileState += $", Char: {character.Name}";
            if (doodad != null)
                tileState += $", Doodad: {doodad.Type}";
            if (trigger != null)
                tileState += $", Trigger: {trigger.Name}";

            statusTileText.Text = $"Tile: {gridX},{gridY} ({tileState})";
        }

        private void Layers_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshPropertiesPanel();
        }

        private void RefreshPropertiesPanel()
        {
            if (propertiesPanel == null)
                return;

            propertiesPanel.Children.Clear();

            if (layers.Count == 0)
                return;

            var layer = ActiveLayer;

            propertiesPanel.Children.Add(new TextBlock
            {
                Text = "Active Layer",
                FontSize = 13,
                FontWeight = FontWeight.SemiBold
            });

            propertiesPanel.Children.Add(new TextBlock
            {
                Text = $"Id: {layer.Id}",
                FontSize = 11,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 2, 0, 0)
            });

            var sizeText = $"Size: {layer.Tiles.GetLength(0)} Ã— {layer.Tiles.GetLength(1)}";
            propertiesPanel.Children.Add(new TextBlock
            {
                Text = sizeText,
                FontSize = 11,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 2, 0, 0)
            });

            propertiesPanel.Children.Add(new TextBlock
            {
                Text = "Name",
                Margin = new Thickness(0, 10, 0, 0),
                FontWeight = FontWeight.Medium
            });

            var nameBox = new TextBox
            {
                Text = layer.Name
            };

            void CommitLayerName()
            {
                var proposed = string.IsNullOrWhiteSpace(nameBox.Text)
                    ? "Layer"
                    : nameBox.Text!.Trim();

                if (layer.Name == proposed)
                    return;

                var previous = layer.Name;
                layer.Name = proposed;
                PushHistory($"Renamed layer '{previous}' to '{proposed}'");
            }

            nameBox.LostFocus += (_, _) => CommitLayerName();
            nameBox.KeyDown += (sender, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    CommitLayerName();
                    e.Handled = true;
                }
            };

            propertiesPanel.Children.Add(nameBox);

            var visibleCheck = new CheckBox
            {
                Content = "Visible",
                IsChecked = layer.IsVisible,
                Margin = new Thickness(0, 10, 0, 0)
            };

            visibleCheck.IsCheckedChanged += (_, _) =>
            {
                var desired = visibleCheck.IsChecked != false;
                if (layer.IsVisible == desired)
                    return;

                layer.IsVisible = desired;
                RenderMap();
            };

            propertiesPanel.Children.Add(visibleCheck);

            var opacityText = new TextBlock
            {
                Text = $"Opacity: {Math.Round(layer.Opacity * 100)}%",
                Margin = new Thickness(0, 10, 0, 0)
            };

            propertiesPanel.Children.Add(opacityText);

            var opacitySlider = new Slider
            {
                Minimum = 0,
                Maximum = 1,
                Value = layer.Opacity,
                TickFrequency = 0.05,
                Margin = new Thickness(0, 4, 0, 0)
            };

            opacitySlider.PropertyChanged += (_, args) =>
            {
                if (args.Property != RangeBase.ValueProperty)
                    return;

                var clamped = Math.Clamp(opacitySlider.Value, 0.0, 1.0);
                if (Math.Abs(layer.Opacity - clamped) <= double.Epsilon)
                    return;

                layer.Opacity = clamped;
                opacityText.Text = $"Opacity: {Math.Round(clamped * 100)}%";
                RenderMap();
            };

            propertiesPanel.Children.Add(opacitySlider);
        }

        private void CharacterList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (suppressCharacterSelection)
                return;

            if (characterList?.SelectedItem is Character character)
            {
                selectedCharacter = character;
                if (currentMode != EditorMode.Characters)
                    SwitchToCharactersMode(sender, new RoutedEventArgs());
            }
            else
            {
                selectedCharacter = null;
            }
        }

        private void DoodadList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (suppressDoodadSelection)
                return;

            if (doodadList?.SelectedItem is Doodad doodad)
            {
                selectedDoodad = doodad;
                if (currentMode != EditorMode.Doodads)
                    SwitchToDoodadsMode(sender, new RoutedEventArgs());
            }
            else
            {
                selectedDoodad = null;
            }
        }

        private void TriggerList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (suppressTriggerSelection)
                return;

            selectedTrigger = triggerList?.SelectedItem as BehaviorTrigger;
        }

        private void AssetTabs_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (assetTabs?.SelectedItem is not TabItem tab)
                return;

            var headerText = tab.Header?.ToString();
            if (string.IsNullOrWhiteSpace(headerText))
                return;

            if (headerText.Contains("Tiles", StringComparison.OrdinalIgnoreCase) && currentMode != EditorMode.Tiles)
            {
                SwitchToTilesMode(sender, new RoutedEventArgs());
            }
            else if (headerText.Contains("Characters", StringComparison.OrdinalIgnoreCase) && currentMode != EditorMode.Characters)
            {
                SwitchToCharactersMode(sender, new RoutedEventArgs());
            }
            else if (headerText.Contains("Doodads", StringComparison.OrdinalIgnoreCase) && currentMode != EditorMode.Doodads)
            {
                SwitchToDoodadsMode(sender, new RoutedEventArgs());
            }
            else if (headerText.Contains("Triggers", StringComparison.OrdinalIgnoreCase) && currentMode != EditorMode.Triggers)
            {
                SwitchToTriggersMode(sender, new RoutedEventArgs());
            }
        }

        private void SelectCharacterInList(Character? character)
        {
            if (characterList == null)
                return;

            suppressCharacterSelection = true;
            characterList.SelectedItem = character;
            if (character != null)
                characterList.ScrollIntoView(character);
            suppressCharacterSelection = false;
        }

        private void SelectDoodadInList(Doodad? doodad)
        {
            if (doodadList == null)
                return;

            suppressDoodadSelection = true;
            doodadList.SelectedItem = doodad;
            if (doodad != null)
                doodadList.ScrollIntoView(doodad);
            suppressDoodadSelection = false;
        }

        private void SelectTriggerInList(BehaviorTrigger? trigger)
        {
            if (triggerList == null)
                return;

            suppressTriggerSelection = true;
            triggerList.SelectedItem = trigger;
            if (trigger != null)
                triggerList.ScrollIntoView(trigger);
            suppressTriggerSelection = false;
        }

        private void AdjustZoom(double factor)
        {
            SetZoom(zoomLevel * factor);
        }

        private void ResetZoom()
        {
            SetZoom(1.0);
        }

        private void SetZoom(double newZoom)
        {
            zoomLevel = Math.Clamp(newZoom, MinZoom, MaxZoom);
            ApplyZoom();
        }

        private void ApplyZoom()
        {
            if (mapCanvas != null)
            {
                mapCanvas.RenderTransform = new ScaleTransform(zoomLevel, zoomLevel);
            }

            UpdateStatusZoom();
        }

        private (int x, int y)? ComputeGridCoordinates(Point mapPoint)
        {
            var tiles = ActiveTiles;
            int width = tiles.GetLength(0);
            int height = tiles.GetLength(1);

            if (width <= 0 || height <= 0)
                return null;

            int gridX = (int)Math.Floor(mapPoint.X / currentCellSize);
            int gridY = (int)Math.Floor(mapPoint.Y / currentCellSize);

            if (gridX < 0 || gridY < 0 || gridX >= width || gridY >= height)
                return null;

            return (gridX, gridY);
        }

        private Point GetMapPoint(PointerEventArgs e)
        {
            if (mapCanvas == null)
                return default;

            var position = e.GetPosition(mapCanvas);
            return new Point(position.X / zoomLevel, position.Y / zoomLevel);
        }

        private void InitializeEditorUI()
        {
            this.KeyDown += (sender, e) =>
            {
                // If the runtime preview host has keyboard focus and there are no
                // modifier keys, don't handle the key here so the MonoGame preview
                // receives raw input (WASD, arrows, space, etc.). Allow Escape to
                // still be handled by the editor to dismiss overlays.
                try
                {
                    if (runtimePreviewHost?.IsFocused == true && e.KeyModifiers == KeyModifiers.None && e.Key != Key.Escape)
                        return;
                }
                catch
                {
                    // best-effort; fall through if any issue checking focus
                }

                if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                {
                    switch (e.Key)
                    {
                        case Key.S:
                            BtnSaveMap_Click(sender, new RoutedEventArgs());
                            e.Handled = true;
                            return;
                        case Key.O:
                            BtnLoadMap_Click(sender, new RoutedEventArgs());
                            e.Handled = true;
                            return;
                        case Key.P:
                            BtnMonoGamePreview_Click(sender, new RoutedEventArgs());
                            e.Handled = true;
                            return;
                        case Key.R:
                            BtnReloadGameData_Click(sender, new RoutedEventArgs());
                            e.Handled = true;
                            return;
                        case Key.OemPlus:
                        case Key.Add:
                            AdjustZoom(1.1);
                            e.Handled = true;
                            return;
                        case Key.OemMinus:
                        case Key.Subtract:
                            AdjustZoom(0.9);
                            e.Handled = true;
                            return;
                        case Key.D0:
                        case Key.NumPad0:
                            ResetZoom();
                            e.Handled = true;
                            return;
                    }
                }

                if (e.KeyModifiers == KeyModifiers.None)
                {
                    if (e.Key == Key.Escape && runtimePreviewStatusOverlay?.IsVisible == true)
                    {
                        UpdateRuntimePreviewStatus(string.Empty, false, false);
                        e.Handled = true;
                        return;
                    }

                    switch (e.Key)
                    {
                        case Key.T:
                            SwitchToTilesMode(sender, e);
                            break;
                        case Key.C:
                            SwitchToCharactersMode(sender, e);
                            break;
                        case Key.D:
                            SwitchToDoodadsMode(sender, e);
                            break;
                    }
                }
            };

            // Initialize property panels
            if (TilesToolsPanel != null) TilesToolsPanel.IsVisible = true;
            if (CharactersToolsPanel != null) CharactersToolsPanel.IsVisible = false;
            if (DoodadsToolsPanel != null) DoodadsToolsPanel.IsVisible = false;
        }

        private void InitializeLoggingHooks()
        {
            if (logList != null)
                logList.ItemsSource = logEntries;

            var sink = LogManager.GetBufferedSink();
            if (sink == null)
                return;

            foreach (var logEvent in sink.Snapshot)
                AppendLog(logEvent);

            bufferedLogHandler = (_, evt) => AppendLog(evt);
            sink.LogReceived += bufferedLogHandler;

            logger.Info("Editor window initialized.");
        }

        private void AppendLog(LogEvent logEvent)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => AppendLog(logEvent));
                return;
            }

            var entry = new LogEntryViewModel(logEvent);
            logEntries.Insert(0, entry);
            IncrementAlertCounters(logEvent.Level);

            while (logEntries.Count > MaxLogEntries)
            {
                var removed = logEntries[^1];
                logEntries.RemoveAt(logEntries.Count - 1);
                DecrementAlertCounters(removed.Level);
            }

            UpdateAlertStatus();
        }

        private void IncrementAlertCounters(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Warning:
                    warningCount++;
                    break;
                case LogLevel.Error:
                case LogLevel.Critical:
                    errorCount++;
                    break;
            }
        }

        private void DecrementAlertCounters(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Warning when warningCount > 0:
                    warningCount--;
                    break;
                case LogLevel.Error when errorCount > 0:
                case LogLevel.Critical when errorCount > 0:
                    errorCount--;
                    break;
            }
        }

        private void UpdateAlertStatus()
        {
            if (statusAlertText == null)
                return;

            if (warningCount <= 0 && errorCount <= 0)
            {
                statusAlertText.IsVisible = false;
                statusAlertText.Text = string.Empty;
                return;
            }

            var segments = new List<string>(2);
            if (errorCount > 0)
                segments.Add($"Errors: {errorCount}");
            if (warningCount > 0)
                segments.Add($"Warnings: {warningCount}");

            statusAlertText.Text = string.Join("   ", segments);
            statusAlertText.Foreground = errorCount > 0 ? Brushes.DarkRed : Brushes.DarkOrange;
            statusAlertText.IsVisible = true;
        }

        private void LayerList_AddMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            AddLayerAfterActive();
        }

        private void LayerList_MoveUpMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            MoveActiveLayer(1);
        }

        private void LayerList_MoveDownMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            MoveActiveLayer(-1);
        }

        private void LayerList_RemoveMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            RemoveActiveLayer();
        }

        private void LayerList_ToggleVisibilityMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            if (layerList?.SelectedItem is not LayerState layer)
                return;

            layer.IsVisible = !layer.IsVisible;
            PushHistory($"Layer '{layer.Name}' visibility set to {(layer.IsVisible ? "Visible" : "Hidden")}");
            RenderMap();
        }

        private void CharacterList_AddMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            BtnAddCharacter_Click(sender, new RoutedEventArgs());
        }

        private void CharacterList_EditMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            BtnEditCharacter_Click(sender, new RoutedEventArgs());
        }

        private void CharacterList_RemoveMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            if (characterList?.SelectedItem is not Character character)
                return;

            RemoveCharacter(character);
            PushHistory($"Removed character '{character.Name}'");
            RenderMap();
            SyncMapFromEditorState();
        }

        private void DoodadList_AddMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            BtnAddDoodad_Click(sender, new RoutedEventArgs());
        }

        private void DoodadList_EditMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            BtnEditDoodad_Click(sender, new RoutedEventArgs());
        }

        private void DoodadList_RemoveMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            if (doodadList?.SelectedItem is not Doodad doodad)
                return;

            RemoveDoodad(doodad);
            PushHistory($"Removed doodad '{doodad.Type}'");
            RenderMap();
            SyncMapFromEditorState();
        }

        private void TriggerList_AddMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            BtnAddTrigger_Click(sender, new RoutedEventArgs());
        }

        private void TriggerList_RemoveMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            if (triggerList?.SelectedItem is BehaviorTrigger trigger)
            {
                selectedTrigger = trigger;
                RemoveBehaviorTrigger(trigger);
            }
            else
            {
                BtnRemoveTrigger_Click(sender, new RoutedEventArgs());
            }
        }

        private async void BtnLoadSpriteSheet_Click(object? sender, RoutedEventArgs e)
        {
            var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Sprite Sheet",
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new FilePickerFileType("Image Files") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                }
            });

            if (result != null && result.Count > 0)
            {
                var filePath = result[0].Path.LocalPath;
                try
                {
                    spriteSheetImage = Dotgame.Avalonia.Models.AssetManager.Instance.LoadBitmap(filePath);
                    spriteSheetPath = filePath;
                    activeTilesetState?.Dispose();
                    activeTilesetState = null;
                    logger.Info($"Sprite sheet loaded: {filePath}");
                }
                catch (Exception ex)
                {
                    logger.Error($"Error loading sprite sheet from {filePath}.", ex);
                }
            }
        }

        private void BtnSplitSheet_Click(object? sender, RoutedEventArgs e)
        {
            if (spriteSheetImage == null)
            {
                logger.Error("No sprite sheet loaded.");
                return;
            }

            if (numTileWidth == null || numTileHeight == null || 
                numSpacing == null || numMargin == null || tilePalette == null)
            {
                logger.Error("Missing UI elements for splitting parameters.");
                return;
            }

            var tw = (int)(numTileWidth.Value ?? 32);
            var th = (int)(numTileHeight.Value ?? 32);
            var spacing = (int)(numSpacing.Value ?? 0);
            var margin = (int)(numMargin.Value ?? 0);

            if (tw <= 0 || th <= 0)
            {
                logger.Error("Tile width and height must be greater than zero.");
                return;
            }

            tiles.Clear();
            tilePalette.Children.Clear();
            selectedTile = null;
            selectedTileBorder = null;

            using var skBitmap = BitmapToSKBitmap(spriteSheetImage);
            int cols = (skBitmap.Width - margin * 2 + spacing) / (tw + spacing);
            int rows = (skBitmap.Height - margin * 2 + spacing) / (th + spacing);

            if (cols <= 0 || rows <= 0)
            {
                logger.Error("Invalid tile dimensions or parameters. No tiles can be generated.");
                return;
            }

            var textureKey = !string.IsNullOrWhiteSpace(spriteSheetPath) ? spriteSheetPath! : "spritesheet.png";
            var absolute = !string.IsNullOrWhiteSpace(spriteSheetPath) ? IOPath.GetFullPath(spriteSheetPath!) : null;
            var tileCount = Math.Max(0, rows * cols);
            const int firstId = 1;

            activeTilesetState?.Dispose();
            var reference = new TilesetReference(textureKey, absolute, tw, th, cols, tileCount, margin, spacing, firstId);
            activeTilesetState = TilesetState.FromSpriteSheet(reference, spriteSheetImage);

            foreach (var tileId in activeTilesetState.EnumerateTileIds())
            {
                try
                {
                    var entry = activeTilesetState.GetPaletteEntry(tileId);
                    tiles.Add(entry);
                    AddTileToPalette(entry);
                }
                catch (Exception ex)
                {
                    logger.Error($"Failed to generate tile {tileId}.", ex);
                }
            }

            if (tiles.Count > 0)
            {
                selectedTile = tiles[0];
                logger.Info($"Successfully split sprite sheet into {tiles.Count} tiles.");
            }
            else
            {
                logger.Error("No tiles generated from sprite sheet.");
            }
        }

        private async void BtnLoadTiles_Click(object? sender, RoutedEventArgs e)
        {
            var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Tile Images",
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new FilePickerFileType("Image Files") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                }
            });

            if (result != null && result.Count > 0)
            {
                activeTilesetState?.Dispose();
                activeTilesetState = null;
                spriteSheetImage = null;
                spriteSheetPath = null;
                foreach (var file in result)
                {
                    var filePath = file.Path.LocalPath;
                    var bitmap = Dotgame.Avalonia.Models.AssetManager.Instance.LoadBitmap(filePath);
                    var entry = new TileEntry(bitmap, filePath);
                    tiles.Add(entry);
                    AddTileToPalette(entry);
                }
            }
        }

        private void AddTileToPalette(TileEntry entry)
        {
            if (tilePalette == null)
                return;

            var pixelSize = entry.Bitmap.PixelSize;
            var longestEdge = Math.Max(pixelSize.Width, pixelSize.Height);

            const double minPreview = 48d;
            const double maxPreview = 128d;

            double scale = 1d;
            if (longestEdge > maxPreview && longestEdge > 0)
            {
                scale = maxPreview / longestEdge;
            }
            else if (longestEdge < minPreview && longestEdge > 0)
            {
                scale = minPreview / longestEdge;
            }

            var previewWidth = Math.Clamp(pixelSize.Width * scale, minPreview, maxPreview);
            var previewHeight = Math.Clamp(pixelSize.Height * scale, minPreview, maxPreview);

            var image = new Image
            {
                Source = entry.Bitmap,
                Stretch = Stretch.Fill,
                Width = previewWidth,
                Height = previewHeight
            };

            var border = new Border
            {
                Width = previewWidth + 8,
                Height = previewHeight + 8,
                Margin = new Thickness(4),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(6),
                Background = Brushes.Transparent,
                Child = new Grid
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Children = { image }
                }
            };

            border.PointerPressed += (_, _) =>
            {
                selectedTile = entry;
                if (selectedTileBorder != null)
                    selectedTileBorder.Background = Brushes.Transparent;
                border.Background = Brushes.LightBlue;
                selectedTileBorder = border;
            };

            tilePalette.Children.Add(border);

            if (selectedTileBorder == null)
            {
                selectedTileBorder = border;
                border.Background = Brushes.LightBlue;
                selectedTile = entry;
            }
        }

        private void LayerList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (suppressLayerSelection)
                return;

            if (layerList?.SelectedIndex is int index && index >= 0 && index < layers.Count)
            {
                SetActiveLayer(index, scrollIntoView: false);
                RenderMap();
            }
        }

        private void CmbGridSize_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (suppressGridSizeEvent)
                return;

            if (cmbGridSize?.SelectedItem is ComboBoxItem item)
            {
                gridSize = int.Parse(item.Content?.ToString() ?? "20");
                ResizeAllLayers(gridSize);
                SyncMapFromEditorState();
                RenderMap();
            }
        }

        private void BtnClearGrid_Click(object? sender, RoutedEventArgs e)
        {
            ClearActiveLayer();
        }

        private async void BtnSaveMap_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                SyncMapFromEditorState();

                var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Save Map",
                    SuggestedFileName = "mymap.json",
                    FileTypeChoices = new List<FilePickerFileType>
                    {
                        new FilePickerFileType("JSON Map") { Patterns = new[] { "*.json" } }
                    }
                });

                if (file == null)
                    return;

                var path = file.Path?.LocalPath;
                if (string.IsNullOrWhiteSpace(path))
                {
                    logger.Error("Unable to resolve the selected save path.");
                    PushHistory("Unable to resolve the selected save path.");
                    return;
                }

                System.IO.Directory.CreateDirectory(IOPath.GetDirectoryName(path) ?? ".");

                var tw = (int)(numTileWidth?.Value ?? 32);
                var th = (int)(numTileHeight?.Value ?? 32);

                var activeTiles = ActiveTiles;
                int width = activeTiles.GetLength(0);
                int height = activeTiles.GetLength(1);
                bool preferTileIds = activeTilesetState != null;

                object?[][] mapArray = new object?[height][];
                for (int y = 0; y < height; y++)
                {
                    mapArray[y] = new object?[width];
                    for (int x = 0; x < width; x++)
                        mapArray[y][x] = GetExportableTileValue(x, y, preferTileIds);
                }

                var layersPayload = layers.Select(layer =>
                {
                    var layerTiles = layer.Tiles;
                    int layerWidth = layerTiles.GetLength(0);
                    int layerHeight = layerTiles.GetLength(1);
                    object?[][] serializedTiles = new object?[layerHeight][];
                    for (int row = 0; row < layerHeight; row++)
                    {
                        serializedTiles[row] = new object?[layerWidth];
                        for (int col = 0; col < layerWidth; col++)
                            serializedTiles[row][col] = layerTiles[col, row]?.GetSerializableValue(preferTileIds);
                    }

                    return new
                    {
                        id = layer.Id,
                        name = layer.Name,
                        isVisible = layer.IsVisible,
                        opacity = layer.Opacity,
                        tiles = serializedTiles
                    };
                }).ToList();

                var mapDirectory = IOPath.GetDirectoryName(path);
                var tilesetPayload = activeTilesetState?.CreateSerializableDto(mapDirectory);

                var characterData = characters.Select(c => new
                {
                    TileX = c.TileX,
                    TileY = c.TileY,
                    Name = c.Name,
                    Class = c.Class,
                    BehaviorScript = c.BehaviorScript,
                    TriggerEvent = c.TriggerEvent,
                    Color = c.Color.ToString()
                }).ToList();

                var doodadData = doodads.Select(d => new
                {
                    TileX = d.TileX,
                    TileY = d.TileY,
                    Type = d.Type,
                    Collidable = d.Collidable,
                    Interactable = d.Interactable,
                    Animated = d.Animated,
                    Trigger = d.Trigger,
                    Color = d.Color.ToString(),
                    OnInteract = d.OnInteract
                }).ToList();

                var triggerData = triggers.Select(t => new
                {
                    TileX = t.TileX,
                    TileY = t.TileY,
                    Name = t.Name
                }).ToList();

                var mapObject = new
                {
                    cols = width,
                    rows = height,
                    tileW = tw,
                    tileH = th,
                    map = mapArray,
                    layers = layersPayload,
                    activeLayerIndex,
                    characters = characterData,
                    doodads = doodadData,
                    triggers = triggerData,
                    externalTileMapAsset = map.ExternalTileMapAsset,
                    tileset = tilesetPayload
                };

                // serialize editor passability (if present) as jagged bool[][] with [row][col]
                bool[][]? passabilityPayload = null;
                if (editorPassability != null)
                {
                    passabilityPayload = new bool[height][];
                    for (int y = 0; y < height; y++)
                    {
                        passabilityPayload[y] = new bool[width];
                        for (int x = 0; x < width; x++)
                            passabilityPayload[y][x] = editorPassability[x, y];
                    }
                }

                var fullMapObject = new
                {
                    cols = width,
                    rows = height,
                    tileW = tw,
                    tileH = th,
                    map = mapArray,
                    layers = layersPayload,
                    activeLayerIndex,
                    characters = characterData,
                    doodads = doodadData,
                    triggers = triggerData,
                    externalTileMapAsset = map.ExternalTileMapAsset,
                    tileset = tilesetPayload,
                    passability = passabilityPayload
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                System.IO.File.WriteAllText(path, JsonSerializer.Serialize(fullMapObject, options));
                logger.Info($"Map saved to {path}.");
                PushHistory($"Map saved to '{path}'.");
            }
            catch (Exception ex)
            {
                logger.Error($"Error saving map.", ex);
                PushHistory($"Error saving map: {ex.Message}");
            }
        }

        private void ApplyTilesetFromMap(Map loadedMap)
        {
            if (loadedMap == null)
                return;

            activeTilesetState?.Dispose();
            activeTilesetState = null;

            spriteSheetImage = null;
            spriteSheetPath = null;

            tiles.Clear();
            selectedTile = null;
            selectedTileBorder = null;
            if (tilePalette != null)
                tilePalette.Children.Clear();

            var reference = loadedMap.Tileset;
            if (reference == null)
                return;

            try
            {
                activeTilesetState = TilesetState.FromReference(reference);
                foreach (var tileId in activeTilesetState.EnumerateTileIds())
                {
                    try
                    {
                        var paletteEntry = activeTilesetState.GetPaletteEntry(tileId);
                        tiles.Add(paletteEntry);
                        AddTileToPalette(paletteEntry);
                    }
                    catch (Exception ex)
                    {
                        logger.Warn($"Failed to cache tileset tile {tileId}.", ex);
                    }
                }

                if (selectedTile == null && tiles.Count > 0)
                    selectedTile = tiles[0];
            }
            catch (Exception ex)
            {
                logger.Warn($"Unable to load tileset '{reference.TextureKey}'.", ex);
                PushHistory($"Unable to load tileset '{reference.TextureKey}': {ex.Message}");
                activeTilesetState?.Dispose();
                activeTilesetState = null;
            }
        }

        private async void BtnLoadMap_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var results = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Load Map",
                    AllowMultiple = false,
                    FileTypeFilter = new List<FilePickerFileType>
                    {
                        new FilePickerFileType("JSON Map") { Patterns = new[] { "*.json" } },
                        new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                    }
                });

                if (results == null || results.Count == 0)
                    return;

                var path = results[0].Path?.LocalPath;
                if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
                {
                    logger.Error("Selected map file could not be resolved.");
                    PushHistory("Selected map file could not be resolved.");
                    return;
                }

                var json = System.IO.File.ReadAllText(path);
                var serializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var fileDto = JsonSerializer.Deserialize<MapFileDto>(json, serializerOptions);

                map = Map.LoadFromJson(path);
                ApplyTilesetFromMap(map);

                var baseDirectory = IOPath.GetDirectoryName(path) ?? AppContext.BaseDirectory ?? Environment.CurrentDirectory;
                layers.Clear();

                int fallbackCols = Math.Max(1, fileDto?.Cols > 0 ? fileDto.Cols : map.Cols);
                int fallbackRows = Math.Max(1, fileDto?.Rows > 0 ? fileDto.Rows : map.Rows);
                int requestedLayerIndex = 0;

                if (fileDto?.Layers != null && fileDto.Layers.Count > 0)
                {
                    foreach (var layerDto in fileDto.Layers)
                    {
                        var buffer = CreateTileBufferFromSerialized(layerDto.Tiles, baseDirectory, fallbackCols, fallbackRows, activeTilesetState);
                        var layer = CreateLayer(layerDto.Name ?? $"Layer {layers.Count + 1}", buffer, layerDto.Id, layerDto.IsVisible, layerDto.Opacity);
                        layers.Add(layer);
                    }

                    requestedLayerIndex = Math.Clamp(fileDto.ActiveLayerIndex ?? 0, 0, layers.Count - 1);
                }
                else
                {
                    var buffer = CreateTileBuffer(fallbackCols, fallbackRows);
                    var layer = CreateLayer("Base Layer", buffer);
                    layers.Add(layer);

                    var tiles = layer.Tiles;
                    for (int y = 0; y < fallbackRows; y++)
                    {
                        for (int x = 0; x < fallbackCols; x++)
                        {
                            var stored = map.GetTileDataUrl(x, y);
                            tiles[x, y] = string.IsNullOrWhiteSpace(stored) ? null : LoadTileEntry(stored!, baseDirectory, activeTilesetState);
                        }
                    }
                }

                SetActiveLayer(requestedLayerIndex);

                var primaryLayer = ActiveLayer;
                int width = primaryLayer.Tiles.GetLength(0);
                int height = primaryLayer.Tiles.GetLength(1);
                gridSize = Math.Max(width, height);

                characters.Clear();
                foreach (var cloned in map.Characters.Select(CloneCharacterForEditor))
                    characters.Add(cloned);
                doodads.Clear();
                foreach (var cloned in map.Doodads.Select(CloneDoodadForEditor))
                    doodads.Add(cloned);
                triggers.Clear();
                foreach (var trigger in map.Triggers)
                {
                    triggers.Add(new BehaviorTrigger
                    {
                        TileX = trigger.TileX,
                        TileY = trigger.TileY,
                        Name = trigger.Name
                    });
                }
                selectedCharacter = null;
                selectedDoodad = null;
                selectedTrigger = null;
                pendingCharacterTemplate = null;
                pendingDoodadTemplate = null;
                SelectCharacterInList(null);
                SelectDoodadInList(null);
                SelectTriggerInList(null);

                if (numTileWidth != null) numTileWidth.Value = map.TileW;
                if (numTileHeight != null) numTileHeight.Value = map.TileH;

                UpdateGridSizeSelection(gridSize);
                UpdateLayerSelectionUI(scrollIntoView: true);

                SyncMapFromEditorState();
                RenderMap();
                logger.Info($"Loaded map from {path}.");
                PushHistory($"Loaded map '{IOPath.GetFileName(path)}'");
            }
            catch (Exception ex)
            {
                logger.Error("Failed to load map from selection.", ex);
                PushHistory($"Failed to load map: {ex.Message}");
            }
        }

        private async void BtnSpriteEditor_Click(object? sender, RoutedEventArgs e)
        {
            var spriteEditor = new SpriteEditorWindow();
            await spriteEditor.ShowDialog(this);
        }

        private void MapCanvas_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (mapCanvas == null)
                return;

            isMouseDown = true;
            e.Pointer.Capture(mapCanvas);
            var mapPoint = GetMapPoint(e);
            UpdateStatusCursor(mapPoint);
            var properties = e.GetCurrentPoint(mapCanvas).Properties;
            // Start a composite change when beginning a collision edit
            if (currentMode == EditorMode.Tiles && currentTool == EditorTool.Collision)
            {
                history ??= new DotGame.Core.CombinedHistoryService();
                history.BeginPassabilityComposite();
            }
            // Start a composite change for tile painting actions (brush/eraser/fill/rect/line/stamp)
            if (currentMode == EditorMode.Tiles && currentTool != EditorTool.Collision)
            {
                try
                {
                    history ??= new DotGame.Core.CombinedHistoryService();
                    history.BeginTileComposite();
                }
                catch { /* best-effort */ }
            }
            PaintAtPosition(mapPoint, properties, isDrag: false);
        }

        private void MapCanvas_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (mapCanvas == null)
                return;

            var mapPoint = GetMapPoint(e);
            UpdateStatusCursor(mapPoint);

            if (isMouseDown)
            {
                var properties = e.GetCurrentPoint(mapCanvas).Properties;
                PaintAtPosition(mapPoint, properties, isDrag: true);
            }
        }

        private void MapCanvas_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            isMouseDown = false;
            e.Pointer.Capture(null);
            SyncMapFromEditorState();
            // End composite change and push to undo stack if any
            if (currentMode == EditorMode.Tiles && currentTool == EditorTool.Collision)
            {
                try
                {
                    var composite = history?.EndPassabilityComposite();
                    if (composite != null)
                    {
                        PushHistory($"Applied collision edits ({composite.Changes.Count} tiles)");
                    }
                    UpdateUndoRedoButtonState();
                }
                catch { /* best-effort */ }
            }
            // End composite tile change and commit to history manager
            try
            {
                if (history != null)
                {
                    var composite = history.EndTileComposite();
                    if (composite != null)
                        PushHistory($"Applied tile edits ({composite.Changes.Count} tiles)");
                    else
                        PushHistory("Applied tile edits");
                    UpdateUndoRedoButtonState();
                }
            }
            catch { /* best-effort */ }
        }

        private void MapCanvas_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (e.Delta.Y > 0)
            {
                AdjustZoom(1.1);
            }
            else if (e.Delta.Y < 0)
            {
                AdjustZoom(0.9);
            }

            e.Handled = true;
        }

        private void MapCanvas_PointerLeave(object? sender, PointerEventArgs e)
        {
            UpdateStatusCursor(null);
        }

        private void EditorWindow_KeyDown(object? sender, KeyEventArgs e)
        {
            // If the event was already handled by a focused control (e.g., a TextBox), skip it.
            if (e.Handled)
                return;

            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.Z)
            {
                // Prefer tile undo if available, otherwise passability undo
                if (history != null && history.TileUndoCount > 0)
                    UndoTiles();
                else
                    UndoPassability();
                e.Handled = true;
            }
            else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.Y)
            {
                if (history != null && history.TileRedoCount > 0)
                    RedoTiles();
                else
                    RedoPassability();
                e.Handled = true;
            }
        }

        private void PaintAtPosition(Point mapPoint, PointerPointProperties properties, bool isDrag)
        {
            if (mapCanvas == null)
                return;

            var gridCoords = ComputeGridCoordinates(mapPoint);
            if (!gridCoords.HasValue)
                return;

            var (gridX, gridY) = gridCoords.Value;
                bool erase = properties.IsRightButtonPressed || currentTool == EditorTool.Eraser;
                bool primaryPressed = properties.IsLeftButtonPressed && !erase;

                // If collision tool is active and we're in tiles mode, toggle passability
                if (currentMode == EditorMode.Tiles && currentTool == EditorTool.Collision)
                {
                    // Right-click to set passable/unset blocked, left-click to toggle
                    if (erase)
                        SetPassabilityAt(gridX, gridY, true);
                    else if (primaryPressed)
                        TogglePassabilityAt(gridX, gridY);

                    RenderMap();
                    SyncMapFromEditorState();
                    return;
                }

            switch (currentMode)
            {
                case EditorMode.Tiles:
                    if (erase)
                    {
                        PaintTiles(gridX, gridY, true);
                    }
                    else if (currentTool == EditorTool.Fill && primaryPressed && !isDrag)
                    {
                        FloodFillTile(gridX, gridY);
                    }
                    else if (primaryPressed || (!isDrag && currentTool == EditorTool.Brush))
                    {
                        PaintTiles(gridX, gridY, false);
                    }
                    RenderMap();
                    SyncMapFromEditorState();
                    break;
                case EditorMode.Characters:
                    if (!primaryPressed && !erase)
                        break;
                    HandleCharacterPlacement(gridX, gridY, erase);
                    RenderMap();
                    SyncMapFromEditorState();
                    break;
                case EditorMode.Doodads:
                    if (!primaryPressed && !erase)
                        break;
                    HandleDoodadPlacement(gridX, gridY, erase);
                    RenderMap();
                    SyncMapFromEditorState();
                    break;
                case EditorMode.Triggers:
                    if (erase)
                    {
                        if (!RemoveTriggerAt(gridX, gridY))
                        {
                            var message = $"No trigger present at ({gridX}, {gridY}).";
                            logger.Info(message);
                            PushHistory(message);
                        }
                    }
                    else if (primaryPressed)
                    {
                        if (pendingTriggerTemplate == null)
                        {
                            var existingTrigger = GetTriggerAt(gridX, gridY);
                            if (existingTrigger != null)
                            {
                                selectedTrigger = existingTrigger;
                                SelectTriggerInList(existingTrigger);
                                var message = $"Selected trigger '{existingTrigger.Name}' at ({gridX}, {gridY}).";
                                logger.Info(message);
                                PushHistory(message);
                            }
                            else
                            {
                                const string message = "No trigger template selected. Use Add Trigger to create one.";
                                logger.Info(message);
                                PushHistory(message);
                            }
                        }
                        else
                        {
                            var triggerName = string.IsNullOrWhiteSpace(pendingTriggerTemplate.Name)
                                ? "Trigger"
                                : pendingTriggerTemplate.Name;
                            AddBehaviorTrigger(gridX, gridY, triggerName);
                        }
                    }
                    break;
            }
        }

        private void PaintTiles(int gridX, int gridY, bool erase)
        {
            if (!erase && selectedTile == null)
            {
                const string message = "Select a tile from the palette before painting.";
                logger.Info(message);
                PushHistory(message);
                return;
            }

            var tiles = ActiveTiles;
            int width = tiles.GetLength(0);
            int height = tiles.GetLength(1);
            int offset = brushSize / 2;

            for (int dy = 0; dy < brushSize; dy++)
            {
                for (int dx = 0; dx < brushSize; dx++)
                {
                    int x = gridX - offset + dx;
                    int y = gridY - offset + dy;
                    if (x < 0 || y < 0 || x >= width || y >= height)
                        continue;

                    // record previous serialized value
                    string? oldSerialized = GetTopmostTile(x, y)?.GetSerializedValue();
                    string? newSerialized = erase ? null : selectedTile!.GetSerializedValue();

                    tiles[x, y] = erase ? null : selectedTile!.Clone();

                    try
                    {
                        history ??= new DotGame.Core.CombinedHistoryService();
                        var tileChange = new DotGame.Core.TileHistoryManager.TileChange(x, y, oldSerialized, newSerialized);
                        history.RecordTileChange(tileChange);
                    }
                    catch { /* best-effort */ }
                }
            }
        }

        private void SetPassabilityAt(int x, int y, bool passable)
        {
            if (editorPassability == null)
                return;
            if (x < 0 || y < 0 || x >= editorPassability.GetLength(0) || y >= editorPassability.GetLength(1))
                return;
            var old = editorPassability[x, y];
            if (old == passable)
                return;
            editorPassability[x, y] = passable;
            var change = new DotGame.Core.CombinedHistoryService.PassabilityChange(x, y, old, passable);
            history ??= new DotGame.Core.CombinedHistoryService();
            history.RecordPassabilityChange(change);
            UpdateUndoRedoButtonState();
            // keep runtime map in sync so preview can use it
            try
            {
                map?.GetType(); // no-op to avoid null issues
            }
            catch { }
        }

        private void TogglePassabilityAt(int x, int y)
        {
            if (editorPassability == null)
                return;
            if (x < 0 || y < 0 || x >= editorPassability.GetLength(0) || y >= editorPassability.GetLength(1))
                return;
            var old = editorPassability[x, y];
            var nw = !old;
            editorPassability[x, y] = nw;
            var change = new DotGame.Core.CombinedHistoryService.PassabilityChange(x, y, old, nw);
            history ??= new DotGame.Core.CombinedHistoryService();
            history.RecordPassabilityChange(change);
            UpdateUndoRedoButtonState();
        }

        private void UndoPassability()
        {
            if (history == null || history.UndoCount == 0)
                return;
            var entry = history.PopUndo();
            if (editorPassability == null)
                return;
            if (entry is DotGame.Core.CombinedHistoryService.PassabilityChange single)
            {
                editorPassability[single.X, single.Y] = single.OldValue;
                history.PushRedo(single);
                PushHistory($"Undo collision at ({single.X},{single.Y}) -> {(single.OldValue ? "passable" : "blocked")}");
            }
            else if (entry is DotGame.Core.CombinedHistoryService.CompositePassabilityChange composite)
            {
                for (int i = composite.Changes.Count - 1; i >= 0; i--)
                {
                    var c = composite.Changes[i];
                    editorPassability[c.X, c.Y] = c.OldValue;
                }
                history.PushRedo(composite);
                PushHistory($"Undo collision edits ({composite.Changes.Count} tiles)");
            }
            RenderMap();
            SyncMapFromEditorState();
            UpdateUndoRedoButtonState();
        }

        private void RedoPassability()
        {
            if (history == null || history.RedoCount == 0)
                return;
            var entry = history.PopRedo();
            if (editorPassability == null)
                return;
            if (entry is DotGame.Core.CombinedHistoryService.PassabilityChange single)
            {
                editorPassability[single.X, single.Y] = single.NewValue;
                history.PushUndo(single);
                PushHistory($"Redo collision at ({single.X},{single.Y}) -> {(single.NewValue ? "passable" : "blocked")}");
            }
            else if (entry is DotGame.Core.CombinedHistoryService.CompositePassabilityChange composite)
            {
                foreach (var c in composite.Changes)
                    editorPassability[c.X, c.Y] = c.NewValue;
                history.PushUndo(composite);
                PushHistory($"Redo collision edits ({composite.Changes.Count} tiles)");
            }
            RenderMap();
            SyncMapFromEditorState();
            UpdateUndoRedoButtonState();
        }

        private void UpdateUndoRedoButtonState()
        {
            // enable if either passability or tile stacks have entries
            if (btnUndo != null)
                btnUndo.IsEnabled = history != null && history.UndoCount > 0;
            if (btnRedo != null)
                btnRedo.IsEnabled = history != null && history.RedoCount > 0;
        }

        private void UndoTiles()
        {
            if (history == null || history.TileUndoCount == 0)
                return;
            var entry = history.PopUndo();
            if (entry is DotGame.Core.TileHistoryManager.TileChange single)
            {
                var tiles = ActiveTiles;
                if (InBounds(single.X, single.Y))
                {
                    var restored = single.OldSerialized == null ? null : LoadTileEntry(single.OldSerialized!, map.SourceDirectory ?? AppContext.BaseDirectory, activeTilesetState);
                    tiles[single.X, single.Y] = restored;
                    history.PushRedo(single);
                    PushHistory($"Undo tile at ({single.X},{single.Y})");
                }
            }
            else if (entry is DotGame.Core.TileHistoryManager.CompositeTileChange composite)
            {
                var tiles = ActiveTiles;
                for (int i = composite.Changes.Count - 1; i >= 0; i--)
                {
                    var c = composite.Changes[i];
                    var restored = c.OldSerialized == null ? null : LoadTileEntry(c.OldSerialized!, map.SourceDirectory ?? AppContext.BaseDirectory, activeTilesetState);
                    if (InBounds(c.X, c.Y))
                        tiles[c.X, c.Y] = restored;
                }
                history.PushRedo(composite);
                PushHistory($"Undo tile edits ({composite.Changes.Count} tiles)");
            }
            RenderMap();
            SyncMapFromEditorState();
            UpdateUndoRedoButtonState();
        }

        private void RedoTiles()
        {
            if (history == null || history.TileRedoCount == 0)
                return;
            var entry = history.PopRedo();
            if (entry is DotGame.Core.TileHistoryManager.TileChange single)
            {
                var tiles = ActiveTiles;
                if (InBounds(single.X, single.Y))
                {
                    var restored = single.NewSerialized == null ? null : LoadTileEntry(single.NewSerialized!, map.SourceDirectory ?? AppContext.BaseDirectory, activeTilesetState);
                    tiles[single.X, single.Y] = restored;
                    history.PushUndo(single);
                    PushHistory($"Redo tile at ({single.X},{single.Y})");
                }
            }
            else if (entry is DotGame.Core.TileHistoryManager.CompositeTileChange composite)
            {
                var tiles = ActiveTiles;
                foreach (var c in composite.Changes)
                {
                    var restored = c.NewSerialized == null ? null : LoadTileEntry(c.NewSerialized!, map.SourceDirectory ?? AppContext.BaseDirectory, activeTilesetState);
                    if (InBounds(c.X, c.Y))
                        tiles[c.X, c.Y] = restored;
                }
                history.PushUndo(composite);
                PushHistory($"Redo tile edits ({composite.Changes.Count} tiles)");
            }
            RenderMap();
            SyncMapFromEditorState();
            UpdateUndoRedoButtonState();
        }

        private void FloodFillTile(int startX, int startY)
        {
            if (selectedTile == null)
            {
                const string message = "Select a tile before using the fill tool.";
                logger.Info(message);
                PushHistory(message);
                return;
            }

            var tiles = ActiveTiles;
            int width = tiles.GetLength(0);
            int height = tiles.GetLength(1);

            if (startX < 0 || startY < 0 || startX >= width || startY >= height)
                return;

            var targetValue = tiles[startX, startY]?.GetSerializedValue();
            var replacementValue = selectedTile.GetSerializedValue();

            if (targetValue == replacementValue)
                return;

            try { history ??= new DotGame.Core.CombinedHistoryService(); history.BeginTileComposite(); } catch { }
            var queue = new Queue<(int x, int y)>();
            var visited = new bool[width, height];
            queue.Enqueue((startX, startY));

            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                if (x < 0 || y < 0 || x >= width || y >= height)
                    continue;
                if (visited[x, y])
                    continue;

                visited[x, y] = true;

                var currentValue = tiles[x, y]?.GetSerializedValue();
                if (currentValue != targetValue)
                    continue;

                var oldSerialized = currentValue;
                tiles[x, y] = selectedTile.Clone();
                var newSerialized = tiles[x, y]?.GetSerializedValue();
                try
                {
                    var change = new DotGame.Core.TileHistoryManager.TileChange(x, y, oldSerialized, newSerialized);
                        history?.RecordTileChange(change);
                }
                catch { }

                queue.Enqueue((x + 1, y));
                queue.Enqueue((x - 1, y));
                queue.Enqueue((x, y + 1));
                queue.Enqueue((x, y - 1));
            }

            try
            {
                var composite = history?.EndTileComposite();
                if (composite != null)
                    PushHistory($"Filled area ({composite.Changes.Count} tiles)");
                else
                    PushHistory("Filled area");
                UpdateUndoRedoButtonState();
            }
            catch { }
        }

        private void HandleCharacterPlacement(int tileX, int tileY, bool erase)
        {
            if (erase)
            {
                if (!RemoveCharacterAt(tileX, tileY))
                {
                    var message = $"No character present at ({tileX}, {tileY}).";
                    logger.Info(message);
                    PushHistory(message);
                }
                return;
            }

            if (pendingCharacterTemplate == null)
            {
                var existing = GetCharacterAt(tileX, tileY);
                if (existing != null)
                {
                    selectedCharacter = existing;
                    var message = $"Selected character {existing.Name} at ({tileX}, {tileY}).";
                    logger.Info(message);
                    PushHistory(message);
                    SelectCharacterInList(existing);
                }
                else
                {
                    const string message = "No character template selected. Use Add Character to create one.";
                    logger.Info(message);
                    PushHistory(message);
                }
                return;
            }

            var placement = CloneCharacterTemplate(pendingCharacterTemplate);
            placement.TileX = tileX;
            placement.TileY = tileY;

            RemoveCharacterAt(tileX, tileY);
            characters.Add(placement);
            selectedCharacter = placement;
            SelectCharacterInList(placement);
            var confirmation = $"Placed character {placement.Name} at ({tileX}, {tileY}).";
            logger.Info(confirmation);
            PushHistory(confirmation);
        }

        private void HandleDoodadPlacement(int tileX, int tileY, bool erase)
        {
            if (erase)
            {
                if (!RemoveDoodadAt(tileX, tileY))
                {
                    var message = $"No doodad present at ({tileX}, {tileY}).";
                    logger.Info(message);
                    PushHistory(message);
                }
                return;
            }

            if (pendingDoodadTemplate == null)
            {
                var existing = GetDoodadAt(tileX, tileY);
                if (existing != null)
                {
                    selectedDoodad = existing;
                    var message = $"Selected doodad {existing.Type} at ({tileX}, {tileY}).";
                    logger.Info(message);
                    PushHistory(message);
                    SelectDoodadInList(existing);
                }
                else
                {
                    const string message = "No doodad template selected. Use Add Doodad to create one.";
                    logger.Info(message);
                    PushHistory(message);
                }
                return;
            }

            var placement = CloneDoodadTemplate(pendingDoodadTemplate);
            placement.TileX = tileX;
            placement.TileY = tileY;

            RemoveDoodadAt(tileX, tileY);
            doodads.Add(placement);
            selectedDoodad = placement;
            SelectDoodadInList(placement);
            var confirmation = $"Placed doodad {placement.Type} at ({tileX}, {tileY}).";
            logger.Info(confirmation);
            PushHistory(confirmation);
        }

        private Character? GetCharacterAt(int tileX, int tileY)
        {
            return characters.FirstOrDefault(c => c.TileX == tileX && c.TileY == tileY);
        }

        private bool RemoveCharacterAt(int tileX, int tileY)
        {
            var existing = GetCharacterAt(tileX, tileY);
            if (existing == null)
                return false;

            RemoveCharacter(existing);
            if (ReferenceEquals(selectedCharacter, existing))
                selectedCharacter = null;
            return true;
        }

        private Doodad? GetDoodadAt(int tileX, int tileY)
        {
            return doodads.FirstOrDefault(d => d.TileX == tileX && d.TileY == tileY);
        }

        private bool RemoveDoodadAt(int tileX, int tileY)
        {
            var existing = GetDoodadAt(tileX, tileY);
            if (existing == null)
                return false;

            RemoveDoodad(existing);
            if (ReferenceEquals(selectedDoodad, existing))
                selectedDoodad = null;
            return true;
        }

        private BehaviorTrigger? GetTriggerAt(int tileX, int tileY)
        {
            return triggers.FirstOrDefault(t => t.TileX == tileX && t.TileY == tileY);
        }

        private bool RemoveTriggerAt(int tileX, int tileY)
        {
            var existing = GetTriggerAt(tileX, tileY);
            if (existing == null)
                return false;

            RemoveBehaviorTrigger(existing);
            return true;
        }

        private Character CloneCharacterTemplate(Character template)
        {
            var clone = new Character(template.TileX, template.TileY, template.Class, template.Name)
            {
                Sprite = template.Sprite,
                Color = template.Color,
                BehaviorScript = template.BehaviorScript,
                TriggerEvent = template.TriggerEvent
            };
            clone.Direction = template.Direction;
            clone.CurrentHP = template.CurrentHP;
            return clone;
        }

        private Character CloneCharacterForEditor(Character source)
        {
            var clone = CloneCharacterTemplate(source);
            clone.TileX = source.TileX;
            clone.TileY = source.TileY;
            return clone;
        }

        private Doodad CloneDoodadTemplate(Doodad template)
        {
            return new Doodad(template.TileX, template.TileY, template.Type)
            {
                Sprite = template.Sprite,
                Color = template.Color,
                Collidable = template.Collidable,
                Interactable = template.Interactable,
                Animated = template.Animated,
                Trigger = template.Trigger,
                OnInteract = template.OnInteract
            };
        }

        private Doodad CloneDoodadForEditor(Doodad source)
        {
            var clone = CloneDoodadTemplate(source);
            clone.TileX = source.TileX;
            clone.TileY = source.TileY;
            return clone;
        }

        private static Color ParseColorOrDefault(string? value, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            try
            {
                return Color.Parse(value);
            }
            catch
            {
                return fallback;
            }
        }

        private void UpdateGridSizeSelection(int size)
        {
            if (cmbGridSize == null)
                return;

            var match = cmbGridSize.Items?.OfType<ComboBoxItem>().FirstOrDefault(item =>
                int.TryParse(item.Content?.ToString(), out var parsed) && parsed == size);

            if (match != null)
            {
                suppressGridSizeEvent = true;
                cmbGridSize.SelectedItem = match;
                suppressGridSizeEvent = false;
            }
        }

        private async Task<Character?> PromptCharacterTemplateAsync()
        {
            var dialog = new CharacterCreationWindow();
            var result = await dialog.ShowDialog<bool?>(this);
            if (result != true)
                return null;

            var name = string.IsNullOrWhiteSpace(dialog.SelectedName) ? "Hero" : dialog.SelectedName!;
            var character = new Character(0, 0, dialog.SelectedClass, name)
            {
                Sprite = dialog.SelectedSprite,
                Color = dialog.SelectedSprite != null ? Colors.Transparent : Colors.DeepSkyBlue
            };
            return character;
        }

        private async Task<(Character? character, bool delete)> PromptCharacterEditAsync(Character target)
        {
            var dialog = new Window
            {
                Title = "Edit Character",
                Width = 360,
                Height = 360,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var stack = new StackPanel { Margin = new Thickness(20), Spacing = 10 };
            stack.Children.Add(new TextBlock { Text = "Name" });
            var nameBox = new TextBox { Text = target.Name };
            stack.Children.Add(nameBox);

            stack.Children.Add(new TextBlock { Text = "Class" });
            var classCombo = new ComboBox();
            classCombo.ItemsSource = Enum.GetValues(typeof(CharacterClass));
            classCombo.SelectedItem = target.Class;
            stack.Children.Add(classCombo);

            stack.Children.Add(new TextBlock { Text = "Color (#AARRGGBB)" });
            var colorBox = new TextBox { Text = target.Color.ToString() };
            stack.Children.Add(colorBox);

            stack.Children.Add(new TextBlock { Text = "Trigger Event" });
            var triggerBox = new TextBox { Text = target.TriggerEvent ?? string.Empty };
            stack.Children.Add(triggerBox);

            stack.Children.Add(new TextBlock { Text = "Behavior Script" });
            var scriptBox = new TextBox { Text = target.BehaviorScript ?? string.Empty };
            stack.Children.Add(scriptBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10
            };
            var saveButton = new Button { Content = "Save", Width = 80 };
            var deleteButton = new Button { Content = "Delete", Width = 80 };
            var cancelButton = new Button { Content = "Cancel", Width = 80 };
            buttonPanel.Children.Add(saveButton);
            buttonPanel.Children.Add(deleteButton);
            buttonPanel.Children.Add(cancelButton);
            stack.Children.Add(buttonPanel);

            dialog.Content = stack;

            Character? updated = null;
            bool deleteRequested = false;

            saveButton.Click += (_, __) =>
            {
                var selectedClass = classCombo.SelectedItem is CharacterClass cls ? cls : target.Class;
                var name = string.IsNullOrWhiteSpace(nameBox.Text) ? target.Name : nameBox.Text!;
                var clone = new Character(target.TileX, target.TileY, selectedClass, name)
                {
                    Sprite = target.Sprite,
                    Color = ParseColorOrDefault(colorBox.Text, target.Color),
                    BehaviorScript = string.IsNullOrWhiteSpace(scriptBox.Text) ? null : scriptBox.Text,
                    TriggerEvent = string.IsNullOrWhiteSpace(triggerBox.Text) ? null : triggerBox.Text
                };
                clone.Direction = target.Direction;
                clone.CurrentHP = Math.Min(target.CurrentHP, clone.Attributes.MaxHP);
                updated = clone;
                dialog.Close();
            };

            deleteButton.Click += (_, __) =>
            {
                deleteRequested = true;
                dialog.Close();
            };

            cancelButton.Click += (_, __) => dialog.Close();

            await dialog.ShowDialog(this);
            return (updated, deleteRequested);
        }

        private async Task<Doodad?> PromptDoodadTemplateAsync()
        {
            var (doodad, _) = await PromptDoodadEditAsync(null);
            return doodad;
        }

        private async Task<(Doodad? doodad, bool delete)> PromptDoodadEditAsync(Doodad? target)
        {
            var dialog = new Window
            {
                Title = target == null ? "Add Doodad" : "Edit Doodad",
                Width = 360,
                Height = 340,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var stack = new StackPanel { Margin = new Thickness(20), Spacing = 10 };
            stack.Children.Add(new TextBlock { Text = "Type" });
            var typeBox = new TextBox { Text = target?.Type ?? "Doodad" };
            stack.Children.Add(typeBox);

            stack.Children.Add(new TextBlock { Text = "Color (#AARRGGBB)" });
            var colorBox = new TextBox { Text = (target?.Color ?? Colors.Transparent).ToString() };
            stack.Children.Add(colorBox);

            var collidableBox = new CheckBox { Content = "Collidable", IsChecked = target?.Collidable ?? false };
            stack.Children.Add(collidableBox);

            var interactableBox = new CheckBox { Content = "Interactable", IsChecked = target?.Interactable ?? false };
            stack.Children.Add(interactableBox);

            var animatedBox = new CheckBox { Content = "Animated", IsChecked = target?.Animated ?? false };
            stack.Children.Add(animatedBox);

            stack.Children.Add(new TextBlock { Text = "Trigger" });
            var triggerBox = new TextBox { Text = target?.Trigger ?? string.Empty };
            stack.Children.Add(triggerBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10
            };
            var saveButton = new Button { Content = "Save", Width = 80 };
            var deleteButton = new Button { Content = "Delete", Width = 80, IsVisible = target != null };
            var cancelButton = new Button { Content = "Cancel", Width = 80 };
            buttonPanel.Children.Add(saveButton);
            if (target != null) buttonPanel.Children.Add(deleteButton);
            buttonPanel.Children.Add(cancelButton);
            stack.Children.Add(buttonPanel);

            dialog.Content = stack;

            Doodad? updated = null;
            bool deleteRequested = false;

            saveButton.Click += (_, __) =>
            {
                var type = string.IsNullOrWhiteSpace(typeBox.Text) ? (target?.Type ?? "Doodad") : typeBox.Text!;
                var doodad = new Doodad(target?.TileX ?? 0, target?.TileY ?? 0, type)
                {
                    Sprite = target?.Sprite,
                    Color = ParseColorOrDefault(colorBox.Text, target?.Color ?? Colors.Transparent),
                    Collidable = collidableBox.IsChecked ?? false,
                    Interactable = interactableBox.IsChecked ?? false,
                    Animated = animatedBox.IsChecked ?? false,
                    Trigger = string.IsNullOrWhiteSpace(triggerBox.Text) ? null : triggerBox.Text,
                    OnInteract = target?.OnInteract
                };
                updated = doodad;
                dialog.Close();
            };

            deleteButton.Click += (_, __) =>
            {
                deleteRequested = true;
                dialog.Close();
            };

            cancelButton.Click += (_, __) => dialog.Close();

            await dialog.ShowDialog(this);
            return (updated, deleteRequested);
        }

        private async Task<(BehaviorTrigger? trigger, bool confirmed)> PromptTriggerAsync(BehaviorTrigger? existing, bool requestTilePosition = true)
        {
            var dialog = new Window
            {
                Title = existing == null ? "Add Trigger" : "Edit Trigger",
                Width = 320,
                Height = requestTilePosition ? 260 : 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var stack = new StackPanel { Margin = new Thickness(20), Spacing = 10 };
            stack.Children.Add(new TextBlock { Text = "Name" });
            var nameBox = new TextBox { Text = existing?.Name ?? string.Empty };
            stack.Children.Add(nameBox);

            NumericUpDown? tileXInput = null;
            NumericUpDown? tileYInput = null;

            if (requestTilePosition)
            {
                var tiles = ActiveTiles;
                var maxX = Math.Max(0, tiles.GetLength(0) - 1);
                var maxY = Math.Max(0, tiles.GetLength(1) - 1);

                stack.Children.Add(new TextBlock { Text = "Tile X" });
                tileXInput = new NumericUpDown
                {
                    Minimum = 0,
                    Maximum = maxX,
                    Value = existing?.TileX ?? 0
                };
                stack.Children.Add(tileXInput);

                stack.Children.Add(new TextBlock { Text = "Tile Y" });
                tileYInput = new NumericUpDown
                {
                    Minimum = 0,
                    Maximum = maxY,
                    Value = existing?.TileY ?? 0
                };
                stack.Children.Add(tileYInput);
            }
            else
            {
                var caption = new TextBlock
                {
                    Text = "Click on the map after saving to choose a tile.",
                    Foreground = Brushes.Gray,
                    FontSize = 11
                };
                stack.Children.Add(caption);
            }

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10
            };
            var saveButton = new Button { Content = "Save", Width = 80 };
            var cancelButton = new Button { Content = "Cancel", Width = 80 };
            buttonPanel.Children.Add(saveButton);
            buttonPanel.Children.Add(cancelButton);
            stack.Children.Add(buttonPanel);

            dialog.Content = stack;

            BehaviorTrigger? result = null;
            bool confirmed = false;

            saveButton.Click += (_, _) =>
            {
                var name = string.IsNullOrWhiteSpace(nameBox.Text) ? "Trigger" : nameBox.Text!.Trim();
                var tileX = requestTilePosition
                    ? (int)(tileXInput?.Value ?? 0)
                    : existing?.TileX ?? 0;
                var tileY = requestTilePosition
                    ? (int)(tileYInput?.Value ?? 0)
                    : existing?.TileY ?? 0;

                result = new BehaviorTrigger
                {
                    Name = name,
                    TileX = tileX,
                    TileY = tileY
                };

                confirmed = true;
                dialog.Close();
            };

            cancelButton.Click += (_, _) => dialog.Close();

            await dialog.ShowDialog(this);
            return (result, confirmed);
        }

        private void BtnTileSelect_Click(object? sender, RoutedEventArgs e)
        {
            SwitchToTilesMode(sender, e);
            if (selectedTile == null && tiles.Count > 0)
            {
                selectedTile = tiles[0];
                if (tilePalette?.Children.Count > 0 && tilePalette.Children[0] is Border border)
                {
                    if (selectedTileBorder != null)
                        selectedTileBorder.Background = Brushes.Transparent;
                    border.Background = Brushes.LightBlue;
                    selectedTileBorder = border;
                }
            }
            const string paintModeMessage = "Tile painting mode enabled. Select a tile from the palette to change the brush.";
            logger.Info(paintModeMessage);
            PushHistory(paintModeMessage);
        }

        private void BtnTileFill_Click(object? sender, RoutedEventArgs e)
        {
            if (selectedTile == null)
            {
                const string message = "Select a tile before using Fill Area.";
                logger.Info(message);
                PushHistory(message);
                return;
            }

            var tiles = ActiveTiles;
            int width = tiles.GetLength(0);
            int height = tiles.GetLength(1);

            try
            {
                history ??= new DotGame.Core.CombinedHistoryService();
                history.BeginTileComposite();
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        var old = tiles[x, y]?.GetSerializedValue();
                        tiles[x, y] = selectedTile.Clone();
                        var nw = tiles[x, y]?.GetSerializedValue();
                        var change = new DotGame.Core.TileHistoryManager.TileChange(x, y, old, nw);
                        history.RecordTileChange(change);
                    }
                }
                var composite = history.EndTileComposite();
                RenderMap();
                if (composite != null)
                {
                    var fillMessage = $"Filled the entire map with the current tile selection ({composite.Changes.Count} tiles).";
                    logger.Info(fillMessage);
                    PushHistory(fillMessage);
                }
                else
                {
                    const string fillMessage = "Filled the entire map with the current tile selection.";
                    logger.Info(fillMessage);
                    PushHistory(fillMessage);
                }
                SyncMapFromEditorState();
                UpdateUndoRedoButtonState();
            }
            catch { }
        }

        private async void BtnAddCharacter_Click(object? sender, RoutedEventArgs e)
        {
            var template = await PromptCharacterTemplateAsync();
            if (template == null)
                return;

            pendingCharacterTemplate = CloneCharacterTemplate(template);
            SwitchToCharactersMode(sender, e);
            const string readyCharacterMessage = "Character template ready. Left click on the map to place it.";
            logger.Info(readyCharacterMessage);
            PushHistory(readyCharacterMessage);
        }

        private async void BtnEditCharacter_Click(object? sender, RoutedEventArgs e)
        {
            if (characters.Count == 0)
            {
                const string message = "There are no characters to edit.";
                logger.Info(message);
                PushHistory(message);
                return;
            }

            var target = selectedCharacter ?? characters[0];
            var (updated, delete) = await PromptCharacterEditAsync(target);

            if (delete)
            {
                characters.Remove(target);
                if (ReferenceEquals(selectedCharacter, target))
                    selectedCharacter = null;
                var message = $"Deleted character {target.Name}.";
                logger.Info(message);
                PushHistory(message);
                SelectCharacterInList(null);
            }
            else if (updated != null)
            {
                var index = characters.IndexOf(target);
                if (index >= 0)
                {
                    characters[index] = updated;
                    selectedCharacter = updated;
                    var message = $"Updated character {updated.Name}.";
                    logger.Info(message);
                    PushHistory(message);
                    SelectCharacterInList(updated);
                }
            }

            RenderMap();
            SyncMapFromEditorState();
        }

        private async void BtnAddDoodad_Click(object? sender, RoutedEventArgs e)
        {
            var doodad = await PromptDoodadTemplateAsync();
            if (doodad == null)
                return;

            pendingDoodadTemplate = CloneDoodadTemplate(doodad);
            SwitchToDoodadsMode(sender, e);
            const string readyDoodadMessage = "Doodad template ready. Left click on the map to place it.";
            logger.Info(readyDoodadMessage);
            PushHistory(readyDoodadMessage);
        }

        private async void BtnEditDoodad_Click(object? sender, RoutedEventArgs e)
        {
            if (doodads.Count == 0)
            {
                const string message = "There are no doodads to edit.";
                logger.Info(message);
                PushHistory(message);
                return;
            }

            var target = selectedDoodad ?? doodads[0];
            var (updated, delete) = await PromptDoodadEditAsync(target);

            if (delete)
            {
                doodads.Remove(target);
                if (ReferenceEquals(selectedDoodad, target))
                    selectedDoodad = null;
                var message = $"Deleted doodad {target.Type}.";
                logger.Info(message);
                PushHistory(message);
                SelectDoodadInList(null);
            }
            else if (updated != null)
            {
                updated.TileX = target.TileX;
                updated.TileY = target.TileY;
                var index = doodads.IndexOf(target);
                if (index >= 0)
                {
                    doodads[index] = updated;
                    selectedDoodad = updated;
                    var message = $"Updated doodad {updated.Type}.";
                    logger.Info(message);
                    PushHistory(message);
                    SelectDoodadInList(updated);
                }
            }

            RenderMap();
            SyncMapFromEditorState();
        }

        private async void BtnAddTrigger_Click(object? sender, RoutedEventArgs e)
        {
            var (template, confirmed) = await PromptTriggerAsync(pendingTriggerTemplate, requestTilePosition: false);
            if (!confirmed || template == null)
                return;

            pendingTriggerTemplate = template;
            SwitchToTriggersMode(sender, e ?? new RoutedEventArgs());
            var message = $"Trigger template '{template.Name}' ready. Left click on the map to place it.";
            logger.Info(message);
            PushHistory(message);
        }

        private void BtnRemoveTrigger_Click(object? sender, RoutedEventArgs e)
        {
            if (selectedTrigger == null)
            {
                const string message = "Select a trigger to remove.";
                logger.Info(message);
                PushHistory(message);
                return;
            }

            var trigger = selectedTrigger;
            selectedTrigger = null;
            SelectTriggerInList(null);
            RemoveBehaviorTrigger(trigger);
        }

        private void SwitchMode(EditorMode mode)
        {
            currentMode = mode;
            RenderMap();
            UpdateStatusTool();
        }

        private void RenderMap()
        {
            if (mapCanvas == null)
                return;

            mapCanvas.Children.Clear();

            float tileWidth = (float)(numTileWidth?.Value ?? (map.TileW > 0 ? map.TileW : 32));
            float tileHeight = (float)(numTileHeight?.Value ?? (map.TileH > 0 ? map.TileH : 32));
            currentCellSize = Math.Max(8f, Math.Max(tileWidth, tileHeight));

            var activeTiles = ActiveTiles;
            int width = activeTiles.GetLength(0);
            int height = activeTiles.GetLength(1);

            mapCanvas.Width = width * currentCellSize;
            mapCanvas.Height = height * currentCellSize;

            for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                var layer = layers[layerIndex];
                if (!layer.IsVisible)
                    continue;

                var tiles = layer.Tiles;
                int cols = tiles.GetLength(0);
                int rows = tiles.GetLength(1);

                for (int x = 0; x < cols; x++)
                {
                    for (int y = 0; y < rows; y++)
                    {
                        var entry = tiles[x, y];
                        if (entry == null)
                            continue;

                        var img = new Image
                        {
                            Source = entry.Bitmap,
                            Width = currentCellSize,
                            Height = currentCellSize,
                            Stretch = Stretch.Fill
                        };
                        Canvas.SetLeft(img, x * currentCellSize);
                        Canvas.SetTop(img, y * currentCellSize);
                        mapCanvas.Children.Add(img);
                    }
                }
            }

            if (currentMode == EditorMode.Characters || currentMode == EditorMode.Tiles)
            {
                foreach (var character in characters)
                {
                    var rect = new Rectangle
                    {
                        Width = currentCellSize,
                        Height = currentCellSize,
                        Fill = new SolidColorBrush(character.Color),
                        Stroke = Brushes.Black,
                        StrokeThickness = 2
                    };
                    Canvas.SetLeft(rect, character.TileX * currentCellSize);
                    Canvas.SetTop(rect, character.TileY * currentCellSize);
                    mapCanvas.Children.Add(rect);
                }
            }

            if (currentMode == EditorMode.Doodads || currentMode == EditorMode.Tiles)
            {
                foreach (var doodad in doodads)
                {
                    var rect = new Rectangle
                    {
                        Width = currentCellSize,
                        Height = currentCellSize,
                        Fill = doodad.Sprite != null ? new ImageBrush(doodad.Sprite) : new SolidColorBrush(doodad.Color),
                        Stroke = Brushes.Gray,
                        StrokeThickness = 1
                    };
                    Canvas.SetLeft(rect, doodad.TileX * currentCellSize);
                    Canvas.SetTop(rect, doodad.TileY * currentCellSize);
                    mapCanvas.Children.Add(rect);
                }
            }

            foreach (var trigger in triggers)
            {
                var rect = new Rectangle
                {
                    Width = currentCellSize,
                    Height = currentCellSize,
                    Fill = new SolidColorBrush(Color.FromArgb(96, 255, 215, 0)),
                    Stroke = Brushes.Goldenrod,
                    StrokeThickness = 1.5
                };
                Canvas.SetLeft(rect, trigger.TileX * currentCellSize);
                Canvas.SetTop(rect, trigger.TileY * currentCellSize);
                mapCanvas.Children.Add(rect);
            }

            if (gridVisibilityCheck?.IsChecked != false)
            {
                for (int i = 0; i <= width; i++)
                {
                    var vline = new Line
                    {
                        StartPoint = new Point(i * currentCellSize, 0),
                        EndPoint = new Point(i * currentCellSize, height * currentCellSize),
                        Stroke = Brushes.LightGray,
                        StrokeThickness = 1
                    };
                    mapCanvas.Children.Add(vline);
                }

                for (int j = 0; j <= height; j++)
                {
                    var hline = new Line
                    {
                        StartPoint = new Point(0, j * currentCellSize),
                        EndPoint = new Point(width * currentCellSize, j * currentCellSize),
                        Stroke = Brushes.LightGray,
                        StrokeThickness = 1
                    };
                    mapCanvas.Children.Add(hline);
                }
            }

            // Draw passability overlay (blocked tiles) on top
            if (editorPassability != null && (passabilityOverlayCheck?.IsChecked ?? true))
            {
                int cols = Math.Min(width, editorPassability.GetLength(0));
                int rows = Math.Min(height, editorPassability.GetLength(1));
                var blockedBrush = new SolidColorBrush(Color.FromArgb(120, 255, 0, 0));
                for (int x = 0; x < cols; x++)
                {
                    for (int y = 0; y < rows; y++)
                    {
                        // false = blocked, true = passable
                        if (!editorPassability[x, y])
                        {
                            var overlay = new Rectangle
                            {
                                Width = currentCellSize,
                                Height = currentCellSize,
                                Fill = blockedBrush,
                                Stroke = Brushes.Transparent
                            };
                            Canvas.SetLeft(overlay, x * currentCellSize);
                            Canvas.SetTop(overlay, y * currentCellSize);
                            mapCanvas.Children.Add(overlay);
                        }
                    }
                }
            }

            ApplyZoom();
        }

        private void SyncMapFromEditorState(bool suppressPreviewUpdate = false)
        {
            int tileW = (int)(numTileWidth?.Value ?? 32);
            int tileH = (int)(numTileHeight?.Value ?? 32);
            var activeTiles = ActiveTiles;
            int width = activeTiles.GetLength(0);
            int height = activeTiles.GetLength(1);
            var tilesSnapshot = new string?[height, width];
            int?[,]? tileIdSnapshot = activeTilesetState != null ? new int?[height, width] : null;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var tile = GetTopmostTile(x, y);
                    tilesSnapshot[y, x] = tile?.GetSerializedValue();

                    if (tileIdSnapshot != null)
                    {
                        if (tile != null && tile.TryGetTileId(out var id))
                            tileIdSnapshot[y, x] = id;
                        else
                            tileIdSnapshot[y, x] = null;
                    }
                }
            }

            gridSize = Math.Max(width, height);
            var triggerSnapshot = triggers.Select(t => new BehaviorTrigger
            {
                TileX = t.TileX,
                TileY = t.TileY,
                Name = t.Name
            }).ToList();

            var tilesetReference = activeTilesetState?.CreateReferenceForMap();
            map.InitializeFromArray(width, height, tileW, tileH, tilesSnapshot, characters, doodads, triggerSnapshot, map.ExternalTileMapAsset, tileIdSnapshot, tilesetReference, map.SourceDirectory);

            // Ensure editor passability grid exists and matches dimensions. Default to all passable (true).
            if (editorPassability == null || editorPassability.GetLength(0) != width || editorPassability.GetLength(1) != height)
            {
                editorPassability = new bool[width, height];
                for (int x = 0; x < width; x++)
                    for (int y = 0; y < height; y++)
                        editorPassability[x, y] = true;
            }

            // Push editor passability into runtime map so preview reflects edits immediately.
            try
            {
                if (editorPassability != null)
                {
                    // map.SetPassability expects [rows,cols] (y,x)
                    var grid = new bool[height, width];
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            grid[y, x] = editorPassability[x, y];
                        }
                    }
                    map.SetPassability(grid);
                }
            }
            catch (Exception ex)
            {
                logger.Warn($"Failed to push passability to runtime preview: {ex.Message}");
            }

            var fallbackStillRequired = ShouldUseFallbackPreviewMap();
            if (!fallbackStillRequired)
                usingFallbackPreviewMap = false;

            if (!suppressPreviewUpdate && !(usingFallbackPreviewMap && fallbackStillRequired))
            {
                NotifyPreviewMapUpdate();
            }
            UpdateUndoRedoButtonState();
        }

        private void ToggleDiagnosticsPanel(bool? visible = null)
        {
            if (diagnosticsPanel == null)
                return;

            var shouldShow = visible ?? !diagnosticsPanel.IsVisible;
            diagnosticsPanel.IsVisible = shouldShow;
        }

        private async void BtnCaptureTelemetry_Click(object? sender, RoutedEventArgs e)
        {
            if (telemetryCaptureInProgress)
            {
                PushHistory("Telemetry capture already running.");
                UpdateTelemetryStatus("Telemetry capture already in progress.");
                return;
            }

            telemetryCaptureInProgress = true;
            var captureButton = sender as Button;
            if (captureButton != null)
                captureButton.IsEnabled = false;

            UpdateTelemetryStatus("Capturing telemetry snapshot...");

            try
            {
                var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["editor.preview.fallbackMap"] = usingFallbackPreviewMap ? "true" : "false",
                    ["editor.layers"] = layers.Count.ToString(CultureInfo.InvariantCulture),
                    ["editor.jobSystem.active"] = jobSystemActivation.Resolved
                };
                    // no-op here

                if (!string.IsNullOrWhiteSpace(map.SourceDirectory))
                    metadata["editor.map.sourceDirectory"] = map.SourceDirectory!;

                var headlessOptions = new HeadlessRuntimeOptions(
                    frameCount: 300,
                    jobsPerFrame: 96,
                    jobIterations: 8,
                    innerLoopIterations: 256,
                    batchSize: 1,
                    targetFrameRate: 60.0,
                    seed: Environment.TickCount,
                    maxConcurrentJobs: Math.Max(0, jobSystemActivation.WorkerCount * 2),
                    sampleStatistics: false);

                var request = new TelemetryCaptureRequest
                {
                    SessionPrefix = "editor",
                    SessionSuffix = jobSystemActivation.Resolved,
                    JobSystemIdentifier = jobSystemActivation.Resolved,
                    WorkerCountOverride = jobSystemActivation.WorkerCount,
                    Metadata = metadata,
                    HeadlessOptions = headlessOptions
                };

                var result = await telemetryCaptureService
                    .CaptureAsync(request, CancellationToken.None)
                    .ConfigureAwait(false);

                ApplyTelemetryCaptureResult(result);
                PushHistory($"Telemetry captured -> {result.Files.JsonPath}");
            }
            catch (OperationCanceledException)
            {
                PushHistory("Telemetry capture cancelled.");
                UpdateTelemetryStatus("Telemetry capture cancelled.");
            }
            catch (Exception ex)
            {
                var message = $"Telemetry capture failed: {ex.Message}";
                PushHistory(message);
                UpdateTelemetryStatus(message);
            }
            finally
            {
                telemetryCaptureInProgress = false;
                if (captureButton != null)
                    Dispatcher.UIThread.Post(() => captureButton.IsEnabled = true);
            }
        }

        private void ApplyTelemetryCaptureResult(TelemetryCaptureResult result)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => ApplyTelemetryCaptureResult(result));
                return;
            }

            var viewModel = new TelemetrySessionViewModel(result.SessionName, result.Export, result.JobSystem, result.Files);
            telemetrySessions.Insert(0, viewModel);
            while (telemetrySessions.Count > 5)
                telemetrySessions.RemoveAt(telemetrySessions.Count - 1);

            var fileName = IOPath.GetFileName(result.Files.JsonPath);
            UpdateTelemetryStatus($"Telemetry exported: {fileName}");
        }

        private void UpdateTelemetryStatus(string message)
        {
            if (telemetryStatusText == null)
                return;

            void Apply()
            {
                telemetryStatusText.Text = message ?? string.Empty;
            }

            if (Dispatcher.UIThread.CheckAccess())
                Apply();
            else
                Dispatcher.UIThread.Post(Apply);
        }

        private void TelemetryOpenJsonButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            var path = button.Tag as string;
            if (string.IsNullOrWhiteSpace(path))
            {
                UpdateTelemetryStatus("Telemetry JSON path unavailable.");
                return;
            }

            if (!File.Exists(path))
            {
                var message = $"Telemetry JSON not found: {path}";
                PushHistory(message);
                UpdateTelemetryStatus(message);
                return;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                var message = $"Failed to open telemetry JSON: {ex.Message}";
                PushHistory(message);
                UpdateTelemetryStatus(message);
            }
        }

        private void BtnReloadGameData_Click(object? sender, RoutedEventArgs e)
        {
            ReloadGameData();
        }

        private void BtnMonoGamePreview_Click(object? sender, RoutedEventArgs e)
        {
            InitializeRuntimePreview();
            if (viewportTabControl != null)
            {
                viewportTabControl.SelectedIndex = 1;
            }
        }

        private void LaunchMonoGamePreview()
        {
            InitializeRuntimePreview();
            if (viewportTabControl != null)
            {
                viewportTabControl.SelectedIndex = 1;
            }
        }

        private void NotifyPreviewMapUpdate()
        {
            EditorGame? game;
            lock (previewGameLock)
            {
                game = previewGame;
            }

            if (game == null)
                return;

            if (usingFallbackPreviewMap && ShouldUseFallbackPreviewMap())
                return;

            try
            {
                var snapshot = map.Clone();
                game.RequestMapSwap(snapshot);
                usingFallbackPreviewMap = false;
                if (!ShouldUseFallbackPreviewMap())
                {
                    UpdateRuntimePreviewStatus(string.Empty, false, false);
                }
            }
            catch (Exception ex)
            {
                logger.Error("MonoGame preview sync error.", ex);
                PushHistory($"MonoGame preview sync error: {ex.Message}");
                UpdateRuntimePreviewStatus($"Preview update failed.\n{ex.Message}", true, true);
            }
        }

        private SKBitmap BitmapToSKBitmap(Bitmap bitmap)
        {
            try
            {
                using var stream = new System.IO.MemoryStream();
                bitmap.Save(stream);
                stream.Position = 0;
                return SKBitmap.Decode(stream);
            }
            catch (Exception ex)
            {
                logger.Error("Error converting Bitmap to SKBitmap.", ex);
                throw;
            }
        }

        private void SwitchToTilesMode(object? sender, RoutedEventArgs e)
        {
            currentMode = EditorMode.Tiles;
            if (TilesToolsPanel != null) TilesToolsPanel.IsVisible = true;
            if (CharactersToolsPanel != null) CharactersToolsPanel.IsVisible = false;
            if (DoodadsToolsPanel != null) DoodadsToolsPanel.IsVisible = false;
            RenderMap();
            UpdateStatusTool();
        }

        private void SwitchToCharactersMode(object? sender, RoutedEventArgs e)
        {
            currentMode = EditorMode.Characters;
            if (TilesToolsPanel != null) TilesToolsPanel.IsVisible = false;
            if (CharactersToolsPanel != null) CharactersToolsPanel.IsVisible = true;
            if (DoodadsToolsPanel != null) DoodadsToolsPanel.IsVisible = false;
            RenderMap();
            UpdateStatusTool();
        }

        private void SwitchToDoodadsMode(object? sender, RoutedEventArgs e)
        {
            currentMode = EditorMode.Doodads;
            if (TilesToolsPanel != null) TilesToolsPanel.IsVisible = false;
            if (CharactersToolsPanel != null) CharactersToolsPanel.IsVisible = false;
            if (DoodadsToolsPanel != null) DoodadsToolsPanel.IsVisible = true;
            RenderMap();
            UpdateStatusTool();
        }

        private void SwitchToTriggersMode(object? sender, RoutedEventArgs e)
        {
            currentMode = EditorMode.Triggers;
            if (TilesToolsPanel != null) TilesToolsPanel.IsVisible = false;
            if (CharactersToolsPanel != null) CharactersToolsPanel.IsVisible = false;
            if (DoodadsToolsPanel != null) DoodadsToolsPanel.IsVisible = false;
            RenderMap();
            UpdateStatusTool();
        }

        private void PlaceCharacter(int tileX, int tileY, Character character)
        {
            if (!map.InBounds(tileX, tileY))
            {
                const string outOfBoundsMessage = "Character placement out of bounds.";
                logger.Warn(outOfBoundsMessage);
                PushHistory(outOfBoundsMessage);
                return;
            }

            character.TileX = tileX;
            character.TileY = tileY;
            characters.Add(character);
            var placementMessage = $"Placed character {character.Name} at ({tileX}, {tileY}).";
            logger.Info(placementMessage);
            PushHistory(placementMessage);
            SelectCharacterInList(character);
        }

        private void RemoveCharacter(Character character)
        {
            if (characters.Remove(character))
            {
                if (ReferenceEquals(selectedCharacter, character))
                {
                    selectedCharacter = null;
                    SelectCharacterInList(null);
                }
                var removalMessage = $"Removed character {character.Name}.";
                logger.Info(removalMessage);
                PushHistory(removalMessage);
            }
        }

        private void PlaceDoodad(int tileX, int tileY, Doodad doodad)
        {
            if (!map.InBounds(tileX, tileY))
            {
                const string outOfBoundsMessage = "Doodad placement out of bounds.";
                logger.Warn(outOfBoundsMessage);
                PushHistory(outOfBoundsMessage);
                return;
            }

            doodad.TileX = tileX;
            doodad.TileY = tileY;
            doodads.Add(doodad);
            var placementMessage = $"Placed doodad {doodad.Type} at ({tileX}, {tileY}).";
            logger.Info(placementMessage);
            PushHistory(placementMessage);
            SelectDoodadInList(doodad);
        }

        private void RemoveDoodad(Doodad doodad)
        {
            if (doodads.Remove(doodad))
            {
                if (ReferenceEquals(selectedDoodad, doodad))
                {
                    selectedDoodad = null;
                    SelectDoodadInList(null);
                }
                var removalMessage = $"Removed doodad {doodad.Type}.";
                logger.Info(removalMessage);
                PushHistory(removalMessage);
            }
        }

        private void AddBehaviorTrigger(int tileX, int tileY, string triggerName)
        {
            if (!map.InBounds(tileX, tileY))
            {
                const string outOfBoundsMessage = "Trigger placement out of bounds.";
                logger.Warn(outOfBoundsMessage);
                PushHistory(outOfBoundsMessage);
                return;
            }

            var trigger = new BehaviorTrigger
            {
                TileX = tileX,
                TileY = tileY,
                Name = triggerName
            };

            var existing = GetTriggerAt(tileX, tileY);
            if (existing != null)
                triggers.Remove(existing);

            triggers.Add(trigger);
            selectedTrigger = trigger;
            SelectTriggerInList(trigger);
            var addedTriggerMessage = $"Added behavior trigger '{triggerName}' at ({tileX}, {tileY}).";
            logger.Info(addedTriggerMessage);
            PushHistory($"Added trigger '{triggerName}' at ({tileX}, {tileY})");
            RenderMap();
            SyncMapFromEditorState();
        }

        private void RemoveBehaviorTrigger(BehaviorTrigger trigger)
        {
            if (!triggers.Remove(trigger))
            {
                var notFoundMessage = $"Trigger '{trigger.Name}' not found.";
                logger.Warn(notFoundMessage);
                PushHistory(notFoundMessage);
                return;
            }

            if (ReferenceEquals(selectedTrigger, trigger))
            {
                selectedTrigger = null;
                SelectTriggerInList(null);
            }

            var removalMessage = $"Removed behavior trigger '{trigger.Name}'.";
            logger.Info(removalMessage);
            PushHistory($"Removed trigger '{trigger.Name}'");
            RenderMap();
            SyncMapFromEditorState();
        }

    }

    }


