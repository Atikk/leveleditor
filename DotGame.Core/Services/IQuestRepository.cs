using DotGame.Core.Quests;

namespace DotGame.Core.Services;

public interface IQuestRepository
{
    QuestDefinition? FindById(string questId);

    IEnumerable<QuestDefinition> Enumerate();
}
