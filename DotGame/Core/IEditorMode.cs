using global::Avalonia;
using global::Avalonia.Media;

namespace Dotgame.Avalonia.Legacy;

[System.Obsolete("Legacy UI-side copy of IEditorMode. Use DotGame.Core.IEditorMode in DotGame.Core instead.")]
public interface IEditorMode
{
    void OnPointerDown(Point pos);
    void OnPointerMove(Point pos);
    void OnPointerUp(Point pos);
    void Render(DrawingContext ctx);
}
