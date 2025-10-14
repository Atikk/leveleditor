using System;
using System.Collections.Generic;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace DotGameAvalonia.Models
{
    public enum MonsterType { Slime, Skeleton, Dragon }

    public class Monster
    {
        public int TileX { get; set; }
        public int TileY { get; set; }
        public MonsterType Type { get; set; }
        public string Name { get; set; }
        public int CurrentHP { get; set; }
        public int MaxHP { get; set; }
        public int Attack { get; set; }
        public int Defense { get; set; }
        public bool IsAlive => CurrentHP > 0;

        public Bitmap? Sprite { get; set; }
        public Color Color { get; set; } = Colors.Red;
        public Facing Direction { get; set; } = Facing.Down;

        public Dictionary<AnimationState, SpriteAnimation> Animations { get; set; } = new();
        public AnimationState CurrentState { get; set; } = AnimationState.Idle;
        private SpriteAnimation? currentAnimation;

        public int AnimationDelay { get; set; } = 5;
        private int animationCounter = 0;

        public int MoveDelay { get; set; } = 60;
        private int moveCounter = 0;

        public Monster(int x, int y, MonsterType type)
        {
            TileX = x;
            TileY = y;
            Type = type;
            
            var stats = GetMonsterStats(type);
            Name = stats.name;
            MaxHP = stats.hp;
            CurrentHP = stats.hp;
            Attack = stats.attack;
            Defense = stats.defense;
            Color = stats.color;
            
            InitializeAnimations();
        }

        private void InitializeAnimations()
        {
            Animations[AnimationState.Idle] = new SpriteAnimation(null, 1, 32, 32, 0, true);
            Animations[AnimationState.Walk] = new SpriteAnimation(null, 1, 32, 32, 0, true);
            Animations[AnimationState.Attack] = new SpriteAnimation(null, 1, 32, 32, 0, false);
            Animations[AnimationState.Hit] = new SpriteAnimation(null, 1, 32, 32, 0, false);
            Animations[AnimationState.Death] = new SpriteAnimation(null, 1, 32, 32, 0, false);
            
            SetAnimation(AnimationState.Idle);
        }

        private (string name, int hp, int attack, int defense, Color color) GetMonsterStats(MonsterType type)
        {
            return type switch
            {
                MonsterType.Slime => ("Slime", 15, 3, 2, Colors.LimeGreen),
                MonsterType.Skeleton => ("Skeleton", 25, 5, 3, Colors.Gray),
                MonsterType.Dragon => ("Dragon", 50, 10, 7, Colors.DarkRed),
                _ => ("Unknown", 10, 2, 1, Colors.Red)
            };
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
        }

        public bool DidMoveThisUpdate { get; private set; } = false;

        public void UpdateAI(Map map, Character player)
        {
            if (!IsAlive) return;

            DidMoveThisUpdate = false;
            moveCounter++;
            if (moveCounter >= MoveDelay)
            {
                moveCounter = 0;
                
                int dx = Math.Sign(player.TileX - TileX);
                int dy = Math.Sign(player.TileY - TileY);

                if (Math.Abs(dx) > Math.Abs(dy))
                {
                    DidMoveThisUpdate = TryMove(dx, 0, map);
                }
                else if (dy != 0)
                {
                    DidMoveThisUpdate = TryMove(0, dy, map);
                }
            }
        }

        private bool TryMove(int dx, int dy, Map map)
        {
            int nx = TileX + dx;
            int ny = TileY + dy;
            if (map.InBounds(nx, ny))
            {
                TileX = nx;
                TileY = ny;
                UpdateDirection(dx, dy);
                SetAnimation(AnimationState.Walk);
                return true;
            }
            return false;
        }

        private void UpdateDirection(int dx, int dy)
        {
            if (dy < 0) Direction = Facing.Up;
            else if (dy > 0) Direction = Facing.Down;
            else if (dx < 0) Direction = Facing.Left;
            else if (dx > 0) Direction = Facing.Right;

            if (Animations.Count > 0)
            {
                var previousState = CurrentState;
                Animations[AnimationState.Idle] = new SpriteAnimation(Sprite, 1, 32, 32, (int)Direction, true);
                Animations[AnimationState.Walk] = new SpriteAnimation(Sprite, 1, 32, 32, (int)Direction, true);
                Animations[AnimationState.Attack] = new SpriteAnimation(Sprite, 1, 32, 32, (int)Direction, false);
                Animations[AnimationState.Hit] = new SpriteAnimation(Sprite, 1, 32, 32, (int)Direction, false);
                Animations[AnimationState.Death] = new SpriteAnimation(Sprite, 1, 32, 32, (int)Direction, false);
                SetAnimation(previousState);
            }
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
                var paint = new SKPaint { Color = SKColor.Parse(Color.ToString()), Style = SKPaintStyle.Fill };
                canvas.DrawRect(skRect, paint);
                
                var borderPaint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
                canvas.DrawRect(skRect, borderPaint);
            }

            if (!IsAlive)
            {
                var deathPaint = new SKPaint { Color = new SKColor(0, 0, 0, 128), Style = SKPaintStyle.Fill };
                canvas.DrawRect(skRect, deathPaint);
            }
        }

        private SKBitmap BitmapToSKBitmap(Bitmap bitmap)
        {
            using var stream = new System.IO.MemoryStream();
            bitmap.Save(stream);
            stream.Position = 0;
            return SKBitmap.Decode(stream);
        }

        public int TakeDamage(int damage)
        {
            int actualDamage = Math.Max(1, damage - Defense);
            CurrentHP = Math.Max(0, CurrentHP - actualDamage);
            SetAnimation(AnimationState.Hit);
            
            if (CurrentHP <= 0)
            {
                SetAnimation(AnimationState.Death);
            }
            
            return actualDamage;
        }
    }
}
