using DotGame.Core.Systems;
using DotGame.Runtime.Rendering;

namespace DotGame.Runtime.Systems;

public interface IRuntimeEntitySystem : IEntitySystem
{
    void ApplyUpdateContext(in RuntimeUpdateContext context);

    void ApplyDrawContext(in RuntimeDrawContext context);
}

public abstract class RuntimeEntitySystemBase : EntitySystemBase, IRuntimeEntitySystem
{
    protected RuntimeUpdateContext UpdateContext { get; private set; }

    protected RuntimeDrawContext DrawContext { get; private set; }

    public virtual void ApplyUpdateContext(in RuntimeUpdateContext context)
    {
        UpdateContext = context;
    }

    public virtual void ApplyDrawContext(in RuntimeDrawContext context)
    {
        DrawContext = context;
    }
}
