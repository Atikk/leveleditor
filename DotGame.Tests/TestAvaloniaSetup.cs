using System;
using Avalonia;

// This static initializer ensures Avalonia's Skia platform is initialized for tests
// so that constructing Avalonia.Media.Imaging.Bitmap from streams works in headless test runs.
// If this fails on some CI agents, we can switch to an alternative approach.
static class TestAvaloniaSetup
{
    static TestAvaloniaSetup()
    {
        try
        {
            // Initialize Skia platform for bitmap creation
            Avalonia.Skia.SkiaPlatform.Initialize();
        }
        catch (Exception)
        {
            // best-effort; tests that require Bitmap may still fail if initialization isn't supported
        }
    }
}
