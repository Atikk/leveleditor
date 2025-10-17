using System;
using DotGame.Core.Entities;
using DotGame.Core.States;
using DotGame.Runtime.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DotGame.Runtime.Systems;

public sealed class SpriteRenderSystem : RuntimeEntitySystemBase
{
    public override int Order => 50;

    public override void Draw(GameClock clock, EntityWorld world)
    {
        var runtime = DrawContext.Runtime;
        if (runtime is null)
            return;

        var spriteBatch = runtime.SpriteBatch;
        var viewMatrix = runtime.Camera.GetViewMatrix();

        spriteBatch.Begin(transformMatrix: viewMatrix, samplerState: SamplerState.PointClamp);
        foreach (var entity in world.Entities)
        {
            if (!entity.TryGet(out TransformComponent? transform) ||
                !entity.TryGet(out SpriteAnimationComponent? sprite) ||
                sprite.Texture is null)
            {
                continue;
            }

            var texture = sprite.Texture;
            var frameCount = Math.Max(1, sprite.FrameCount);
            var frameSize = sprite.FrameSize;
            if (frameSize.X <= 0)
                frameSize.X = texture.Width / frameCount;
            if (frameSize.Y <= 0)
                frameSize.Y = texture.Height;

            var currentFrame = sprite.CurrentFrame;
            if (currentFrame >= frameCount)
                currentFrame = frameCount - 1;

            var sourceRectangle = new Rectangle(frameSize.X * currentFrame, 0, frameSize.X, frameSize.Y);
            var rotation = transform.Rotation;
            var scale = transform.Scale;
            if (scale == Vector2.Zero)
                scale = Vector2.One;

            var origin = sprite.Origin;
            if (origin == Vector2.Zero)
                origin = new Vector2(frameSize.X * 0.5f, frameSize.Y * 0.5f);

            spriteBatch.Draw(texture,
                transform.Position,
                sourceRectangle,
                sprite.Tint,
                rotation,
                origin,
                scale,
                sprite.Effects,
                0f);
        }

        spriteBatch.End();
    }
}
