using System;
using Dotgame.Avalonia.Models;

namespace Dotgame.Avalonia.Engine.Components
{
    /// <summary>
    /// Associates an entity with a behavior trigger definition from the map.
    /// </summary>
    public sealed class TriggerComponent : ComponentBase
    {
        public TriggerComponent(BehaviorTrigger trigger)
        {
            Trigger = trigger ?? throw new ArgumentNullException(nameof(trigger));
        }

        public BehaviorTrigger Trigger { get; }
    }
}

