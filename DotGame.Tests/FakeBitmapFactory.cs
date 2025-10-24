using System;
using System.IO;
using global::Avalonia.Media.Imaging;

namespace DotGame.Tests
{
    // A minimal fake factory that returns a tiny 1x1 transparent bitmap created from a PNG byte array.
    public class FakeBitmapFactory : Dotgame.Avalonia.Services.IBitmapFactory
    {
        private static readonly byte[] OnePixelPng = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVQImWNgYAAAAAMAAWgmWQ0AAAAASUVORK5CYII=");

        public Bitmap LoadFromStream(Stream stream)
        {
            // ignore the provided stream and create from the embedded 1x1 PNG
            using var ms = new MemoryStream(OnePixelPng);
            // Construct an Avalonia Bitmap directly to avoid calling AssetManager (which would recurse).
            ms.Position = 0;
            return new global::Avalonia.Media.Imaging.Bitmap(ms);
        }

        public Bitmap LoadFromFile(string path)
        {
            using var ms = new MemoryStream(OnePixelPng);
            ms.Position = 0;
            return new global::Avalonia.Media.Imaging.Bitmap(ms);
        }
    }
}
