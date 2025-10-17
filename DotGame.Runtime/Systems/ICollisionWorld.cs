using System.Collections.Generic;
using MonoGame.Extended;

namespace DotGame.Runtime.Systems;

public interface ICollisionWorld
{
    RectangleF WorldBounds { get; }

    IReadOnlyList<RectangleF> StaticColliders { get; }
}
