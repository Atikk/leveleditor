namespace DotGame.Core.Logging;

public interface ILogSink
{
    void Emit(in LogEvent logEvent);
}
