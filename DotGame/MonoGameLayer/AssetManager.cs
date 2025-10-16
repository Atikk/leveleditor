using System;
using System.Collections.Concurrent;
using System.IO;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Tiled;

namespace DotGameAvalonia.MonoGameLayer
{
    public sealed class AssetManager
    {
        private readonly GraphicsDevice _gfx;
        private readonly ContentManager _content;
    private readonly ConcurrentDictionary<string, Texture2D> _runtimeTextures = new();
        private readonly ConcurrentDictionary<string, Texture2D> _contentTextures = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, TiledMap> _tiledMaps = new(StringComparer.OrdinalIgnoreCase);

        public AssetManager(ContentManager content, GraphicsDevice gfx)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _gfx = gfx ?? throw new ArgumentNullException(nameof(gfx));
        }

        public Texture2D GetTexture(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Texture key must be provided.", nameof(key));

            if (IsDataUrl(key) || LooksLikeFilePath(key))
            {
                if (_runtimeTextures.TryGetValue(key, out var cached))
                    return cached;

                var texture = IsDataUrl(key)
                    ? CreateTextureFromDataUrl(key)
                    : CreateTextureFromFile(key);

                _runtimeTextures[key] = texture;
                return texture;
            }

            var normalized = NormalizeAssetKey(key);

            if (_contentTextures.TryGetValue(normalized, out var cachedContentTex))
                return cachedContentTex;

            var contentTexture = _content.Load<Texture2D>(normalized);
            _contentTextures[normalized] = contentTexture;
            return contentTexture;
        }

        public TiledMap GetTiledMap(string assetName)
        {
            if (string.IsNullOrWhiteSpace(assetName))
                throw new ArgumentException("Asset name must be provided.", nameof(assetName));

            var normalized = NormalizeAssetKey(assetName);

            if (_tiledMaps.TryGetValue(normalized, out var cached))
                return cached;

            var map = _content.Load<TiledMap>(normalized);
            _tiledMaps[normalized] = map;
            return map;
        }

        public void Clear()
        {
            foreach (var kv in _runtimeTextures)
                kv.Value.Dispose();

            _runtimeTextures.Clear();
            _content.Unload();
            _contentTextures.Clear();

            _tiledMaps.Clear();
        }

        private static bool IsDataUrl(string key) => key.StartsWith("data:", StringComparison.OrdinalIgnoreCase);

        private static bool LooksLikeFilePath(string key)
        {
            try
            {
                if (!Path.HasExtension(key))
                    return false;

                var resolved = Path.IsPathRooted(key)
                    ? key
                    : Path.GetFullPath(key, AppContext.BaseDirectory ?? Environment.CurrentDirectory);

                return File.Exists(resolved);
            }
            catch
            {
                return false;
            }
        }

        private static string NormalizeAssetKey(string key)
        {
            return key.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        }

        private Texture2D CreateTextureFromFile(string path)
        {
            var resolved = Path.IsPathRooted(path)
                ? path
                : Path.GetFullPath(path, AppContext.BaseDirectory ?? Environment.CurrentDirectory);

            using var fs = File.OpenRead(resolved);
            return Texture2D.FromStream(_gfx, fs);
        }

        private Texture2D CreateTextureFromDataUrl(string dataUrl)
        {
            var comma = dataUrl.IndexOf(',');
            var base64 = comma >= 0 ? dataUrl[(comma + 1)..] : dataUrl;
            var bytes = Convert.FromBase64String(base64);
            using var ms = new MemoryStream(bytes);
            return Texture2D.FromStream(_gfx, ms);
        }
    }
}