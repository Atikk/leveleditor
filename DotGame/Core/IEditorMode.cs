using global::Avalonia;
using global::Avalonia.Media;

namespace DotGame.Core;

public interface IEditorMode
{
    void OnPointerDown(Point pos);
    void OnPointerMove(Point pos);
    void OnPointerUp(Point pos);
    void Render(DrawingContext ctx);
}
