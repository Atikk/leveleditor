using System;
using global::Avalonia.Media.Imaging;
using SkiaSharp;

namespace Dotgame.Avalonia.Models
{
    public enum AnimationState { Idle, Walk, Attack, Hit, Death }

    public class SpriteAnimation
    {
        public Bitmap? SpriteSheet { get; set; }
        public int FrameCount { get; set; }
        public int CurrentFrame { get; set; } = 0;
        public int FrameWidth { get; set; }
        public int FrameHeight { get; set; }
        public int Row { get; set; }
        public bool Loop { get; set; } = true;
        public bool IsFinished { get; private set; } = false;

        public SpriteAnimation(Bitmap? spriteSheet, int frameCount, int frameWidth, int frameHeight, int row = 0, bool loop = true)
        {
            SpriteSheet = spriteSheet;
            FrameCount = frameCount;
            FrameWidth = frameWidth;
            FrameHeight = frameHeight;
            Row = row;
            Loop = loop;
        }

        public void Advance()
        {
            if (IsFinished && !Loop) return;

            CurrentFrame++;
            if (CurrentFrame >= FrameCount)
            {
                if (Loop)
                {
                    CurrentFrame = 0;
                }
                else
                {
                    CurrentFrame = FrameCount - 1;
                    IsFinished = true;
                }
            }
        }

        public void Reset()
        {
            CurrentFrame = 0;
            IsFinished = false;
        }

        public SKRect CurrentFrameRect()
        {
            return new SKRect(
                CurrentFrame * FrameWidth,
                Row * FrameHeight,
                (CurrentFrame + 1) * FrameWidth,
                (Row + 1) * FrameHeight
            );
        }
    }
}


