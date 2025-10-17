using System;
using System.Collections.Concurrent;
using System.IO;
using DotGame.Core.Resources;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using DotGame.Runtime.Content;

namespace DotGameAvalonia.MonoGameLayer
{
    public sealed class AssetManager
    {
        private readonly GraphicsDevice _gfx;
        private readonly ContentManager _content;
    private readonly ConcurrentDictionary<string, Texture2D> _runtimeTextures = new();
    private readonly ConcurrentDictionary<string, Texture2D> _contentTextures = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, RuntimeTiledMap> _runtimeTiledMaps = new(StringComparer.OrdinalIgnoreCase);
    private readonly ResourceManager? _resourceManager;

        public AssetManager(ContentManager content, GraphicsDevice gfx, ResourceManager? resourceManager = null)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _gfx = gfx ?? throw new ArgumentNullException(nameof(gfx));
            _resourceManager = resourceManager;
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

        public RuntimeTiledMap GetRuntimeTiledMap(string assetName)
        {
            if (string.IsNullOrWhiteSpace(assetName))
                throw new ArgumentException("Asset name must be provided.", nameof(assetName));

            var normalized = NormalizeAssetKey(assetName);
            if (_runtimeTiledMaps.TryGetValue(normalized, out var cached))
            {
                return cached;
            }

            var path = ResolveMapPath(normalized);
            var map = new RuntimeTiledMap(_gfx, path);
            _runtimeTiledMaps[normalized] = map;
            return map;
        }

        public void RequestRuntimeTiledMap(string assetName, Action<RuntimeTiledMap?> onLoaded, Action<Exception>? onError = null)
        {
            if (string.IsNullOrWhiteSpace(assetName))
            {
                onLoaded?.Invoke(null);
                return;
            }

            var normalized = NormalizeAssetKey(assetName);

            if (_runtimeTiledMaps.TryGetValue(normalized, out var cached))
            {
                onLoaded?.Invoke(cached);
                return;
            }

            if (_resourceManager == null)
            {
                try
                {
                    var map = GetRuntimeTiledMap(assetName);
                    onLoaded?.Invoke(map);
                }
                catch (Exception ex)
                {
                    onError?.Invoke(ex);
                }

                return;
            }

            _resourceManager.LoadAsync(
                key: $"tiledmap:{normalized}",
                loader: _ => new RuntimeTiledMap(_gfx, ResolveMapPath(normalized)),
                onCompleted: h =>
                {
                    try
                    {
                        var map = h.Value;
                        _runtimeTiledMaps[normalized] = map;
                        onLoaded?.Invoke(map);
                    }
                    finally
                    {
                        _resourceManager.Release(h);
                    }
                },
                onFailed: h =>
                {
                    try
                    {
                        var ex = h.Exception ?? new InvalidOperationException($"Failed to load tiled map '{normalized}'.");
                        onError?.Invoke(ex);
                    }
                    finally
                    {
                        _resourceManager.Release(h);
                    }
                });
        }

        public void Clear()
        {
            foreach (var kv in _runtimeTextures)
                kv.Value.Dispose();

            _runtimeTextures.Clear();
            foreach (var kvp in _runtimeTiledMaps)
            {
                kvp.Value.Dispose();
            }

            _runtimeTiledMaps.Clear();
            _content.Unload();
            _contentTextures.Clear();
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

        private string ResolveMapPath(string normalizedAsset)
        {
            var candidate = normalizedAsset;
            if (!Path.HasExtension(candidate))
            {
                candidate += ".tmx";
            }

            if (Path.IsPathRooted(candidate) && File.Exists(candidate))
            {
                return candidate;
            }

            var baseDirectory = AppContext.BaseDirectory ?? Environment.CurrentDirectory;

            var resolved = Path.Combine(baseDirectory, candidate.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(resolved))
            {
                return resolved;
            }

            resolved = Path.Combine(baseDirectory, "Content", candidate.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(resolved))
            {
                return resolved;
            }

            resolved = Path.Combine(baseDirectory, "Content", normalizedAsset.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(resolved))
            {
                return resolved;
            }

            throw new FileNotFoundException($"Unable to locate tiled map asset '{normalizedAsset}'.", normalizedAsset);
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