using System;
using DotGame.Core.Entities;
using DotGame.Core.States;
using DotGame.Runtime.Components;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace DotGame.Runtime.Systems;

public sealed class SpriteAnimationSystem : RuntimeEntitySystemBase
{
    public override int Order => -25;

    public override void Update(GameClock clock, EntityWorld world)
    {
        var runtime = UpdateContext.Runtime;
        if (runtime is null)
            return;

        var content = runtime.Content;
        var deltaTime = (float)clock.Delta.TotalSeconds;
        if (deltaTime <= 0f)
            return;

        foreach (var entity in world.Entities)
        {
            if (!entity.TryGet(out SpriteAnimationComponent? sprite))
                continue;

            EnsureTextureLoaded(sprite, content);
            if (sprite.Texture is null)
                continue;

            sprite.FrameCount = Math.Max(1, sprite.FrameCount);
            sprite.FrameDuration = Math.Max(0.0001f, sprite.FrameDuration);

            if (sprite.Paused || sprite.FrameCount <= 1)
                continue;

            sprite.Accumulator += deltaTime;
            while (sprite.Accumulator >= sprite.FrameDuration)
            {
                sprite.Accumulator -= sprite.FrameDuration;
                sprite.CurrentFrame++;
                if (sprite.CurrentFrame >= sprite.FrameCount)
                {
                    if (sprite.Loop)
                    {
                        sprite.CurrentFrame = 0;
                    }
                    else
                    {
                        sprite.CurrentFrame = sprite.FrameCount - 1;
                        sprite.Paused = true;
                        break;
                    }
                }
            }
        }
    }

    private static void EnsureTextureLoaded(SpriteAnimationComponent sprite, ContentManager content)
    {
        if (sprite.Texture != null || string.IsNullOrWhiteSpace(sprite.TextureAsset))
            return;

        sprite.Texture = content.Load<Texture2D>(sprite.TextureAsset);
    }
}
