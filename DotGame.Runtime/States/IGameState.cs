using System;
using DotGame.Core.States;
using DotGame.Runtime.Rendering;

namespace DotGame.Runtime.States;

public interface IGameState : IDisposable
{
    bool AllowUpdateBelow { get; }

    bool AllowDrawBelow { get; }

    void OnEnter();

    void OnExit();

    void Update(in RuntimeUpdateContext context);

    void Draw(in RuntimeDrawContext context);
}
