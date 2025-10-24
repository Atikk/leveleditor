using System;
using System.IO;
using global::Avalonia.Media.Imaging;

namespace Dotgame.Avalonia.Services
{
    public interface IBitmapFactory
    {
        Bitmap LoadFromStream(Stream stream);
        Bitmap LoadFromFile(string path);
    }
}
