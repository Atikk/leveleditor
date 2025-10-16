using System;
using System.Collections.Concurrent;
using System.Linq;
using DotGame.Runtime.Rendering;

namespace DotGame.Runtime.States;

public sealed class GameStateStack : IDisposable
{
    private readonly ConcurrentStack<IGameState> _states = new();

    public void Push(IGameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _states.Push(state);
        state.OnEnter();
    }

    public void Pop()
    {
        if (_states.TryPop(out var state))
        {
            state.OnExit();
            state.Dispose();
        }
    }

    public void Clear()
    {
        while (_states.TryPop(out var state))
        {
            state.OnExit();
            state.Dispose();
        }
    }

    public void Update(in RuntimeUpdateContext context)
    {
        foreach (var state in _states)
        {
            state.Update(context);
            if (!state.AllowUpdateBelow)
            {
                break;
            }
        }
    }

    public void Draw(in RuntimeDrawContext context)
    {
        foreach (var state in _states.Reverse())
        {
            state.Draw(context);
            if (!state.AllowDrawBelow)
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        Clear();
    }
}
