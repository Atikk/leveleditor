using System;

namespace DotGame.Core.Platform;

public readonly struct PlatformDiagnosticSnapshot
{
    public PlatformDiagnosticSnapshot(string operatingSystem, string processArchitecture, string frameworkDescription, TimeSpan processUptime)
    {
        OperatingSystem = operatingSystem;
        ProcessArchitecture = processArchitecture;
        FrameworkDescription = frameworkDescription;
        ProcessUptime = processUptime;
    }

    public string OperatingSystem { get; }

    public string ProcessArchitecture { get; }

    public string FrameworkDescription { get; }

    public TimeSpan ProcessUptime { get; }
}
