using global::Avalonia;
using global::Avalonia.Media;

using DotGame.Core;

namespace Dotgame.Avalonia.Legacy;

[System.Obsolete("Legacy UI-side copy of CharacterMode. Use DotGame.Core.CharacterMode in DotGame.Core instead.")]
public class CharacterMode : IEditorMode
{
    public void OnPointerDown(Point pos)
    {
        // TODO: Implement character-specific pointer down logic
    }

    public void OnPointerMove(Point pos)
    {
        // TODO: Implement character-specific pointer move logic
    }

    public void OnPointerUp(Point pos)
    {
        // TODO: Implement character-specific pointer up logic
    }

    public void Render(DrawingContext ctx)
    {
        // TODO: Implement character-specific rendering logic
    }
}
