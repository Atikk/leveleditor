using Dotgame.Avalonia.Models;
using Dotgame.Avalonia.Services;

namespace DotGame.Tests
{
    // xUnit collection fixture to swap the AssetManager's bitmap factory for tests
    public class BitmapFactoryFixture : System.IDisposable
    {
        private readonly FakeBitmapFactory fakeFactory = new FakeBitmapFactory();

        public BitmapFactoryFixture()
        {
            // install the fake factory into AssetManager
            AssetManager.Instance.SetBitmapFactory(fakeFactory);
        }

        public void Dispose()
        {
            // restore default factory
            AssetManager.Instance.ResetBitmapFactory();
        }
    }

    [Xunit.CollectionDefinition("BitmapFactory collection")]
    public class BitmapFactoryCollection : Xunit.ICollectionFixture<BitmapFactoryFixture>
    {
        // collection definition for tests that require bitmap factory swapping
    }
}
