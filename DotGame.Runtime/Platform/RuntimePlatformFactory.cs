using System;

namespace DotGame.Runtime.Platform;

public static class RuntimePlatformFactory
{
    private const string DefaultIdentifier = "windows";
    private const string EnvironmentVariable = "DOTGAME_PLATFORM_IMPLEMENTATION";

    public static PlatformActivation CreateFromEnvironment()
    {
        var requested = Environment.GetEnvironmentVariable(EnvironmentVariable);
        return Create(requested);
    }

    public static PlatformActivation Create(string? descriptor)
    {
        var trimmed = string.IsNullOrWhiteSpace(descriptor) ? null : descriptor.Trim();
        var normalized = (trimmed ?? DefaultIdentifier).ToLowerInvariant();
        var resolved = ResolveIdentifier(normalized, out var recognized);
        var services = CreatePlatformServices(resolved);
        return new PlatformActivation(trimmed, resolved, services, UsedFallback: !recognized && resolved == DefaultIdentifier);
    }

    private static string ResolveIdentifier(string normalized, out bool recognized)
    {
        recognized = true;
        return normalized switch
        {
            "windows" or "win" or "win32" or "win64" or "win-x64" => "windows",
            "linux" or "gnu" or "linux-x64" or "linux64" => "linux",
            "mac" or "macos" or "osx" or "osx-arm64" or "osx-x64" or "macos-arm64" => "mac",
            _ => ResolveFallback(out recognized)
        };
    }

    private static string ResolveFallback(out bool recognized)
    {
        recognized = false;
        return DefaultIdentifier;
    }

    private static DotGame.Core.Platform.IPlatformServices CreatePlatformServices(string identifier)
    {
        return identifier switch
        {
            "linux" => new LinuxPlatformServices(),
            "mac" => new MacPlatformServices(),
            _ => new WindowsPlatformServices()
        };
    }

    public sealed record PlatformActivation(string? Requested, string Resolved, DotGame.Core.Platform.IPlatformServices Services, bool UsedFallback);
}
