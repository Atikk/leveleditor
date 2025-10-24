using Xunit;
using DotGame.Core.Entities;

namespace DotGame.Core.Tests
{
    public class CharacterTests
    {
        [Fact]
        public void CreateAndMoveCharacter()
        {
            var c = new Character("Hero", 2, 3, CharacterClass.Mage);
            Assert.Equal("Hero", c.Name);
            Assert.Equal(2, c.TileX);
            Assert.Equal(3, c.TileY);
            Assert.Equal(CharacterClass.Mage, c.Class);

            c.MoveBy(1, -1);
            Assert.Equal(3, c.TileX);
            Assert.Equal(2, c.TileY);
        }
    }
}
