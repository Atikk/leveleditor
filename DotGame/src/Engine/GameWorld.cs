using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Dotgame.Avalonia.Engine.Systems;

namespace Dotgame.Avalonia.Engine
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
            entity.ClearRemovalFlag();
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

            for (var i = _entities.Count - 1; i >= 0; i--)
            {
                if (_entities[i].IsMarkedForRemoval)
                {
                    _entities.RemoveAt(i);
                }
            }
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

