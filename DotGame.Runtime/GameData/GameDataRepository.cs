using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DotGame.Runtime.GameData;

public sealed class GameDataRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true
    };

    private readonly Dictionary<string, DialogueGraph> _dialogues = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, QuestDefinition> _quests = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CutsceneScript> _cutscenes = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, DialogueGraph> Dialogues => _dialogues;

    public IReadOnlyDictionary<string, QuestDefinition> Quests => _quests;

    public IReadOnlyDictionary<string, CutsceneScript> Cutscenes => _cutscenes;

    public GameDataLoadReport? LastLoadReport { get; private set; }

    public GameDataLoadReport LoadAllFromContent(string? contentRoot = null)
    {
        var root = contentRoot ?? Path.Combine(AppContext.BaseDirectory, "Content", "Data");
        var report = new GameDataLoadReport(DateTime.UtcNow);

        _dialogues.Clear();
        _quests.Clear();
        _cutscenes.Clear();

        LoadDialogues(Path.Combine(root, "Dialogue"), report);
        LoadQuests(Path.Combine(root, "Quests"), report);
        LoadCutscenes(Path.Combine(root, "Cutscenes"), report);

        LastLoadReport = report;
        return report;
    }

    public DialogueGraph? TryGetDialogue(string id)
    {
        return _dialogues.TryGetValue(id, out var graph) ? graph : null;
    }

    public QuestDefinition? TryGetQuest(string id)
    {
        return _quests.TryGetValue(id, out var quest) ? quest : null;
    }

    public CutsceneScript? TryGetCutscene(string id)
    {
        return _cutscenes.TryGetValue(id, out var cutscene) ? cutscene : null;
    }

    private void LoadDialogues(string directory, GameDataLoadReport report)
    {
        if (!Directory.Exists(directory))
            return;

        foreach (var filePath in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                var graph = JsonSerializer.Deserialize<DialogueGraph>(stream, JsonOptions);
                if (graph is null || string.IsNullOrWhiteSpace(graph.Id))
                {
                    report.Errors.Add(new GameDataLoadError(filePath, "Missing dialogue id."));
                    continue;
                }

                graph.Normalize();
                _dialogues[graph.Id] = graph;
                report.DialogueCount++;
            }
            catch (Exception ex)
            {
                report.Errors.Add(new GameDataLoadError(filePath, ex.Message));
            }
        }
    }

    private void LoadQuests(string directory, GameDataLoadReport report)
    {
        if (!Directory.Exists(directory))
            return;

        foreach (var filePath in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                var quest = JsonSerializer.Deserialize<QuestDefinition>(stream, JsonOptions);
                if (quest is null || string.IsNullOrWhiteSpace(quest.Id))
                {
                    report.Errors.Add(new GameDataLoadError(filePath, "Missing quest id."));
                    continue;
                }

                quest.Normalize();
                _quests[quest.Id] = quest;
                report.QuestCount++;
            }
            catch (Exception ex)
            {
                report.Errors.Add(new GameDataLoadError(filePath, ex.Message));
            }
        }
    }

    private void LoadCutscenes(string directory, GameDataLoadReport report)
    {
        if (!Directory.Exists(directory))
            return;

        foreach (var filePath in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                var cutscene = JsonSerializer.Deserialize<CutsceneScript>(stream, JsonOptions);
                if (cutscene is null || string.IsNullOrWhiteSpace(cutscene.Id))
                {
                    report.Errors.Add(new GameDataLoadError(filePath, "Missing cutscene id."));
                    continue;
                }

                cutscene.Normalize();
                _cutscenes[cutscene.Id] = cutscene;
                report.CutsceneCount++;
            }
            catch (Exception ex)
            {
                report.Errors.Add(new GameDataLoadError(filePath, ex.Message));
            }
        }
    }
}
