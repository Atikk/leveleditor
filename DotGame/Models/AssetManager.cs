using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Media.Imaging;

namespace DotGameAvalonia.Models
{
    public class AssetManager
    {
        private static AssetManager? instance;
        private readonly Dictionary<string, Bitmap> assetCache = new();

        private AssetManager() {}

        public static AssetManager Instance => instance ??= new AssetManager();

        public Bitmap LoadBitmap(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            if (assetCache.TryGetValue(filePath, out var bitmap))
                return bitmap;

            if (!File.Exists(filePath))
                throw new FileNotFoundException("Asset file not found.", filePath);

            bitmap = new Bitmap(filePath);
            assetCache[filePath] = bitmap;
            return bitmap;
        }

        public void ClearCache()
        {
            assetCache.Clear();
        }
    }
}