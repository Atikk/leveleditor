using DotGame.Core.Platform;
using DotGame.Core.Services;
using Xunit;

namespace DotGame.Tests
{
    public class PreviewDIWiringTests
    {
        [Fact]
        public void ServiceContainer_Resolves_IPreviewService_When_Registered()
        {
            // Arrange: register the UI adapter as Program would
            var adapter = new Dotgame.Avalonia.Services.Adapters.MonoGamePreviewAdapter();
            ServiceContainer.RegisterSingleton<IPreviewService>(adapter);

            // Act
            var resolved = ServiceContainer.Resolve<IPreviewService>();

            // Assert
            Assert.NotNull(resolved);
            Assert.IsType<Dotgame.Avalonia.Services.Adapters.MonoGamePreviewAdapter>(resolved);
        }
    }
}
