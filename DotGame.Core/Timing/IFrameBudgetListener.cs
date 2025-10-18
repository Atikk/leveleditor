namespace DotGame.Core.Timing;

public interface IFrameBudgetListener
{
    void OnFrameStart(in FrameTimingInfo timing);

    void OnBudgetExceeded(in FrameTimingInfo timing);

    void OnFrameEnd(in FrameTimingInfo timing);
}
