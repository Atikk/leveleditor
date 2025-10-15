using System;
using System.Collections.Concurrent;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace DotGameAvalonia.MonoGameLayer
{
    public sealed class AssetManager
    {
        private readonly GraphicsDevice _gfx;
        private readonly ConcurrentDictionary<string, Texture2D> _textures = new();

        public AssetManager(GraphicsDevice gfx)
        {
            _gfx = gfx;
        }

        public Texture2D GetTexture(string key)
        {
            if (_textures.TryGetValue(key, out var tex)) return tex;

            tex = key.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                ? CreateTextureFromDataUrl(key)
                : CreateTextureFromFile(key);

            _textures[key] = tex;
            return tex;
        }

        public void Clear()
        {
            foreach (var kv in _textures)
                kv.Value.Dispose();
            _textures.Clear();
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