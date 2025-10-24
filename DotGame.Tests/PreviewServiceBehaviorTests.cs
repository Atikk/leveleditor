using DotGame.Core.Platform;
using DotGame.Core.Services;
using Xunit;

namespace DotGame.Tests
{
    public class PreviewServiceBehaviorTests
    {
        [Fact]
        public void PreviewService_StartsAndStops_TogglesRunningFlag()
        {
            // Arrange
            var adapter = new Dotgame.Avalonia.Services.Adapters.MonoGamePreviewAdapter();
            ServiceContainer.RegisterSingleton<IPreviewService>(adapter);

            // Act
            var svc = ServiceContainer.Resolve<IPreviewService>();

            // initial state should be false
            Assert.False(svc.IsRunning);

            svc.StartPreview(null);
            Assert.True(svc.IsRunning);

            svc.StopPreview();
            Assert.False(svc.IsRunning);
        }
    }
}
