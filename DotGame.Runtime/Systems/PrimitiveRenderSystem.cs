using DotGame.Core.Entities;
using DotGame.Core.States;
using DotGame.Runtime.Components;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;

namespace DotGame.Runtime.Systems;

public sealed class PrimitiveRenderSystem : RuntimeEntitySystemBase
{
    public override int Order => 100;

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
            if (!entity.TryGet(out TransformComponent? transform) || !entity.TryGet(out PrimitiveRenderComponent? primitive))
                continue;

            var rectangle = new RectangleF(transform.Position, primitive.Size);
            spriteBatch.FillRectangle(rectangle, primitive.FillColor);
            if (primitive.OutlineColor is { } outline)
            {
                spriteBatch.DrawRectangle(rectangle, outline);
            }
        }
        spriteBatch.End();
    }
}
