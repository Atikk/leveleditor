using System;

namespace DotGame.Core.Entities
{
    public enum Facing { Down, Left, Right, Up }

    public enum CharacterClass { Warrior, Mage, Thief }

    public sealed class Character
    {
        public string Name { get; set; } = string.Empty;
        public int TileX { get; set; }
        public int TileY { get; set; }
        public CharacterClass Class { get; set; } = CharacterClass.Warrior;

        public Character() { }

        public Character(string name, int x, int y, CharacterClass cls = CharacterClass.Warrior)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            TileX = x;
            TileY = y;
            Class = cls;
        }

        public void MoveBy(int dx, int dy)
        {
            TileX += dx;
            TileY += dy;
        }
    }
}
