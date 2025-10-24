using DotGame.Core.Platform;
using Dotgame.Avalonia.Services;
using Dotgame.Avalonia.Services.Adapters;
using Xunit;

namespace DotGame.Tests
{
    public class DIWiringTests
    {
        [Fact]
        public void ServiceContainer_RegistersAndResolves_CoreInterface()
        {
            // Register the UI tile service first (mimic Program startup); use FakeBitmapFactory to avoid platform deps in tests.
            var ui = new TileService(new FakeBitmapFactory());
            // register UI service and core adapter in the canonical ServiceContainer
            DotGame.Core.Platform.ServiceContainer.RegisterSingleton<Dotgame.Avalonia.Services.ITileService>(ui);

            var adapter = new TileServiceAdapter(ui);
            DotGame.Core.Platform.ServiceContainer.RegisterSingleton<DotGame.Core.Services.ITileService>(adapter);

            var resolved = DotGame.Core.Platform.ServiceContainer.Resolve<DotGame.Core.Services.ITileService>();
            Assert.NotNull(resolved);

            // Basic call to ensure adapter works
            var buf = resolved.CreateTileBuffer(2,2);
            Assert.NotNull(buf);
            Assert.Equal(2, buf.GetLength(0));
            Assert.Equal(2, buf.GetLength(1));
        }
    }
}
