using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using DotGameAvalonia.Engine.Systems;

namespace DotGameAvalonia.Engine
{
    /// <summary>
    /// Maintains a collection of entities and coordinates global update and draw calls.
    /// </summary>
    public sealed class GameWorld
    {
    private readonly List<Entity> _entities = new();
    private readonly CollisionSystem _collisionSystem = new();

        public IReadOnlyList<Entity> Entities => _entities;

        public T AddEntity<T>(T entity) where T : Entity
        {
            _entities.Add(entity);
            return entity;
        }

        public bool RemoveEntity(Entity entity)
        {
            return _entities.Remove(entity);
        }

        public void Clear()
        {
            _entities.Clear();
        }

        public void Update(GameTime gameTime)
        {
            for (var i = 0; i < _entities.Count; i++)
            {
                _entities[i].Update(gameTime);
            }

            _collisionSystem.Process(gameTime, _entities);
        }

        public void Draw(GameTime gameTime, SpriteBatch spriteBatch, Matrix viewMatrix)
        {
            spriteBatch.Begin(transformMatrix: viewMatrix, samplerState: SamplerState.PointClamp);
            for (var i = 0; i < _entities.Count; i++)
            {
                _entities[i].Draw(gameTime, spriteBatch);
            }
            spriteBatch.End();
        }
    }
}
