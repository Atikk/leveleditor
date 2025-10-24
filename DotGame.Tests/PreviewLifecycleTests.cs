using System;
using Dotgame.Avalonia.Views;
using Xunit;

namespace DotGame.Tests
{
    public class FakePreviewGame : EditorWindow.IPreviewGame
    {
        public int SwapRequests { get; private set; }

        public void RequestMapSwap(Dotgame.Avalonia.Models.Map mapSnapshot)
        {
            if (mapSnapshot == null)
                throw new ArgumentNullException(nameof(mapSnapshot));

            SwapRequests++;
        }
    }

    public class PreviewLifecycleTests
    {
        [Fact]
        public void TryRequestPreviewMap_AllowsMultipleSwapsWithoutException()
        {
            var fake = new FakePreviewGame();
            var map = new Dotgame.Avalonia.Models.Map();

            for (int i = 0; i < 5; i++)
            {
                bool ok = EditorWindow.TryRequestPreviewMap(fake, map, out var err);
                Assert.True(ok, err);
            }

            Assert.Equal(5, fake.SwapRequests);
        }
    }
}
