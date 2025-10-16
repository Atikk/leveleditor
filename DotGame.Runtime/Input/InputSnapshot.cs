using Microsoft.Xna.Framework.Input;

namespace DotGame.Runtime.Input;

public readonly record struct InputSnapshot(KeyboardState Keyboard, MouseState Mouse, GamePadState GamePad, bool GamePadConnected)
{
    public bool IsKeyDown(Keys key) => Keyboard.IsKeyDown(key);

    public bool IsKeyUp(Keys key) => Keyboard.IsKeyUp(key);

    public ButtonState LeftButton => Mouse.LeftButton;

    public ButtonState RightButton => Mouse.RightButton;
}
