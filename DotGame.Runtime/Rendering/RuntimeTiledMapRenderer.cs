using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using DotGame.Runtime.Content;

namespace DotGame.Runtime.Rendering;

public static class RuntimeTiledMapRenderer
{
    public static void DrawTileLayers(SpriteBatch spriteBatch, RuntimeTiledMap map)
    {
        if (spriteBatch == null)
        {
            throw new ArgumentNullException(nameof(spriteBatch));
        }

        if (map == null)
        {
            throw new ArgumentNullException(nameof(map));
        }

        var tiledMap = map.Map;
        foreach (var layer in map.TileLayers)
        {
            if (layer?.data == null || layer.data.Length == 0)
            {
                continue;
            }

            if (!layer.visible)
            {
                continue;
            }

            var layerWidth = Math.Max(1, layer.width);
            for (var index = 0; index < layer.data.Length; index++)
            {
                var rawGid = layer.data[index];
                if (!map.TryResolveTile(rawGid, out var sprite, out var flipFlags))
                {
                    continue;
                }

                var tileX = index % layerWidth;
                var tileY = index / layerWidth;
                var position = new Vector2(tileX * tiledMap.TileWidth, tileY * tiledMap.TileHeight);

                var effects = SpriteEffects.None;
                if ((flipFlags & RuntimeTiledMap.TileFlipFlags.Horizontal) != 0)
                {
                    effects |= SpriteEffects.FlipHorizontally;
                }

                if ((flipFlags & RuntimeTiledMap.TileFlipFlags.Vertical) != 0)
                {
                    effects |= SpriteEffects.FlipVertically;
                }

                var rotation = 0f;
                var origin = Vector2.Zero;
                if ((flipFlags & RuntimeTiledMap.TileFlipFlags.Diagonal) != 0)
                {
                    // TODO: diagonal flips need precise rotation handling when combined with other flags.
                    rotation = MathHelper.PiOver2;
                    origin = new Vector2(sprite.Source.Width, 0f);
                }

                spriteBatch.Draw(sprite.Texture, position, sprite.Source, Color.White, rotation, origin, 1f, effects, 0f);
            }
        }
    }
}
