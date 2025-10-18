using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Dotgame.Avalonia.Models
{
    public class AsepriteFrame
    {
        public string Filename { get; set; } = string.Empty;
        public FrameData Frame { get; set; } = new FrameData();
        public bool Rotated { get; set; }
        public bool Trimmed { get; set; }
        public FrameData SpriteSourceSize { get; set; } = new FrameData();
        public Size SourceSize { get; set; } = new Size();
        public int Duration { get; set; }
    }

    public class FrameData
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int W { get; set; }
        public int H { get; set; }
    }

    public class Size
    {
        public int W { get; set; }
        public int H { get; set; }
    }

    public class AsepriteMeta
    {
        public string App { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
        public Size Size { get; set; } = new Size();
        public List<string> FrameTags { get; set; } = new List<string>();
    }

    public class AsepriteData
    {
        public List<AsepriteFrame> Frames { get; set; } = new List<AsepriteFrame>();
        public AsepriteMeta Meta { get; set; } = new AsepriteMeta();
    }

    public static class AsepriteParser
    {
        public static AsepriteData Parse(string jsonPath)
        {
            if (!File.Exists(jsonPath))
                throw new FileNotFoundException("Aseprite JSON file not found.", jsonPath);

            var json = File.ReadAllText(jsonPath);
            return JsonSerializer.Deserialize<AsepriteData>(json) ?? throw new InvalidDataException("Invalid Aseprite JSON data.");
        }
    }
}
