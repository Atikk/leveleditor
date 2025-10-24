using System;
using Dotgame.Avalonia.Views;
using Xunit;

namespace DotGame.Tests
{
    public class FakePreviewGame2 : EditorWindow.IPreviewGame
    {
        public int SwapRequests { get; private set; }

        public void RequestMapSwap(Dotgame.Avalonia.Models.Map mapSnapshot)
        {
            if (mapSnapshot == null)
                throw new ArgumentNullException(nameof(mapSnapshot));

            SwapRequests++;
        }
    }

    public class PreviewLifecycleTests2
    {
        [Fact]
        public void NotifyPreviewMapUpdateForTest_RequestsSwap_WhenNotUsingFallback()
        {
            var fake = new FakePreviewGame2();
            var map = new Dotgame.Avalonia.Models.Map();

            bool ok = EditorWindow.NotifyPreviewMapUpdateForTest(fake, map, usingFallbackPreviewMap: false, shouldUseFallbackPreviewMap: () => false, out var err);
            Assert.True(ok, err);
            Assert.Equal(1, fake.SwapRequests);
        }

        [Fact]
        public void NotifyPreviewMapUpdateForTest_SkipsSwap_WhenUsingFallbackAndStillRequired()
        {
            var fake = new FakePreviewGame2();
            var map = new Dotgame.Avalonia.Models.Map();

            bool ok = EditorWindow.NotifyPreviewMapUpdateForTest(fake, map, usingFallbackPreviewMap: true, shouldUseFallbackPreviewMap: () => true, out var err);
            // ok == false indicates we did not request the swap because fallback still required
            Assert.False(ok);
            Assert.Equal(0, fake.SwapRequests);
        }
    }
}
