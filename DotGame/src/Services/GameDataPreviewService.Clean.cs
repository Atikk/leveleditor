using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DotGame.Core.Resources;
using DotGame.Runtime.GameData;

namespace DotGameAvalonia.Services;

public sealed class GameDataPreviewService
{
    private readonly object syncRoot = new();
    private readonly GameDataRepository repository = new();
    private readonly ResourceManager resourceManager;
    private CancellationTokenSource? reloadCancellation;
    private Task? reloadTask;
    private bool isLoading;
    private string? contentRootOverride;

    public GameDataPreviewService(ResourceManager resourceManager)
    {
        this.resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
    }

    public bool IsLoading
    {
        get
        {
            lock (syncRoot)
            {
                return isLoading;
            }
        }
    }

    public string? ContentRoot
    {
        get
        {
            lock (syncRoot)
            {
                return contentRootOverride;
            }
        }
        set
        {
            lock (syncRoot)
            {
                contentRootOverride = value;
            }
        }
    }

    public void ReloadAsync(Action<GameDataLoadReport>? onCompleted, Action<Exception>? onError, string? contentRoot = null)
    {
        CancellationTokenSource cancellation;
        lock (syncRoot)
        {
            reloadCancellation?.Cancel();
            cancellation = new CancellationTokenSource();
            reloadCancellation = cancellation;
            isLoading = true;
        }

        var root = contentRoot ?? ContentRoot;
        reloadTask = Task.Run(() => ExecuteReload(cancellation, onCompleted, onError, root), cancellation.Token);
    }

    public bool TryDescribeTrigger(string triggerName, out string description)
    {
        description = string.Empty;
        if (string.IsNullOrWhiteSpace(triggerName))
            return false;

        var parts = triggerName.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return false;

        var type = parts[0].ToLowerInvariant();
        var arguments = parts.Skip(1).ToArray();

        lock (syncRoot)
        {
            return type switch
            {
                "dialogue" when arguments.Length >= 1 && TryDescribeDialogue(arguments[0], arguments.Skip(1).ToArray(), out description) => true,
                "quest" when arguments.Length >= 1 && TryDescribeQuest(arguments[0], out description) => true,
                "cutscene" when arguments.Length >= 1 && TryDescribeCutscene(arguments[0], out description) => true,
                _ => false
            };
        }
    }

    public IReadOnlyList<GameDataEntrySummary> GetDialogueSummaries()
    {
        lock (syncRoot)
        {
            return repository.Dialogues.Values
                .OrderBy(g => g.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => new GameDataEntrySummary(g.Id, $"{g.Id} - {g.Nodes.Count} node{(g.Nodes.Count == 1 ? string.Empty : "s")}"))
                .ToList();
        }
    }

    public IReadOnlyList<GameDataEntrySummary> GetQuestSummaries()
    {
        lock (syncRoot)
        {
            return repository.Quests.Values
                .OrderBy(q => q.Id, StringComparer.OrdinalIgnoreCase)
                .Select(q => new GameDataEntrySummary(q.Id, $"{q.Name} ({q.Id}) - {q.Stages.Count} stage{(q.Stages.Count == 1 ? string.Empty : "s")}"))
                .ToList();
        }
    }

    public IReadOnlyList<GameDataEntrySummary> GetCutsceneSummaries()
    {
        lock (syncRoot)
        {
            return repository.Cutscenes.Values
                .OrderBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
                .Select(c => new GameDataEntrySummary(c.Id, $"{c.Id} - {c.Steps.Count} step{(c.Steps.Count == 1 ? string.Empty : "s")}"))
                .ToList();
        }
    }

    private void ExecuteReload(CancellationTokenSource cancellation, Action<GameDataLoadReport>? onCompleted, Action<Exception>? onError, string? contentRoot)
    {
        GameDataLoadReport? report = null;
        try
        {
            cancellation.Token.ThrowIfCancellationRequested();
            lock (syncRoot)
            {
                report = repository.LoadAllFromContent(contentRoot);
            }
            cancellation.Token.ThrowIfCancellationRequested();

            bool shouldNotify;
            lock (syncRoot)
            {
                shouldNotify = ReferenceEquals(reloadCancellation, cancellation);
            }

            if (shouldNotify && report != null)
            {
                onCompleted?.Invoke(report);
            }
        }
        catch (OperationCanceledException)
        {
            // Ignored
        }
        catch (Exception ex)
        {
            bool shouldNotify;
            lock (syncRoot)
            {
                shouldNotify = ReferenceEquals(reloadCancellation, cancellation);
            }

            if (shouldNotify)
            {
                onError?.Invoke(ex);
            }
        }
        finally
        {
            lock (syncRoot)
            {
                if (ReferenceEquals(reloadCancellation, cancellation))
                {
                    reloadCancellation = null;
                    reloadTask = null;
                    isLoading = false;
                }
            }

            cancellation.Dispose();
        }
    }

    private bool TryDescribeDialogue(string graphId, string[] nodeSegments, out string description)
    {
        var graph = repository.TryGetDialogue(graphId);
        if (graph == null)
        {
            description = string.Empty;
            return false;
        }

        var sb = new StringBuilder();
        sb.Append($"Dialogue '{graph.Id}'");
        if (graph.Nodes.Count == 0)
        {
            sb.Append(" has no nodes.");
            description = sb.ToString();
            return true;
        }

        var node = graph.Nodes[0];
        if (nodeSegments.Length > 0)
        {
            var requestedNodeId = nodeSegments[0];
            if (graph.TryGetNode(requestedNodeId, out var resolved))
            {
                node = resolved;
                sb.Append($" node '{requestedNodeId}'");
            }
            else
            {
                sb.Append($" node '{requestedNodeId}' not found; showing first node");
            }
        }

        sb.Append($" - {FormatSpeakerLine(node)}");
        if (node.Choices.Count > 0)
        {
            sb.Append($" ({node.Choices.Count} choice{(node.Choices.Count == 1 ? string.Empty : "s")})");
        }

        description = sb.ToString();
        return true;
    }

    private bool TryDescribeQuest(string questId, out string description)
    {
        var quest = repository.TryGetQuest(questId);
        if (quest == null)
        {
            description = string.Empty;
            return false;
        }

        var sb = new StringBuilder();
        sb.Append($"Quest '{quest.Name}' ({quest.Id}) - {quest.Stages.Count} stage");
        sb.Append(quest.Stages.Count == 1 ? string.Empty : "s");

        if (quest.Stages.Count > 0)
        {
            var firstStage = quest.Stages[0];
            sb.Append($". Stage '{firstStage.Id}' has {firstStage.Objectives.Count} objective");
            sb.Append(firstStage.Objectives.Count == 1 ? string.Empty : "s");

            if (!string.IsNullOrWhiteSpace(firstStage.Narrative))
            {
                sb.Append($" - {Truncate(firstStage.Narrative, 80)}");
            }
        }

        description = sb.ToString();
        return true;
    }

    private bool TryDescribeCutscene(string cutsceneId, out string description)
    {
        var cutscene = repository.TryGetCutscene(cutsceneId);
        if (cutscene == null)
        {
            description = string.Empty;
            return false;
        }

        var sb = new StringBuilder();
        sb.Append($"Cutscene '{cutscene.Id}' - {cutscene.Steps.Count} step");
        sb.Append(cutscene.Steps.Count == 1 ? string.Empty : "s");

        if (cutscene.Steps.Count > 0)
        {
            var firstStep = cutscene.Steps[0];
            sb.Append($". First step: {firstStep.Type} ({FormatDuration(firstStep.Duration)})");
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

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        return text[..Math.Max(0, maxLength - 3)] + "...";
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

    public sealed record GameDataEntrySummary(string Id, string Summary);
}
