using System;
using System.Collections.Generic;
using System.Linq;
using Dotgame.Avalonia.Engine.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Dotgame.Avalonia.Engine
{
    /// <summary>
    /// Lightweight entity composed of modular components.
    /// </summary>
    public class Entity
    {
    private readonly List<IGameEntityComponent> _components = new();

        public Entity(string? name = null)
        {
            Name = string.IsNullOrWhiteSpace(name) ? $"Entity_{Id:N}" : name;
            Transform = AddComponent(new TransformComponent());
        }

        public Guid Id { get; } = Guid.NewGuid();

        public string Name { get; set; }

        public TransformComponent Transform { get; }

        public IReadOnlyList<IGameEntityComponent> Components => _components;

        public bool IsMarkedForRemoval { get; private set; }

        public T AddComponent<T>(T component) where T : IGameEntityComponent
        {
            if (_components.Contains(component))
                return component;

            component.OnAttached(this);
            component.Initialize();
            _components.Add(component);
            return component;
        }

        public void MarkForRemoval()
        {
            IsMarkedForRemoval = true;
        }

        internal void ClearRemovalFlag()
        {
            IsMarkedForRemoval = false;
        }

        public bool RemoveComponent<T>(T component) where T : IGameEntityComponent
        {
            return _components.Remove(component);
        }

        public T? GetComponent<T>() where T : class, IGameEntityComponent
        {
            return _components.OfType<T>().FirstOrDefault();
        }

        internal void Update(GameTime gameTime)
        {
            foreach (var component in _components)
            {
                if (component.Enabled)
                {
                    component.Update(gameTime);
                }
            }
        }

        internal void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            foreach (var component in _components)
            {
                if (component.Enabled)
                {
                    component.Draw(gameTime, spriteBatch);
                }
            }
        }
    }
}

