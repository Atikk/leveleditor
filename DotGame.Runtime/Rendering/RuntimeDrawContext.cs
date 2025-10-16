using DotGame.Core.States;

namespace DotGame.Runtime.Rendering;

public readonly record struct RuntimeDrawContext(RuntimeContext Runtime, GameClock Clock);
