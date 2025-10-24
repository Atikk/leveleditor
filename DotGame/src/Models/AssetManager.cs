using System;
using System.Collections.Generic;
using System.IO;
using global::Avalonia.Media.Imaging;

namespace Dotgame.Avalonia.Models
{
    public class AssetManager
    {
        private static AssetManager? instance;
        private readonly Dictionary<string, Bitmap> assetCache = new(StringComparer.Ordinal);
        private Services.IBitmapFactory bitmapFactory;
        private readonly object factorySync = new object();

        private AssetManager() : this(bitmapFactory: null) { }

        internal AssetManager(Services.IBitmapFactory? bitmapFactory = null)
        {
            this.bitmapFactory = bitmapFactory ?? new Services.AvaloniaBitmapFactory();
        }

        public static AssetManager Instance => instance ??= new AssetManager();

        public Bitmap LoadBitmap(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            if (assetCache.TryGetValue(filePath, out var bitmap))
                return bitmap;

            if (!File.Exists(filePath))
                throw new FileNotFoundException("Asset file not found.", filePath);

            Services.IBitmapFactory factory;
            lock (factorySync) { factory = bitmapFactory; }
            bitmap = factory.LoadFromFile(filePath);
            assetCache[filePath] = bitmap;
            return bitmap;
        }

        public Bitmap LoadBitmapFromStream(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            Services.IBitmapFactory factory;
            lock (factorySync) { factory = bitmapFactory; }
            return factory.LoadFromStream(stream);
        }

        // Test-support: allow tests to swap in a custom IBitmapFactory (for headless tests)
        public void SetBitmapFactory(Services.IBitmapFactory factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            lock (factorySync)
            {
                bitmapFactory = factory;
            }
        }

        public void ResetBitmapFactory()
        {
            lock (factorySync)
            {
                bitmapFactory = new Services.AvaloniaBitmapFactory();
            }
        }

        public void ClearCache()
        {
            assetCache.Clear();
        }
    }
}

