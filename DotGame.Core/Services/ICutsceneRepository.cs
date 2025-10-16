using DotGame.Core.Cutscenes;

namespace DotGame.Core.Services;

public interface ICutsceneRepository
{
    CutsceneScript? FindById(string cutsceneId);

    IEnumerable<CutsceneScript> Enumerate();
}
