using DotGame.Core.Dialogue;

namespace DotGame.Core.Services;

public interface IDialogueRepository
{
    DialogueGraph? FindById(string dialogueId);

    IEnumerable<DialogueGraph> Enumerate();
}
