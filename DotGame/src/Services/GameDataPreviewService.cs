using System;using System;

using System.Collections.Generic;using System.Collections.Generic;

using System.Linq;using System.Linq;

using System.Text;    }

using System.Threading;}

using System.Threading.Tasks;        var sb = new StringBuilder();

using DotGame.Core.Resources;

using DotGame.Runtime.GameData;        sb.Append($"Quest '{quest.Name}' ({quest.Id}) - {quest.Stages.Count} stage");            .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);        {



namespace DotGameAvalonia.Services;        sb.Append(quest.Stages.Count == 1 ? string.Empty : "s");



public sealed class GameDataPreviewService        if (parts.Length == 0)            cancellation.Token.ThrowIfCancellationRequested();

{

    private readonly object syncRoot = new();        if (quest.Stages.Count > 0)

    private readonly GameDataRepository repository = new();

    private readonly ResourceManager resourceManager;        {            return false;

    private CancellationTokenSource? reloadCancellation;

    private Task? reloadTask;            var firstStage = quest.Stages[0];

    private bool isLoading;

    private string? contentRootOverride;            sb.Append($". Stage '{firstStage.Id}' has {firstStage.Objectives.Count} objective");            GameDataLoadReport report;



    public GameDataPreviewService(ResourceManager resourceManager)            sb.Append(firstStage.Objectives.Count == 1 ? string.Empty : "s");

    {

        this.resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));        var triggerType = parts[0].ToLowerInvariant();            lock (_syncRoot)

    }

            if (!string.IsNullOrWhiteSpace(firstStage.Narrative))

    public bool IsLoading

    {            {            {

        get

        {                sb.Append($" - {Truncate(firstStage.Narrative, 80)}");

            lock (syncRoot)

            {            }        lock (_syncRoot)                report = _repository.LoadAllFromContent(contentRoot);

                return isLoading;

            }        }

        }

    }        {            }



    public string? ContentRoot        description = sb.ToString();

    {

        get        return true;            return triggerType switch

        {

            lock (syncRoot)    }

            {

                return contentRootOverride;            {            if (cancellation.IsCancellationRequested)

            }

        }    private bool TryDescribeCutscene(string cutsceneId, out string description)

        set

        {    {                "dialogue" when parts.Length >= 2 && TryDescribeDialogue(parts[1], parts.Skip(2).ToArray(), out description) => true,                return;

            lock (syncRoot)

            {        description = string.Empty;

                contentRootOverride = value;

            }        var cutscene = _repository.TryGetCutscene(cutsceneId);                "quest" when parts.Length >= 2 && TryDescribeQuest(parts[1], out description) => true,

        }

    }        if (cutscene == null)



    public void ReloadAsync(Action<GameDataLoadReport>? onCompleted, Action<Exception>? onError, string? contentRoot = null)            return false;                "cutscene" when parts.Length >= 2 && TryDescribeCutscene(parts[1], out description) => true,            onCompleted?.Invoke(report);

    {

        CancellationTokenSource cancellation;

        lock (syncRoot)

        {        var sb = new StringBuilder();                _ => false        }

            reloadCancellation?.Cancel();

            cancellation = new CancellationTokenSource();        sb.Append($"Cutscene '{cutscene.Id}' - {cutscene.Steps.Count} step");

            reloadCancellation = cancellation;

            isLoading = true;        sb.Append(cutscene.Steps.Count == 1 ? string.Empty : "s");            };        catch (OperationCanceledException)

        }



        var root = contentRoot ?? ContentRoot;

        reloadTask = Task.Run(() => ExecuteReload(cancellation, onCompleted, onError, root), cancellation.Token);        if (cutscene.Steps.Count > 0)        }        {

    }

        {

    public bool TryDescribeTrigger(string triggerName, out string description)

    {            var firstStep = cutscene.Steps[0];    }            // Cancellation requested; ignore result.

        description = string.Empty;

        if (string.IsNullOrWhiteSpace(triggerName))            sb.Append($". First step: {firstStep.Type} ({FormatDuration(firstStep.Duration)})");

            return false;

        }        }

        var parts = triggerName.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)

            return false;

        description = sb.ToString();    private bool TryDescribeDialogue(string graphId, string[] nodeSegments, out string description)        catch (Exception ex)

        var type = parts[0].ToLowerInvariant();

        var arguments = parts.Skip(1).ToArray();        return true;



        lock (syncRoot)    }    {        {

        {

            return type switch

            {

                "dialogue" when arguments.Length >= 1 && TryDescribeDialogue(arguments[0], arguments.Skip(1).ToArray(), out description) => true,    private static string Truncate(string text, int maxLength)        description = string.Empty;            if (!cancellation.IsCancellationRequested)

                "quest" when arguments.Length >= 1 && TryDescribeQuest(arguments[0], out description) => true,

                "cutscene" when arguments.Length >= 1 && TryDescribeCutscene(arguments[0], out description) => true,    {

                _ => false

            };        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)        var graph = _repository.TryGetDialogue(graphId);                onError?.Invoke(ex);

        }

    }            return text;



    public IReadOnlyList<GameDataEntrySummary> GetDialogueSummaries()        if (graph == null)        }

    {

        lock (syncRoot)        return text[..Math.Max(0, maxLength - 3)] + "...";

        {

            return repository.Dialogues.Values    }            return false;        finally

                .OrderBy(g => g.Id, StringComparer.OrdinalIgnoreCase)

                .Select(g => new GameDataEntrySummary(g.Id, $"{g.Id} - {g.Nodes.Count} node{(g.Nodes.Count == 1 ? string.Empty : "s")}"))

                .ToList();

        }    private static string FormatDuration(TimeSpan duration)        {

    }

    {

    public IReadOnlyList<GameDataEntrySummary> GetQuestSummaries()

    {        if (duration == TimeSpan.Zero)        var sb = new StringBuilder();            lock (_syncRoot)

        lock (syncRoot)

        {            return "instant";

            return repository.Quests.Values

                .OrderBy(q => q.Id, StringComparer.OrdinalIgnoreCase)        sb.Append($"Dialogue '{graph.Id}'");            {

                .Select(q => new GameDataEntrySummary(q.Id, $"{q.Name} ({q.Id}) - {q.Stages.Count} stage{(q.Stages.Count == 1 ? string.Empty : "s")}"))

                .ToList();        if (duration.TotalSeconds < 1)

        }

    }            return $"{duration.TotalMilliseconds:0} ms";                if (ReferenceEquals(_reloadCancellation, cancellation))



    public IReadOnlyList<GameDataEntrySummary> GetCutsceneSummaries()

    {

        lock (syncRoot)        if (duration.TotalMinutes < 1)        if (graph.Nodes.Count == 0)                {

        {

            return repository.Cutscenes.Values            return $"{duration.TotalSeconds:0.##} s";

                .OrderBy(c => c.Id, StringComparer.OrdinalIgnoreCase)

                .Select(c => new GameDataEntrySummary(c.Id, $"{c.Id} - {c.Steps.Count} step{(c.Steps.Count == 1 ? string.Empty : "s")}"))        {                    _reloadCancellation = null;

                .ToList();

        }        return duration.ToString();

    }

    }            sb.Append(" (no nodes)");                    _reloadTask = null;

    private void ExecuteReload(CancellationTokenSource cancellation, Action<GameDataLoadReport>? onCompleted, Action<Exception>? onError, string? contentRoot)

    {

        GameDataLoadReport? report = null;

        try    public IReadOnlyList<GameDataEntrySummary> GetDialogueSummaries()            description = sb.ToString();                    _isLoading = false;

        {

            cancellation.Token.ThrowIfCancellationRequested();    {

            lock (syncRoot)

            {        lock (_syncRoot)            return true;                }

                report = repository.LoadAllFromContent(contentRoot);

            }        {

            cancellation.Token.ThrowIfCancellationRequested();

            return _repository.Dialogues.Values        }            }

            bool shouldNotify;

            lock (syncRoot)                .OrderBy(g => g.Id, StringComparer.OrdinalIgnoreCase)

            {

                shouldNotify = ReferenceEquals(reloadCancellation, cancellation);                .Select(g => new GameDataEntrySummary(g.Id, $"{g.Id} - {g.Nodes.Count} node{(g.Nodes.Count == 1 ? string.Empty : "s")}"))        }

            }

                .ToList();

            if (shouldNotify && report != null)

            {        }        DialogueNode? node = null;    }

                onCompleted?.Invoke(report);

            }    }

        }

        catch (OperationCanceledException)        if (nodeSegments.Length > 0)        description = sb.ToString();

        {

            // Ignored    public IReadOnlyList<GameDataEntrySummary> GetQuestSummaries()

        }

        catch (Exception ex)    {        {        return true;

        {

            bool shouldNotify;        lock (_syncRoot)

            lock (syncRoot)

            {        {            var requestedNodeId = nodeSegments[0];    }

                shouldNotify = ReferenceEquals(reloadCancellation, cancellation);

            }            return _repository.Quests.Values



            if (shouldNotify)                .OrderBy(q => q.Id, StringComparer.OrdinalIgnoreCase)            if (graph.TryGetNode(requestedNodeId, out var resolved))

            {

                onError?.Invoke(ex);                .Select(q => new GameDataEntrySummary(q.Id, $"{q.Name} ({q.Id}) - {q.Stages.Count} stage{(q.Stages.Count == 1 ? string.Empty : "s")}"))

            }

        }                .ToList();            {    private static string FormatSpeakerLine(DialogueNode node)

        finally

        {        }

            lock (syncRoot)

            {    }                node = resolved;    {

                if (ReferenceEquals(reloadCancellation, cancellation))

                {

                    reloadCancellation = null;

                    reloadTask = null;    public IReadOnlyList<GameDataEntrySummary> GetCutsceneSummaries()                sb.Append($" node '{requestedNodeId}'");        var speaker = string.IsNullOrWhiteSpace(node.Speaker) ? "Narrator" : node.Speaker;

                    isLoading = false;

                }    {

            }

        lock (_syncRoot)            }        var text = string.IsNullOrWhiteSpace(node.Text) ? "(empty line)" : Truncate(node.Text, 90);

            cancellation.Dispose();

        }        {

    }

            return _repository.Cutscenes.Values        }        return $"{speaker}: \"{text}\"";

    private bool TryDescribeDialogue(string graphId, string[] nodeSegments, out string description)

    {                .OrderBy(c => c.Id, StringComparer.OrdinalIgnoreCase)

        var graph = repository.TryGetDialogue(graphId);

        if (graph == null)                .Select(c => new GameDataEntrySummary(c.Id, $"{c.Id} - {c.Steps.Count} step{(c.Steps.Count == 1 ? string.Empty : "s")}"))    }

        {

            description = string.Empty;                .ToList();

            return false;

        }        }        node ??= graph.Nodes[0];



        var sb = new StringBuilder();    }

        sb.Append($"Dialogue '{graph.Id}'");

        if (graph.Nodes.Count == 0)        sb.Append($"  {FormatSpeakerLine(node)}");    private bool TryDescribeQuest(string questId, out string description)

        {

            sb.Append(" has no nodes.");    public sealed record GameDataEntrySummary(string Id, string Summary);

            description = sb.ToString();

            return true;    {

        }

    private void ExecuteReload(CancellationTokenSource cancellation, Action<GameDataLoadReport>? onCompleted, Action<Exception>? onError, string? contentRoot)

        var node = graph.Nodes[0];

        if (nodeSegments.Length > 0)    {        if (node.Choices.Count > 0)        description = string.Empty;

        {

            var requestedNodeId = nodeSegments[0];        try

            if (graph.TryGetNode(requestedNodeId, out var resolved))

            {        {        {        var quest = _repository.TryGetQuest(questId);

                node = resolved;

                sb.Append($" node '{requestedNodeId}'");            GameDataLoadReport report;

            }

            else            lock (_syncRoot)            sb.Append($" ({node.Choices.Count} choice{(node.Choices.Count > 1 ? "s" : string.Empty)})");        if (quest == null)

            {

                sb.Append($" node '{requestedNodeId}' not found; showing first node");            {

            }

        }                report = _repository.LoadAllFromContent(contentRoot);        }            return false;



        sb.Append($" - {FormatSpeakerLine(node)}");            }

        if (node.Choices.Count > 0)

        {

            sb.Append($" ({node.Choices.Count} choice{(node.Choices.Count == 1 ? string.Empty : "s")})");

        }            if (cancellation.IsCancellationRequested)



        description = sb.ToString();                return;        description = sb.ToString();        var sb = new StringBuilder();

        return true;

    }



    private bool TryDescribeQuest(string questId, out string description)            onCompleted?.Invoke(report);        return true;        sb.Append($"Quest '{quest.Name}' ({quest.Id}) – {quest.Stages.Count} stage");

    {

        var quest = repository.TryGetQuest(questId);        }

        if (quest == null)

        {        catch (OperationCanceledException)    }        sb.Append(quest.Stages.Count == 1 ? string.Empty : "s");

            description = string.Empty;

            return false;        {

        }

            // Load was cancelled; ignore.

        var sb = new StringBuilder();

        sb.Append($"Quest '{quest.Name}' ({quest.Id}) - {quest.Stages.Count} stage");        }

        sb.Append(quest.Stages.Count == 1 ? string.Empty : "s");

        catch (Exception ex) when (cancellation.IsCancellationRequested)    private static string FormatSpeakerLine(DialogueNode node)        if (quest.Stages.Count > 0)

        if (quest.Stages.Count > 0)

        {        {

            var firstStage = quest.Stages[0];

            sb.Append($". Stage '{firstStage.Id}' has {firstStage.Objectives.Count} objective");            // Ignore errors from cancelled loads.    {        {

            sb.Append(firstStage.Objectives.Count == 1 ? string.Empty : "s");

        }

            if (!string.IsNullOrWhiteSpace(firstStage.Narrative))

            {        catch (Exception ex)        var speaker = string.IsNullOrWhiteSpace(node.Speaker) ? "Narrator" : node.Speaker;            var firstStage = quest.Stages[0];

                sb.Append($" - {Truncate(firstStage.Narrative, 80)}");

            }        {

        }

            if (!cancellation.IsCancellationRequested)        var text = string.IsNullOrWhiteSpace(node.Text) ? "(empty line)" : Truncate(node.Text, 90);            sb.Append($". Stage '{firstStage.Id}' has {firstStage.Objectives.Count} objective");

        description = sb.ToString();

        return true;            {

    }

                onError?.Invoke(ex);        return $"{speaker}: \"{text}\"";            sb.Append(firstStage.Objectives.Count == 1 ? string.Empty : "s");

    private bool TryDescribeCutscene(string cutsceneId, out string description)

    {            }

        var cutscene = repository.TryGetCutscene(cutsceneId);

        if (cutscene == null)        }    }

        {

            description = string.Empty;        finally

            return false;

        }        {            if (!string.IsNullOrWhiteSpace(firstStage.Narrative))



        var sb = new StringBuilder();            lock (_syncRoot)

        sb.Append($"Cutscene '{cutscene.Id}' - {cutscene.Steps.Count} step");

        sb.Append(cutscene.Steps.Count == 1 ? string.Empty : "s");            {    private bool TryDescribeQuest(string questId, out string description)            {



        if (cutscene.Steps.Count > 0)                if (ReferenceEquals(_reloadCancellation, cancellation))

        {

            var firstStep = cutscene.Steps[0];                {    {                sb.Append($" – {Truncate(firstStage.Narrative, 80)}");

            sb.Append($". First step: {firstStep.Type} ({FormatDuration(firstStep.Duration)})");

        }                    _reloadCancellation = null;



        description = sb.ToString();                    _isLoading = false;        description = string.Empty;            }

        return true;

    }                }



    private static string FormatSpeakerLine(DialogueNode node)            }        var quest = _repository.TryGetQuest(questId);        }

    {

        var speaker = string.IsNullOrWhiteSpace(node.Speaker) ? "Narrator" : node.Speaker;

        var text = string.IsNullOrWhiteSpace(node.Text) ? "(empty line)" : Truncate(node.Text, 90);

        return $"{speaker}: \"{text}\"";            cancellation.Dispose();        if (quest == null)

    }

        }

    private static string Truncate(string text, int maxLength)

    {    }            return false;        description = sb.ToString();

        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)

            return text;}



        return text[..Math.Max(0, maxLength - 3)] + "...";        return true;

    }

        var sb = new StringBuilder();    }

    private static string FormatDuration(TimeSpan duration)

    {        sb.Append($"Quest '{quest.Name}' ({quest.Id})  {quest.Stages.Count} stage");

        if (duration == TimeSpan.Zero)

            return "instant";        sb.Append(quest.Stages.Count == 1 ? string.Empty : "s");    private bool TryDescribeCutscene(string cutsceneId, out string description)



        if (duration.TotalSeconds < 1)    {

            return $"{duration.TotalMilliseconds:0} ms";

        if (quest.Stages.Count > 0)        description = string.Empty;

        if (duration.TotalMinutes < 1)

            return $"{duration.TotalSeconds:0.##} s";        {        var cutscene = _repository.TryGetCutscene(cutsceneId);



        return duration.ToString();            var firstStage = quest.Stages[0];        if (cutscene == null)

    }

            sb.Append($". Stage '{firstStage.Id}' has {firstStage.Objectives.Count} objective");            return false;

    public sealed record GameDataEntrySummary(string Id, string Summary);

}            sb.Append(firstStage.Objectives.Count == 1 ? string.Empty : "s");


        var sb = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(firstStage.Narrative))        sb.Append($"Cutscene '{cutscene.Id}' – {cutscene.Steps.Count} step");

            {        sb.Append(cutscene.Steps.Count == 1 ? string.Empty : "s");

                sb.Append($"  {Truncate(firstStage.Narrative, 80)}");

            }        if (cutscene.Steps.Count > 0)

        }        {

            var firstStep = cutscene.Steps[0];

        description = sb.ToString();            sb.Append($". First step: {firstStep.Type} ({FormatDuration(firstStep.Duration)})");

        return true;        }

    }

        description = sb.ToString();

    private bool TryDescribeCutscene(string cutsceneId, out string description)        return true;

    {    }

        description = string.Empty;

        var cutscene = _repository.TryGetCutscene(cutsceneId);    private static string Truncate(string text, int maxLength)

        if (cutscene == null)    {

            return false;        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)

            return text;

        var sb = new StringBuilder();

        sb.Append($"Cutscene '{cutscene.Id}'  {cutscene.Steps.Count} step");        return text[..Math.Max(0, maxLength - 1)] + "…";

        sb.Append(cutscene.Steps.Count == 1 ? string.Empty : "s");    }



        if (cutscene.Steps.Count > 0)    private static string FormatDuration(TimeSpan duration)

        {    {

            var firstStep = cutscene.Steps[0];        if (duration == TimeSpan.Zero)

            sb.Append($". First step: {firstStep.Type} ({FormatDuration(firstStep.Duration)})");            return "instant";

        }

        if (duration.TotalSeconds < 1)

        description = sb.ToString();            return $"{duration.TotalMilliseconds:0} ms";

        return true;

    }        if (duration.TotalMinutes < 1)

            return $"{duration.TotalSeconds:0.##} s";

    private static string Truncate(string text, int maxLength)

    {        return duration.ToString();

        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)    }

            return text;

    public IReadOnlyList<GameDataEntrySummary> GetDialogueSummaries()

        return text[..Math.Max(0, maxLength - 1)] + "";    {

    }        lock (_syncRoot)

        {

    private static string FormatDuration(TimeSpan duration)            return _repository.Dialogues.Values

    {                .OrderBy(g => g.Id, StringComparer.OrdinalIgnoreCase)

        if (duration == TimeSpan.Zero)                .Select(g => new GameDataEntrySummary(g.Id, $"{g.Id} – {g.Nodes.Count} node{(g.Nodes.Count == 1 ? string.Empty : "s")}"))

            return "instant";                .ToList();

        }

        if (duration.TotalSeconds < 1)    }

            return $"{duration.TotalMilliseconds:0} ms";

    public IReadOnlyList<GameDataEntrySummary> GetQuestSummaries()

        if (duration.TotalMinutes < 1)    {

            return $"{duration.TotalSeconds:0.##} s";        lock (_syncRoot)

        {

        return duration.ToString();            return _repository.Quests.Values

    }                .OrderBy(q => q.Id, StringComparer.OrdinalIgnoreCase)

                .Select(q => new GameDataEntrySummary(q.Id, $"{q.Name} ({q.Id}) – {q.Stages.Count} stage{(q.Stages.Count == 1 ? string.Empty : "s")}"))

    public IReadOnlyList<GameDataEntrySummary> GetDialogueSummaries()                .ToList();

    {        }

        lock (_syncRoot)    }

        {

            return _repository.Dialogues.Values    public IReadOnlyList<GameDataEntrySummary> GetCutsceneSummaries()

                .OrderBy(g => g.Id, StringComparer.OrdinalIgnoreCase)    {

                .Select(g => new GameDataEntrySummary(g.Id, $"{g.Id}  {g.Nodes.Count} node{(g.Nodes.Count == 1 ? string.Empty : "s")}"))        lock (_syncRoot)

                .ToList();        {

        }            return _repository.Cutscenes.Values

    }                .OrderBy(c => c.Id, StringComparer.OrdinalIgnoreCase)

                .Select(c => new GameDataEntrySummary(c.Id, $"{c.Id} – {c.Steps.Count} step{(c.Steps.Count == 1 ? string.Empty : "s")}"))

    public IReadOnlyList<GameDataEntrySummary> GetQuestSummaries()                .ToList();

    {        }

        lock (_syncRoot)    }

        {

            return _repository.Quests.Values    public sealed record GameDataEntrySummary(string Id, string Summary);

                .OrderBy(q => q.Id, StringComparer.OrdinalIgnoreCase)

                .Select(q => new GameDataEntrySummary(q.Id, $"{q.Name} ({q.Id})  {q.Stages.Count} stage{(q.Stages.Count == 1 ? string.Empty : "s")}"))    private void HandleCompleted(ResourceHandle<GameDataLoadReport> handle, Action<GameDataLoadReport>? callback)

                .ToList();    {

        }        try

    }        {

            callback?.Invoke(handle.Value);

    public IReadOnlyList<GameDataEntrySummary> GetCutsceneSummaries()        }

    {        finally

        lock (_syncRoot)        {

        {            ReleaseHandle(handle);

            return _repository.Cutscenes.Values        }

                .OrderBy(c => c.Id, StringComparer.OrdinalIgnoreCase)    }

                .Select(c => new GameDataEntrySummary(c.Id, $"{c.Id}  {c.Steps.Count} step{(c.Steps.Count == 1 ? string.Empty : "s")}"))

                .ToList();    private void HandleFailed(ResourceHandle<GameDataLoadReport> handle, Action<Exception>? callback)

        }    {

    }        try

        {

    public sealed record GameDataEntrySummary(string Id, string Summary);            var exception = handle.Exception ?? new InvalidOperationException("Unknown error while loading game data.");

            callback?.Invoke(exception);

    private void ExecuteReload(CancellationTokenSource cancellation, Action<GameDataLoadReport>? onCompleted, Action<Exception>? onError, string? contentRoot)        }

    {        finally

        try        {

        {            ReleaseHandle(handle);

            GameDataLoadReport report;        }

            lock (_syncRoot)    }

            {

                report = _repository.LoadAllFromContent(contentRoot);    private void ReleaseHandle(ResourceHandle<GameDataLoadReport> handle)

            }    {

        lock (_syncRoot)

            if (cancellation.IsCancellationRequested)        {

                return;            if (_resourceManager != null)

            {

            onCompleted?.Invoke(report);                _resourceManager.Release(handle);

        }            }

        catch (OperationCanceledException)

        {            if (ReferenceEquals(_activeHandle, handle))

            // Load was abandoned by a newer request; ignore.            {

        }                _activeHandle = null;

        catch (Exception ex) when (cancellation.IsCancellationRequested)            }

        {        }

            // A newer load superseded this one; ignore the error.    }

        }

        catch (Exception ex)    private static string BuildCacheKey(string? contentRoot)

        {    {

            if (!cancellation.IsCancellationRequested)        return string.IsNullOrWhiteSpace(contentRoot)

            {            ? "gamedata:preview:default"

                onError?.Invoke(ex);            : $"gamedata:preview:{contentRoot}";

            }    }

        }}

        finally
        {
            lock (_syncRoot)
            {
                if (ReferenceEquals(_reloadCancellation, cancellation))
                {
                    _reloadCancellation = null;
                    _isLoading = false;
                }
            }

            cancellation.Dispose();
        }
    }
}
