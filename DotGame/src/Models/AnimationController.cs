using System;
using System.Collections.Generic;

namespace Dotgame.Avalonia.Models
{
    public class AnimationController
    {
        private readonly Dictionary<AnimationState, List<AsepriteFrame>> animations = new();
        private AnimationState currentState;
        private int currentFrameIndex;
        private double elapsedTime;

        public AnimationController()
        {
            currentState = AnimationState.Idle;
            currentFrameIndex = 0;
            elapsedTime = 0;
        }

        public void AddAnimation(AnimationState state, List<AsepriteFrame> frames)
        {
            if (frames == null || frames.Count == 0)
                throw new ArgumentException("Animation frames cannot be null or empty.", nameof(frames));

            animations[state] = frames;
        }

        public void ChangeState(AnimationState newState)
        {
            if (currentState == newState) return;

            if (!animations.ContainsKey(newState))
                throw new InvalidOperationException($"No animation defined for state {newState}.");

            currentState = newState;
            currentFrameIndex = 0;
            elapsedTime = 0;
        }

        public AsepriteFrame Update(double deltaTime)
        {
            if (!animations.ContainsKey(currentState))
                throw new InvalidOperationException($"No animation defined for state {currentState}.");

            var frames = animations[currentState];
            elapsedTime += deltaTime;

            if (elapsedTime >= frames[currentFrameIndex].Duration / 1000.0)
            {
                elapsedTime = 0;
                currentFrameIndex = (currentFrameIndex + 1) % frames.Count;
            }

            return frames[currentFrameIndex];
        }

        public AnimationState CurrentState => currentState;
    }
}
