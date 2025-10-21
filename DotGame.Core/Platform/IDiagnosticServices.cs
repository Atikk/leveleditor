namespace DotGame.Core.Platform;

public interface IDiagnosticServices
{
    PlatformDiagnosticSnapshot CaptureSnapshot();

    void WriteTrace(string category, string message);
}
