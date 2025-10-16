using DotGame.Core.States;
using DotGame.Runtime.Input;

namespace DotGame.Runtime.Rendering;

public readonly record struct RuntimeUpdateContext(RuntimeContext Runtime, GameClock Clock, InputSnapshot Input);
