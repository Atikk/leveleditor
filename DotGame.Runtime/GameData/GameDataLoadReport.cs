using System;
using System.Collections.Generic;

namespace DotGame.Runtime.GameData;

public sealed class GameDataLoadReport
{
    internal GameDataLoadReport(DateTime loadedAtUtc)
    {
        LoadedAtUtc = loadedAtUtc;
    }

    public DateTime LoadedAtUtc { get; }

    public int DialogueCount { get; internal set; }

    public int QuestCount { get; internal set; }

    public int CutsceneCount { get; internal set; }

    public List<GameDataLoadError> Errors { get; } = new();

    public bool HasErrors => Errors.Count > 0;
}

public sealed record GameDataLoadError(string FilePath, string Message);
