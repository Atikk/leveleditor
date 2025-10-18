using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Dotgame.Avalonia.Models;

namespace Dotgame.Avalonia.MonoGameLayer
{
    public interface ITextureResolver
    {
        Texture2D Resolve(string key);
    }

    public sealed class FileTextureResolver : ITextureResolver
    {
        private readonly AssetManager _assets;
        public FileTextureResolver(AssetManager assets) { _assets = assets; }
        public Texture2D Resolve(string key) => _assets.GetTexture(key);
    }

    public sealed class MapRenderer
    {
        private readonly SpriteBatch _sb;
        private readonly ITextureResolver _resolver;

        public MapRenderer(GraphicsDevice gfx, ITextureResolver resolver)
        {
            _sb = new SpriteBatch(gfx);
            _resolver = resolver;
        }

        public void Draw(Map map, Vector2 origin, Matrix? viewMatrix = null, bool includeActors = true)
        {
            var transform = viewMatrix ?? Matrix.Identity;
            _sb.Begin(samplerState: SamplerState.PointClamp, transformMatrix: transform);

            var tileset = map.Tileset;
            Texture2D? atlasTexture = null;
            if (map.HasTileIds && tileset != null)
            {
                var atlasKey = tileset.AbsoluteTexturePath ?? tileset.TextureKey;
                if (!string.IsNullOrWhiteSpace(atlasKey))
                {
                    atlasTexture = _resolver.Resolve(atlasKey);
                }
            }

            // Tiles (prefer shared atlas via tile IDs, fall back to data URLs)
            for (int y = 0; y < map.Rows; y++)
            {
                for (int x = 0; x < map.Cols; x++)
                {
                    var dst = new Rectangle((int)origin.X + x * map.TileW, (int)origin.Y + y * map.TileH, map.TileW, map.TileH);
                    var tileId = map.GetTileId(x, y);

                    if (atlasTexture != null && tileId.HasValue && tileset != null)
                    {
                        if (tileset.TryGetSourceRegion(tileId.Value, atlasTexture.Width, out var region))
                        {
                            var src = new Rectangle(region.X, region.Y, region.Width, region.Height);
                            _sb.Draw(atlasTexture, dst, src, Color.White);
                            continue;
                        }
                    }

                    var key = map.GetTileDataUrl(x, y);
                    if (string.IsNullOrEmpty(key))
                        continue;

                    var tex = _resolver.Resolve(key);
                    _sb.Draw(tex, dst, Color.White);
                }
            }

            if (includeActors)
            {
                // Doodads and Characters: render as colored rectangles for now (no texture path on models)
                foreach (var d in map.Doodads)
                {
                    var rect = map.TileRect(d.TileX, d.TileY);
                    var dst = new Rectangle((int)(origin.X + rect.X), (int)(origin.Y + rect.Y), (int)rect.Width, (int)rect.Height);
                    DrawFilledRect(dst, new Color(d.Color.R, d.Color.G, d.Color.B, d.Color.A));
                }

                foreach (var c in map.Characters)
                {
                    var rect = map.TileRect(c.TileX, c.TileY);
                    var dst = new Rectangle((int)(origin.X + rect.X), (int)(origin.Y + rect.Y), (int)rect.Width, (int)rect.Height);
                    DrawFilledRect(dst, new Color(c.Color.R, c.Color.G, c.Color.B, c.Color.A));
                }
            }

            _sb.End();
        }

        private void DrawFilledRect(Rectangle rect, Color color)
        {
            if (_pixel == null)
            {
                _pixel = new Texture2D(_sb.GraphicsDevice, 1, 1);
                _pixel.SetData(new[] { Microsoft.Xna.Framework.Color.White });
            }
            _sb.Draw(_pixel, rect, color);
        }

        private Texture2D? _pixel;
    }
}

