using System;
using System.Collections.Generic;
using global::Avalonia;
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using SkiaSharp;

namespace Dotgame.Avalonia.Models
{
    public enum Facing { Down, Left, Right, Up }
    public enum CharacterClass { Warrior, Mage, Thief }

    public struct Stats
    {
        public int MaxHP;
        public int Attack;
        public int Defense;
    }

    public sealed class Character
    {
        public int TileX { get; set; }
        public int TileY { get; set; }

        public Color Color { get; set; } = Colors.DeepSkyBlue;

        public Bitmap? Sprite { get; set; }

        public int FrameWidth { get; private set; } = 32;
        public int FrameHeight { get; private set; } = 32;
        public int TotalFrames { get; private set; } = 1;

        public Facing Direction { get; set; } = Facing.Down;
        public int FrameIndex { get; private set; } = 0;

        public CharacterClass Class { get; private set; } = CharacterClass.Warrior;

        public Stats Attributes { get; private set; }
        
        public string Name { get; private set; } = "Hero";

        public int CurrentHP { get; set; }
        public bool IsAlive => CurrentHP > 0;

        public Dictionary<AnimationState, SpriteAnimation> Animations { get; set; } = new();
        public AnimationState CurrentState { get; set; } = AnimationState.Idle;
        private SpriteAnimation? currentAnimation;

        public int AnimationDelay { get; set; } = 5;

        private int animationCounter = 0;

        public string? BehaviorScript { get; set; }
        public string? TriggerEvent { get; set; }

        public Character(int tileX, int tileY)
        {
            TileX = tileX;
            TileY = tileY;
            Class = CharacterClass.Warrior;
            Name = "Hero";
            Attributes = GetBaseStats(Class);
            CurrentHP = Attributes.MaxHP;
        }

        public Character(int tileX, int tileY, CharacterClass cls, string name)
            : this(tileX, tileY)
        {
            Class = cls;
            Name = name;
            Attributes = GetBaseStats(cls);
            CurrentHP = Attributes.MaxHP;
        }

        public static Stats GetBaseStats(CharacterClass cls)
        {
            return cls switch
            {
                CharacterClass.Warrior => new Stats { MaxHP = 30, Attack = 5, Defense = 5 },
                CharacterClass.Mage    => new Stats { MaxHP = 20, Attack = 7, Defense = 3 },
                CharacterClass.Thief   => new Stats { MaxHP = 25, Attack = 6, Defense = 4 },
                _ => new Stats { MaxHP = 10, Attack = 3, Defense = 3 },
            };
        }

        public void LoadSprite(string path, int frameW = 32, int frameH = 32, int totalFrames = 1)
        {
            Sprite = Dotgame.Avalonia.Models.AssetManager.Instance.LoadBitmap(path);
            FrameWidth = frameW;
            FrameHeight = frameH;
            TotalFrames = Math.Max(1, totalFrames);
            InitializeAnimations();
        }

        public void InitializeAnimations(int frameWidth = 32, int frameHeight = 32, int walkFrames = 3)
        {
            if (Sprite == null) return;

            FrameWidth = frameWidth;
            FrameHeight = frameHeight;
            TotalFrames = walkFrames;

            Animations[AnimationState.Idle] = new SpriteAnimation(Sprite, 1, FrameWidth, FrameHeight, (int)Direction, true);
            Animations[AnimationState.Walk] = new SpriteAnimation(Sprite, TotalFrames, FrameWidth, FrameHeight, (int)Direction, true);
            Animations[AnimationState.Attack] = new SpriteAnimation(Sprite, 1, FrameWidth, FrameHeight, (int)Direction, false);
            Animations[AnimationState.Hit] = new SpriteAnimation(Sprite, 1, FrameWidth, FrameHeight, (int)Direction, false);
            Animations[AnimationState.Death] = new SpriteAnimation(Sprite, 1, FrameWidth, FrameHeight, (int)Direction, false);
            
            SetAnimation(AnimationState.Idle);
        }

        public void Draw(SKCanvas canvas, Map map)
        {
            var rect = map.TileRect(TileX, TileY);
            var skRect = new SKRect((float)rect.X, (float)rect.Y, 
                                    (float)(rect.X + rect.Width), (float)(rect.Y + rect.Height));

            if (Sprite != null && currentAnimation != null)
            {
                using var skSprite = BitmapToSKBitmap(Sprite);
                var srcRect = currentAnimation.CurrentFrameRect();
                canvas.DrawBitmap(skSprite, srcRect, skRect);
            }
            else
            {
                var fillColor = SafeParseColor(Color.ToString(), SKColors.Red);
                var paint = new SKPaint { Color = fillColor, Style = SKPaintStyle.Fill };
                canvas.DrawRect(skRect, paint);

                var borderPaint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
                canvas.DrawRect(skRect, borderPaint);
            }
        }

        private SKColor SafeParseColor(string? hex, SKColor fallback)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return fallback;

            try
            {
                if (!hex.StartsWith("#"))
                    hex = "#" + hex;

                if (hex.Length == 7 || hex.Length == 9)
                    return SKColor.Parse(hex);
            }
            catch { }

            return fallback;
        }

        private SKBitmap BitmapToSKBitmap(Bitmap bitmap)
        {
            using var stream = new System.IO.MemoryStream();
            bitmap.Save(stream);
            stream.Position = 0;
            return SKBitmap.Decode(stream);
        }

        public void TryMove(int dx, int dy, Map map)
        {
            int nx = TileX + dx;
            int ny = TileY + dy;
            if (map.InBounds(nx, ny) && map.IsTilePassable(nx, ny))
            {
                TileX = nx;
                TileY = ny;
                UpdateDirection(dx, dy);
                if (currentAnimation != null)
                {
                    SetAnimation(AnimationState.Walk);
                }
                AdvanceFrame();
            }
        }

        private void UpdateDirection(int dx, int dy)
        {
            if (dy < 0) Direction = Facing.Up;
            else if (dy > 0) Direction = Facing.Down;
            else if (dx < 0) Direction = Facing.Left;
            else if (dx > 0) Direction = Facing.Right;
            
            if (Sprite != null && Animations.Count > 0)
            {
                var previousState = CurrentState;
                Animations[AnimationState.Idle] = new SpriteAnimation(Sprite, 1, FrameWidth, FrameHeight, (int)Direction, true);
                Animations[AnimationState.Walk] = new SpriteAnimation(Sprite, TotalFrames, FrameWidth, FrameHeight, (int)Direction, true);
                Animations[AnimationState.Attack] = new SpriteAnimation(Sprite, 1, FrameWidth, FrameHeight, (int)Direction, false);
                Animations[AnimationState.Hit] = new SpriteAnimation(Sprite, 1, FrameWidth, FrameHeight, (int)Direction, false);
                Animations[AnimationState.Death] = new SpriteAnimation(Sprite, 1, FrameWidth, FrameHeight, (int)Direction, false);
                SetAnimation(previousState);
            }
        }

        private void AdvanceFrame()
        {
            if (TotalFrames > 1)
                FrameIndex = (FrameIndex + 1) % TotalFrames;
        }

        public void SetAnimation(AnimationState state)
        {
            if (Animations.ContainsKey(state))
            {
                if (CurrentState != state)
                {
                    CurrentState = state;
                }
                currentAnimation = Animations[state];
                currentAnimation.Reset();
            }
        }

        public void UpdateAnimation()
        {
            if (currentAnimation != null)
            {
                animationCounter++;
                if (animationCounter >= AnimationDelay)
                {
                    currentAnimation.Advance();
                    animationCounter = 0;

                    if (currentAnimation.IsFinished && CurrentState == AnimationState.Attack)
                    {
                        SetAnimation(AnimationState.Idle);
                    }
                }
            }
            else if (TotalFrames > 1)
            {
                animationCounter++;
                if (animationCounter >= AnimationDelay)
                {
                    AdvanceFrame();
                    animationCounter = 0;
                }
            }
        }

        public int TakeDamage(int damage)
        {
            int actualDamage = Math.Max(1, damage);
            CurrentHP = Math.Max(0, CurrentHP - actualDamage);
            SetAnimation(AnimationState.Hit);
            
            if (CurrentHP <= 0)
            {
                SetAnimation(AnimationState.Death);
            }
            
            return actualDamage;
        }

        public int AttackTarget(Monster target)
        {
            SetAnimation(AnimationState.Attack);
            return target.TakeDamage(Attributes.Attack);
        }

        public override string ToString() => $"{Name} ({Class}) @ {TileX},{TileY}";
    }
}


