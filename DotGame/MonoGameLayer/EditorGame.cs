using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using DotGameAvalonia.Models;

namespace DotGameAvalonia.MonoGameLayer
{
    public sealed class EditorGame : Game
    {
        private readonly GraphicsDeviceManager _graphics;
        private readonly Map _map;
        private readonly ITextureResolver? _resolverOverride;
        private AssetManager? _assets;
        private MapRenderer? _renderer;
        private ITextureResolver? _resolver;

        public EditorGame(Map map, ITextureResolver? resolverOverride = null)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _resolverOverride = resolverOverride;

            int width = Math.Max(640, Math.Max(1, map.Cols) * Math.Max(1, map.TileW));
            int height = Math.Max(480, Math.Max(1, map.Rows) * Math.Max(1, map.TileH));

            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = width,
                PreferredBackBufferHeight = height,
                SynchronizeWithVerticalRetrace = true
            };

            IsFixedTimeStep = false;
            IsMouseVisible = true;
        }

        protected override void LoadContent()
        {
            _assets = new AssetManager(GraphicsDevice);
            _resolver = _resolverOverride ?? new FileTextureResolver(_assets);
            _renderer = new MapRenderer(GraphicsDevice, _resolver);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(30, 30, 35));
            if (_renderer != null)
                _renderer.Draw(_map, Vector2.Zero);
            base.Draw(gameTime);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _assets?.Clear();
            }

            base.Dispose(disposing);
        }
    }
}
