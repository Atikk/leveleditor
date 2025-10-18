using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;

namespace Dotgame.Avalonia.Models
{
    public class Doodad
    {
        public int TileX { get; set; }
        public int TileY { get; set; }
        public string Type { get; set; }
        public Bitmap? Sprite { get; set; }
        public Color Color { get; set; } = Colors.Transparent;
        public bool Collidable { get; set; } = false;
        public bool Interactable { get; set; } = false;
        public string? OnInteract { get; set; }
        public bool Animated { get; set; } = false;
        public string? Trigger { get; set; }

        public Doodad(int tileX, int tileY, string type)
        {
            TileX = tileX;
            TileY = tileY;
            Type = type;
        }

        public override string ToString() => $"{Type} @ {TileX},{TileY}";
    }
}

