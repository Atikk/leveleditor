using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using SkiaSharp;
using global::Avalonia.Media.Imaging;

namespace Dotgame.Avalonia.Views
{
    // Moved out of EditorWindow to allow reuse and separation of concerns.
    public sealed class TileEntry
    {
        public Bitmap Bitmap { get; }
        public string? SourceKey { get; }
        public string? DataUrl { get; }
        public string? SerializedValueOverride { get; }
        public int? TileId { get; }
        public string? TilesetKey { get; }

        public TileEntry(
            Bitmap bitmap,
            string? sourceKey,
            string? dataUrl = null,
            string? serializedValueOverride = null,
            int? tileId = null,
            string? tilesetKey = null)
        {
            Bitmap = bitmap;
            SourceKey = sourceKey;
            DataUrl = dataUrl;
            SerializedValueOverride = serializedValueOverride;
            TileId = tileId;
            TilesetKey = tilesetKey;
        }

        public bool TryGetTileId(out int id)
        {
            if (TileId.HasValue)
            {
                id = TileId.Value;
                return true;
            }

            id = default;
            return false;
        }

        public string GetSerializedValue()
        {
            if (TileId.HasValue)
                return TileId.Value.ToString(CultureInfo.InvariantCulture);

            if (!string.IsNullOrWhiteSpace(SerializedValueOverride))
                return SerializedValueOverride!;

            if (!string.IsNullOrWhiteSpace(SourceKey))
                return SourceKey!;

            if (!string.IsNullOrEmpty(DataUrl))
                return DataUrl!;

            using var ms = new MemoryStream();
            Bitmap.Save(ms);
            var base64 = Convert.ToBase64String(ms.ToArray());
            return "data:image/png;base64," + base64;
        }

        public object? GetSerializableValue(bool preferTileIds)
        {
            if (preferTileIds && TileId.HasValue)
                return TileId.Value;

            return GetSerializedValue();
        }

        public TileEntry Clone()
        {
            using var ms = new MemoryStream();
            Bitmap.Save(ms);
            ms.Position = 0;
            var cloneBmp = Dotgame.Avalonia.Models.AssetManager.Instance.LoadBitmapFromStream(ms);
            return new TileEntry(cloneBmp, SourceKey, DataUrl, SerializedValueOverride, TileId, TilesetKey);
        }

        public static TileEntry FromDataUrl(string dataUrl)
        {
            int comma = dataUrl.IndexOf(',');
            string base64 = comma >= 0 ? dataUrl[(comma + 1)..] : dataUrl;
            byte[] bytes = Convert.FromBase64String(base64);
            using var ms = new MemoryStream(bytes);
            var bmp = Dotgame.Avalonia.Models.AssetManager.Instance.LoadBitmapFromStream(ms);
            return new TileEntry(bmp, null, dataUrl, dataUrl);
        }
    }

    public sealed class LayerState : System.ComponentModel.INotifyPropertyChanged
    {
        private string name;
        private bool isVisible = true;
        private double opacity = 1.0;

        public LayerState(string id, string name, TileEntry?[,] tiles)
        {
            Id = id;
            this.name = name;
            Tiles = tiles;
        }

        public string Id { get; }

        public string Name
        {
            get => name;
            set
            {
                if (name != value)
                {
                    name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        public bool IsVisible
        {
            get => isVisible;
            set
            {
                if (isVisible != value)
                {
                    isVisible = value;
                    OnPropertyChanged(nameof(IsVisible));
                }
            }
        }

        public double Opacity
        {
            get => opacity;
            set
            {
                var clamped = Math.Clamp(value, 0.0, 1.0);
                if (Math.Abs(opacity - clamped) > double.Epsilon)
                {
                    opacity = clamped;
                    OnPropertyChanged(nameof(Opacity));
                }
            }
        }

        public TileEntry?[,] Tiles { get; set; }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

        public override string ToString() => Name;
    }

    public sealed class TilesetState : IDisposable
    {
        private readonly Dotgame.Avalonia.Models.TilesetReference reference;
        private readonly Func<SKBitmap> atlasFactory;
        private readonly Dictionary<int, TileEntry> paletteCache = new();
        private SKBitmap? atlas;
        private bool disposed;

        private TilesetState(Dotgame.Avalonia.Models.TilesetReference reference, Func<SKBitmap> atlasFactory)
        {
            this.reference = reference ?? throw new ArgumentNullException(nameof(reference));
            this.atlasFactory = atlasFactory ?? throw new ArgumentNullException(nameof(atlasFactory));
        }

        public static TilesetState FromSpriteSheet(Dotgame.Avalonia.Models.TilesetReference reference, Bitmap spriteSheet)
        {
            if (spriteSheet == null) throw new ArgumentNullException(nameof(spriteSheet));
            return new TilesetState(reference, () => CloneToSkBitmap(spriteSheet));
        }

        public static TilesetState FromReference(Dotgame.Avalonia.Models.TilesetReference reference)
        {
            return new TilesetState(reference, () => LoadSkBitmap(reference));
        }

        public Dotgame.Avalonia.Models.TilesetReference Reference => reference;

        public TileEntry CreateTileEntry(int tileId)
        {
            var baseEntry = GetOrCreatePaletteEntry(tileId);
            return baseEntry.Clone();
        }

        public TileEntry GetPaletteEntry(int tileId)
        {
            return GetOrCreatePaletteEntry(tileId);
        }

        public IEnumerable<int> EnumerateTileIds()
        {
            var atlasBitmap = EnsureAtlas();
            var columns = GetEffectiveColumns(atlasBitmap.Width);
            if (columns <= 0)
                yield break;

            var tileCount = reference.TileCount;
            if (tileCount <= 0)
            {
                var rows = GetEffectiveRows(columns, atlasBitmap.Height);
                tileCount = Math.Max(0, columns * rows);
            }

            for (var i = 0; i < tileCount; i++)
                yield return reference.FirstId + i;
        }

        public Dotgame.Avalonia.Models.TilesetReference CreateReferenceForMap() => reference;

        public object CreateSerializableDto(string? mapDirectory)
        {
            var atlasBitmap = EnsureAtlas();
            var columns = GetEffectiveColumns(atlasBitmap.Width);
            var rows = GetEffectiveRows(columns, atlasBitmap.Height);
            var tileCount = reference.TileCount > 0 ? reference.TileCount : columns * rows;
            var texture = GetTextureKeyForSave(mapDirectory);

            return new
            {
                texture,
                columns,
                tileCount,
                margin = reference.Margin,
                spacing = reference.Spacing,
                firstId = reference.FirstId,
                tileWidth = reference.TileWidth,
                tileHeight = reference.TileHeight
            };
        }

        public string GetTextureKeyForSave(string? mapDirectory)
        {
            var absolute = reference.AbsoluteTexturePath;
            if (!string.IsNullOrWhiteSpace(mapDirectory) && !string.IsNullOrWhiteSpace(absolute))
            {
                try
                {
                    var rel = Path.GetRelativePath(mapDirectory, absolute);
                    if (!string.IsNullOrWhiteSpace(rel) && !rel.StartsWith(".."))
                        return rel.Replace('\\', '/');
                }
                catch
                {
                    // fallback to texture key
                }
            }

            return reference.TextureKey;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            if (atlas != null)
            {
                atlas.Dispose();
                atlas = null;
            }
        }

        private TileEntry GetOrCreatePaletteEntry(int tileId)
        {
            if (paletteCache.TryGetValue(tileId, out var cached))
                return cached;

            var atlasBitmap = EnsureAtlas();
            if (!reference.TryGetSourceRegion(tileId, atlasBitmap.Width, out var region))
                throw new InvalidOperationException($"Tileset '{reference.TextureKey}' does not contain tile id {tileId}.");

            using var surface = SKSurface.Create(new SKImageInfo(region.Width, region.Height));
            var srcRect = new SKRectI(region.X, region.Y, region.X + region.Width, region.Y + region.Height);
            var destRect = new SKRectI(0, 0, region.Width, region.Height);
            surface.Canvas.DrawBitmap(atlasBitmap, srcRect, destRect);
            using var snapshot = surface.Snapshot();
            using var data = snapshot.Encode(SKEncodedImageFormat.Png, 100);
            using var ms = new MemoryStream(data.ToArray());
            var bitmap = Dotgame.Avalonia.Models.AssetManager.Instance.LoadBitmapFromStream(ms);
            var entry = new TileEntry(bitmap, null, tileId: tileId, tilesetKey: reference.TextureKey);
            paletteCache[tileId] = entry;
            return entry;
        }

        private SKBitmap EnsureAtlas()
        {
            if (atlas != null)
                return atlas;

            atlas = atlasFactory();
            if (atlas == null)
                throw new InvalidOperationException($"Failed to load tileset texture '{reference.TextureKey}'.");
            return atlas;
        }

        private int GetEffectiveColumns(int atlasWidth)
        {
            if (reference.TileWidth <= 0)
                return 0;

            if (reference.Columns > 0)
                return reference.Columns;

            var denom = reference.TileWidth + reference.Spacing;
            if (denom <= 0)
                return 0;

            var usableWidth = atlasWidth - reference.Margin * 2 + reference.Spacing;
            return Math.Max(1, usableWidth / denom);
        }

        private int GetEffectiveRows(int columns, int atlasHeight)
        {
            if (columns <= 0 || reference.TileHeight <= 0)
                return 0;

            if (reference.TileCount > 0 && columns > 0)
                return Math.Max(1, (int)Math.Ceiling(reference.TileCount / (double)columns));

            var denom = reference.TileHeight + reference.Spacing;
            if (denom <= 0)
                return 0;

            var usableHeight = atlasHeight - reference.Margin * 2 + reference.Spacing;
            return Math.Max(1, usableHeight / denom);
        }

        private static SKBitmap CloneToSkBitmap(Bitmap bitmap)
        {
            using var ms = new MemoryStream();
            bitmap.Save(ms);
            ms.Position = 0;
            return SKBitmap.Decode(ms);
        }

        private static SKBitmap LoadSkBitmap(Dotgame.Avalonia.Models.TilesetReference reference)
        {
            if (!string.IsNullOrWhiteSpace(reference.AbsoluteTexturePath) && File.Exists(reference.AbsoluteTexturePath))
                return SKBitmap.Decode(reference.AbsoluteTexturePath);

            if (!string.IsNullOrWhiteSpace(reference.TextureKey) && File.Exists(reference.TextureKey))
                return SKBitmap.Decode(reference.TextureKey);

            var bitmap = Dotgame.Avalonia.Models.AssetManager.Instance.LoadBitmap(reference.TextureKey);
            return CloneToSkBitmap(bitmap);
        }
    }
}
