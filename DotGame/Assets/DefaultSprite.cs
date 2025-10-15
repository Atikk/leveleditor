using System.IO;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace DotGameAvalonia.Assets
{
    public static class DefaultSprite
    {
        // Generate a simple colored 3x4 sprite sheet (3 frames per row, 4 directions)
        public static Bitmap GetDefaultSprite(int frameWidth = 32, int frameHeight = 32, int framesPerRow = 3, int rows = 4)
        {
            int w = frameWidth * framesPerRow;
            int h = frameHeight * rows;

            using var skBmp = new SKBitmap(w, h);
            using var canvas = new SKCanvas(skBmp);
            canvas.Clear(SKColors.Transparent);

            var rowColors = new SKColor[] { SKColors.Blue, SKColors.Green, SKColors.Orange, SKColors.Purple };
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < framesPerRow; c++)
                {
                    var paint = new SKPaint { Color = rowColors[r % rowColors.Length], IsAntialias = false };
                    // draw a smaller rectangle inside each frame so borders are visible
                    var rect = new SKRect(c * frameWidth + 2, r * frameHeight + 2, (c + 1) * frameWidth - 2, (r + 1) * frameHeight - 2);
                    canvas.DrawRect(rect, paint);
                    // add a small eye or detail to vary frames
                    var eyePaint = new SKPaint { Color = SKColors.Black }; 
                    canvas.DrawCircle(c * frameWidth + frameWidth / 2 + (c - 1)*2, r * frameHeight + frameHeight / 2, 3, eyePaint);
                }
            }

            canvas.Flush();
            using var img = SKImage.FromBitmap(skBmp);
            using var data = img.Encode(SKEncodedImageFormat.Png, 100);
            var ms = new MemoryStream();
            data.SaveTo(ms);
            ms.Position = 0;
            return new Bitmap(ms);
        }
    }
}
