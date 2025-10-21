namespace DotGame.Core.Platform;

public readonly struct WindowDescriptor
{
    public WindowDescriptor(string title, int width, int height, bool isResizable)
    {
        Title = title;
        Width = width;
        Height = height;
        IsResizable = isResizable;
    }

    public string Title { get; }

    public int Width { get; }

    public int Height { get; }

    public bool IsResizable { get; }
}
