using global::Avalonia;
using global::Avalonia.Media;
using DotGame.Core;

namespace Dotgame.Avalonia.Legacy;

[System.Obsolete("Legacy UI-side copy of TileMode. Use DotGame.Core.TileMode in DotGame.Core instead.")]
public class TileMode : IEditorMode
{
    public void OnPointerDown(Point pos)
    {
        // TODO: Implement tile-specific pointer down logic
    }

    public void OnPointerMove(Point pos)
    {
        // TODO: Implement tile-specific pointer move logic
    }

    public void OnPointerUp(Point pos)
    {
        // TODO: Implement tile-specific pointer up logic
    }

    public void Render(DrawingContext ctx)
    {
        // TODO: Implement tile-specific rendering logic
    }
}
