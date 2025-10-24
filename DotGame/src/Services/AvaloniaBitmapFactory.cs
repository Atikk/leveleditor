using System;
using System.IO;
using global::Avalonia.Media.Imaging;

namespace Dotgame.Avalonia.Services
{
    public sealed class AvaloniaBitmapFactory : IBitmapFactory
    {
        public Bitmap LoadFromStream(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            return new Bitmap(stream);
        }

        public Bitmap LoadFromFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
            return new Bitmap(path);
        }
    }
}
