using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TiledCS;

namespace DotGame.Runtime.Content;

public sealed class RuntimeTiledMap : IDisposable
{
    private readonly List<Texture2D> _tilesetTextures = new();
    private readonly Dictionary<int, TileSprite> _tileLookup = new();
    private readonly List<TiledLayer> _tileLayers = new();
    private readonly List<TiledLayer> _objectLayers = new();

    public RuntimeTiledMap(GraphicsDevice graphicsDevice, string mapPath)
    {
        if (graphicsDevice is null)
        {
            throw new ArgumentNullException(nameof(graphicsDevice));
        }

        if (string.IsNullOrWhiteSpace(mapPath))
        {
            throw new ArgumentException("Map path must be provided.", nameof(mapPath));
        }

        MapPath = Path.GetFullPath(mapPath);
        MapDirectory = Path.GetDirectoryName(MapPath) ?? Directory.GetCurrentDirectory();
        Map = new TiledMap(MapPath);

        LoadTilesets(graphicsDevice);
        CategorizeLayers();
    }

    public TiledMap Map { get; }
    public string MapPath { get; }
    public string MapDirectory { get; }
    public IReadOnlyList<TiledLayer> TileLayers => _tileLayers;
    public IReadOnlyList<TiledLayer> ObjectLayers => _objectLayers;
    public int PixelWidth => Map.Width * Map.TileWidth;
    public int PixelHeight => Map.Height * Map.TileHeight;

    internal bool TryResolveTile(int rawGid, out TileSprite sprite, out TileFlipFlags flipFlags)
    {
        flipFlags = TileFlipFlags.None;

        if (rawGid == 0)
        {
            sprite = default;
            return false;
        }

        var flags = DecodeFlipFlags(rawGid, out var gid);
        flipFlags = flags;
        return _tileLookup.TryGetValue(gid, out sprite);
    }

    private void LoadTilesets(GraphicsDevice graphicsDevice)
    {
        var tilesetDefinitions = Map.Tilesets ?? Array.Empty<TiledMapTileset>();
        var loadedTilesets = Map.GetTiledTilesets(MapDirectory);

        foreach (var mapTileset in tilesetDefinitions)
        {
            if (!loadedTilesets.TryGetValue(mapTileset.firstgid, out var tileset))
            {
                continue;
            }

            var tilesetDirectory = MapDirectory;
            if (!string.IsNullOrEmpty(mapTileset.source))
            {
                var tsxPath = Path.Combine(MapDirectory, mapTileset.source);
                tilesetDirectory = Path.GetDirectoryName(tsxPath) ?? tilesetDirectory;
            }

            var imagePath = string.IsNullOrEmpty(tileset.Image)
                ? string.Empty
                : Path.Combine(tilesetDirectory, tileset.Image);

            if (!File.Exists(imagePath))
            {
                continue;
            }

            var texture = LoadTexture(graphicsDevice, imagePath);
            _tilesetTextures.Add(texture);

            var columns = Math.Max(1, tileset.Columns);
            var totalTiles = Math.Max(tileset.TileCount, columns);

            for (var localId = 0; localId < totalTiles; localId++)
            {
                var gid = mapTileset.firstgid + localId;
                var column = localId % columns;
                var row = localId / columns;
                var sx = tileset.Margin + column * (tileset.TileWidth + tileset.Spacing);
                var sy = tileset.Margin + row * (tileset.TileHeight + tileset.Spacing);
                var source = new Rectangle(sx, sy, tileset.TileWidth, tileset.TileHeight);

                if (!_tileLookup.ContainsKey(gid))
                {
                    _tileLookup.Add(gid, new TileSprite(texture, source));
                }
            }
        }
    }

    private static Texture2D LoadTexture(GraphicsDevice device, string path)
    {
        using var stream = File.OpenRead(path);
        return Texture2D.FromStream(device, stream);
    }

    private void CategorizeLayers()
    {
        if (Map.Layers == null)
        {
            return;
        }

        foreach (var layer in Map.Layers)
        {
            if (string.Equals(layer.type, "tilelayer", StringComparison.OrdinalIgnoreCase))
            {
                _tileLayers.Add(layer);
            }
            else if (string.Equals(layer.type, "objectgroup", StringComparison.OrdinalIgnoreCase))
            {
                _objectLayers.Add(layer);
            }
        }
    }

    private static TileFlipFlags DecodeFlipFlags(int rawGid, out int gid)
    {
        const uint horizontal = 0x8000_0000;
        const uint vertical = 0x4000_0000;
        const uint diagonal = 0x2000_0000;

        var raw = (uint)rawGid;
        var flags = TileFlipFlags.None;

        if ((raw & horizontal) != 0)
        {
            flags |= TileFlipFlags.Horizontal;
        }

        if ((raw & vertical) != 0)
        {
            flags |= TileFlipFlags.Vertical;
        }

        if ((raw & diagonal) != 0)
        {
            flags |= TileFlipFlags.Diagonal;
        }

        var mask = ~(horizontal | vertical | diagonal);
        gid = (int)(raw & mask);
        return flags;
    }

    public void Dispose()
    {
        foreach (var texture in _tilesetTextures)
        {
            texture.Dispose();
        }

        _tilesetTextures.Clear();
        _tileLookup.Clear();
        _tileLayers.Clear();
        _objectLayers.Clear();
    }

    internal readonly struct TileSprite
    {
        public TileSprite(Texture2D texture, Rectangle source)
        {
            Texture = texture;
            Source = source;
        }

        public Texture2D Texture { get; }
        public Rectangle Source { get; }
    }

    [Flags]
    internal enum TileFlipFlags
    {
        None = 0,
        Horizontal = 1,
        Vertical = 2,
        Diagonal = 4
    }
}
