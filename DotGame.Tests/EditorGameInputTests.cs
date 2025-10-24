using System;
using Dotgame.Avalonia.MonoGameLayer;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace DotGame.Tests
{
    class FakeInputProvider : EditorGame.IEditorInputProvider
    {
        public bool WasCalled { get; private set; }

        public KeyboardState GetKeyboardState()
        {
            WasCalled = true;
            return new KeyboardState();
        }
        public MouseState GetMouseState()
        {
            WasCalled = true;
            return new MouseState();
        }
    }

    public class EditorGameInputTests
    {
        [Fact]
        public void Update_UsesInputProvider_WhenProvided()
        {
            var map = new Dotgame.Avalonia.Models.Map();
            var fake = new FakeInputProvider();
            var game = new EditorGame(map, schedulerOverride: null, resourceManagerOverride: null, jobSystemOverride: null);
            game.InputProvider = fake;

            // Call Update with a minimal GameTime via reflection because Update is protected.
            var gt = new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16));
            var updateMethod = typeof(EditorGame).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            updateMethod?.Invoke(game, new object[] { gt });

            Assert.True(fake.WasCalled, "EditorGame did not call InputProvider.GetKeyboardState in Update().");
        }
    }
}
