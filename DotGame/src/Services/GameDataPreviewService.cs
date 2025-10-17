using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DotGame.Core.Resources;
using DotGame.Runtime.GameData;

namespace DotGameAvalonia.Services;

public sealed class GameDataPreviewService
{
    private readonly GameDataRepository _repository = new();
    private readonly object _syncRoot = new();
    private readonly ResourceManager? _resourceManager;
    private ResourceHandle<GameDataLoadReport>? _activeHandle;

    public GameDataPreviewService(ResourceManager? resourceManager = null)
    {
        _resourceManager = resourceManager;
    }

    public bool IsLoading
    {
        get
        {
            lock (_syncRoot)
            {
                return _activeHandle != null;
            }
        }
    }

    public void ReloadAsync(Action<GameDataLoadReport>? onCompleted, Action<Exception>? onError, string? contentRoot = null)
    {
        lock (_syncRoot)
        {
            if (_resourceManager == null)
            {
                try
                {
                    var report = _repository.LoadAllFromContent(contentRoot);
                    onCompleted?.Invoke(report);
                }
                catch (Exception ex)
                {
                    onError?.Invoke(ex);
                }

                return;
            }

            if (_activeHandle != null)
            {
                _resourceManager.Release(_activeHandle);
                _activeHandle = null;
            }

            var cacheKey = BuildCacheKey(contentRoot);
            _activeHandle = _resourceManager.LoadAsync(
                cacheKey,
                _ => _repository.LoadAllFromContent(contentRoot),
                onCompleted: handle => HandleCompleted(handle, onCompleted),
                onFailed: handle => HandleFailed(handle, onError));
        }
    }

    public GameDataLoadReport Reload(string? contentRoot = null)
    {
        lock (_syncRoot)
        {
            return _repository.LoadAllFromContent(contentRoot);
        }
    }

    public bool TryDescribeTrigger(string triggerName, out string description)
    {
        description = string.Empty;
        if (string.IsNullOrWhiteSpace(triggerName))
            return false;

        var parts = triggerName
            .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return false;

        var triggerType = parts[0].ToLowerInvariant();

        lock (_syncRoot)
        {
            return triggerType switch
            {
                "dialogue" when parts.Length >= 2 && TryDescribeDialogue(parts[1], parts.Skip(2).ToArray(), out description) => true,
                "quest" when parts.Length >= 2 && TryDescribeQuest(parts[1], out description) => true,
                "cutscene" when parts.Length >= 2 && TryDescribeCutscene(parts[1], out description) => true,
                _ => false
            };
        }
    }

    private bool TryDescribeDialogue(string graphId, string[] nodeSegments, out string description)
    {
        description = string.Empty;
        var graph = _repository.TryGetDialogue(graphId);
        if (graph == null)
            return false;

        var sb = new StringBuilder();
        sb.Append($"Dialogue '{graph.Id}'");

        if (graph.Nodes.Count == 0)
        {
            sb.Append(" (no nodes)");
            description = sb.ToString();
            return true;
        }

        DialogueNode? node = null;
        if (nodeSegments.Length > 0)
        {
            var requestedNodeId = nodeSegments[0];
            if (graph.TryGetNode(requestedNodeId, out var resolved))
            {
                node = resolved;
                sb.Append($" node '{requestedNodeId}'");
            }
        }

        node ??= graph.Nodes[0];
        sb.Append($" – {FormatSpeakerLine(node)}");

        if (node.Choices.Count > 0)
        {
            sb.Append($" ({node.Choices.Count} choice{(node.Choices.Count > 1 ? "s" : string.Empty)})");
        }

        description = sb.ToString();
        return true;
    }

    private static string FormatSpeakerLine(DialogueNode node)
    {
        var speaker = string.IsNullOrWhiteSpace(node.Speaker) ? "Narrator" : node.Speaker;
        var text = string.IsNullOrWhiteSpace(node.Text) ? "(empty line)" : Truncate(node.Text, 90);
        return $"{speaker}: \"{text}\"";
    }

    private bool TryDescribeQuest(string questId, out string description)
    {
        description = string.Empty;
        var quest = _repository.TryGetQuest(questId);
        if (quest == null)
            return false;

        var sb = new StringBuilder();
        sb.Append($"Quest '{quest.Name}' ({quest.Id}) – {quest.Stages.Count} stage");
        sb.Append(quest.Stages.Count == 1 ? string.Empty : "s");

        if (quest.Stages.Count > 0)
        {
            var firstStage = quest.Stages[0];
            sb.Append($". Stage '{firstStage.Id}' has {firstStage.Objectives.Count} objective");
            sb.Append(firstStage.Objectives.Count == 1 ? string.Empty : "s");

            if (!string.IsNullOrWhiteSpace(firstStage.Narrative))
            {
                sb.Append($" – {Truncate(firstStage.Narrative, 80)}");
            }
        }

        description = sb.ToString();
        return true;
    }

    private bool TryDescribeCutscene(string cutsceneId, out string description)
    {
        description = string.Empty;
        var cutscene = _repository.TryGetCutscene(cutsceneId);
        if (cutscene == null)
            return false;

        var sb = new StringBuilder();
        sb.Append($"Cutscene '{cutscene.Id}' – {cutscene.Steps.Count} step");
        sb.Append(cutscene.Steps.Count == 1 ? string.Empty : "s");

        if (cutscene.Steps.Count > 0)
        {
            var firstStep = cutscene.Steps[0];
            sb.Append($". First step: {firstStep.Type} ({FormatDuration(firstStep.Duration)})");
        }

        description = sb.ToString();
        return true;
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        return text[..Math.Max(0, maxLength - 1)] + "…";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration == TimeSpan.Zero)
            return "instant";

        if (duration.TotalSeconds < 1)
            return $"{duration.TotalMilliseconds:0} ms";

        if (duration.TotalMinutes < 1)
            return $"{duration.TotalSeconds:0.##} s";

        return duration.ToString();
    }

    public IReadOnlyList<GameDataEntrySummary> GetDialogueSummaries()
    {
        lock (_syncRoot)
        {
            return _repository.Dialogues.Values
                .OrderBy(g => g.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => new GameDataEntrySummary(g.Id, $"{g.Id} – {g.Nodes.Count} node{(g.Nodes.Count == 1 ? string.Empty : "s")}"))
                .ToList();
        }
    }

    public IReadOnlyList<GameDataEntrySummary> GetQuestSummaries()
    {
        lock (_syncRoot)
        {
            return _repository.Quests.Values
                .OrderBy(q => q.Id, StringComparer.OrdinalIgnoreCase)
                .Select(q => new GameDataEntrySummary(q.Id, $"{q.Name} ({q.Id}) – {q.Stages.Count} stage{(q.Stages.Count == 1 ? string.Empty : "s")}"))
                .ToList();
        }
    }

    public IReadOnlyList<GameDataEntrySummary> GetCutsceneSummaries()
    {
        lock (_syncRoot)
        {
            return _repository.Cutscenes.Values
                .OrderBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
                .Select(c => new GameDataEntrySummary(c.Id, $"{c.Id} – {c.Steps.Count} step{(c.Steps.Count == 1 ? string.Empty : "s")}"))
                .ToList();
        }
    }

    public sealed record GameDataEntrySummary(string Id, string Summary);

    private void HandleCompleted(ResourceHandle<GameDataLoadReport> handle, Action<GameDataLoadReport>? callback)
    {
        try
        {
            callback?.Invoke(handle.Value);
        }
        finally
        {
            ReleaseHandle(handle);
        }
    }

    private void HandleFailed(ResourceHandle<GameDataLoadReport> handle, Action<Exception>? callback)
    {
        try
        {
            var exception = handle.Exception ?? new InvalidOperationException("Unknown error while loading game data.");
            callback?.Invoke(exception);
        }
        finally
        {
            ReleaseHandle(handle);
        }
    }

    private void ReleaseHandle(ResourceHandle<GameDataLoadReport> handle)
    {
        lock (_syncRoot)
        {
            if (_resourceManager != null)
            {
                _resourceManager.Release(handle);
            }

            if (ReferenceEquals(_activeHandle, handle))
            {
                _activeHandle = null;
            }
        }
    }

    private static string BuildCacheKey(string? contentRoot)
    {
        return string.IsNullOrWhiteSpace(contentRoot)
            ? "gamedata:preview:default"
            : $"gamedata:preview:{contentRoot}";
    }
}
