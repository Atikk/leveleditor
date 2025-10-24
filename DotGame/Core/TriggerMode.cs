using global::Avalonia;
using global::Avalonia.Media;
using DotGame.Core;

namespace Dotgame.Avalonia.Legacy;

[System.Obsolete("Legacy UI-side copy of TriggerMode. Use DotGame.Core.TriggerMode in DotGame.Core instead.")]
public sealed class TriggerMode
{
    public void OnPointerDown(Point pos)
    {
        // TODO: Implement trigger-specific pointer down logic
    }

    public void OnPointerMove(Point pos)
    {
        // TODO: Implement trigger-specific pointer move logic
    }

    public void OnPointerUp(Point pos)
    {
        // TODO: Implement trigger-specific pointer up logic
    }

    public void Render(DrawingContext ctx)
    {
        // TODO: Implement trigger-specific rendering logic
    }
}
