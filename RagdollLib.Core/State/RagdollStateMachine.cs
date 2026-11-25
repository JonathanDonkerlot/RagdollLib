using RagdollLib.Core.Model;

namespace RagdollLib.Core.State
{
    /// <summary>
    /// Deterministic state machine governing transitions between <see cref="RagdollState.Animated"/>,
    /// <see cref="RagdollState.ActiveRagdoll"/>, <see cref="RagdollState.DeadRagdoll"/>,
    /// and <see cref="RagdollState.Recovering"/>.
    /// </summary>
    public sealed class RagdollStateMachine
    {
        /// <summary>Minimum allowed non-death ragdoll duration in seconds.</summary>
        public const float MinDurationSeconds = 0.05f;

        /// <summary>Maximum allowed single-trigger ragdoll duration in seconds.</summary>
        public const float MaxDurationSeconds = 120f;

        /// <summary>Current ragdoll lifecycle state.</summary>
        public RagdollState State { get; private set; } = RagdollState.Animated;

        /// <summary>Remaining duration in seconds before automatic recovery begins (zero when dead or animated).</summary>
        public float RemainingDuration { get; private set; }

        /// <summary>Total elapsed time in the current ragdoll activation.</summary>
        public float ElapsedRagdollTime { get; private set; }

        /// <summary>True when the current ragdoll is a terminal death ragdoll.</summary>
        public bool IsDeathRagdoll => State == RagdollState.DeadRagdoll;

        /// <summary>True when physics ragdoll is currently active (temporary or death).</summary>
        public bool IsRagdolled => State == RagdollState.ActiveRagdoll || State == RagdollState.DeadRagdoll;

        /// <summary>
        /// Transitions into <see cref="RagdollState.ActiveRagdoll"/> or <see cref="RagdollState.DeadRagdoll"/>.
        /// Returns true if a transition from <see cref="RagdollState.Animated"/> occurred; false if already ragdolled (timer extended).
        /// </summary>
        public bool EnterRagdoll(float durationSeconds, bool isDeath)
        {
            if (isDeath)
            {
                bool wasAnimated = State == RagdollState.Animated;
                if (wasAnimated)
                    ElapsedRagdollTime = 0f;
                State = RagdollState.DeadRagdoll;
                RemainingDuration = 0f;
                return wasAnimated;
            }

            // Never downgrade a DeadRagdoll back to timed ActiveRagdoll
            if (State == RagdollState.DeadRagdoll)
                return false;

            float clamped = ClampDuration(durationSeconds);
            bool newlyEntered = State == RagdollState.Animated || State == RagdollState.Recovering;
            if (newlyEntered)
            {
                ElapsedRagdollTime = 0f;
                RemainingDuration = clamped;
            }
            else if (clamped > RemainingDuration)
            {
                RemainingDuration = clamped;
            }

            State = RagdollState.ActiveRagdoll;
            return newlyEntered;
        }

        /// <summary>
        /// Advances the ragdoll timer by <paramref name="deltaTime"/> seconds.
        /// Returns true when <see cref="RagdollState.ActiveRagdoll"/> has elapsed and should begin recovery.
        /// </summary>
        public bool Tick(float deltaTime)
        {
            if (deltaTime < 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
                deltaTime = 0f;

            if (State == RagdollState.DeadRagdoll)
            {
                ElapsedRagdollTime += deltaTime;
                return false;
            }

            if (State != RagdollState.ActiveRagdoll)
                return false;

            ElapsedRagdollTime += deltaTime;
            RemainingDuration -= deltaTime;
            return RemainingDuration <= 0f;
        }

        /// <summary>
        /// Marks transition into <see cref="RagdollState.Recovering"/>.
        /// Returns true if recovery can proceed.
        /// </summary>
        public bool BeginRecovery()
        {
            if (State != RagdollState.ActiveRagdoll && State != RagdollState.DeadRagdoll)
                return false;

            State = RagdollState.Recovering;
            RemainingDuration = 0f;
            return true;
        }

        /// <summary>
        /// Completes recovery back to <see cref="RagdollState.Animated"/>.
        /// </summary>
        public void CompleteRecovery()
        {
            State = RagdollState.Animated;
            RemainingDuration = 0f;
            ElapsedRagdollTime = 0f;
        }

        /// <summary>
        /// Immediately resets the state machine to <see cref="RagdollState.Animated"/> (used on pool release/despawn).
        /// </summary>
        public void ForceReset()
        {
            State = RagdollState.Animated;
            RemainingDuration = 0f;
            ElapsedRagdollTime = 0f;
        }

        private static float ClampDuration(float durationSeconds)
        {
            if (float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds) || durationSeconds <= 0f)
                return MinDurationSeconds;
            if (durationSeconds < MinDurationSeconds)
                return MinDurationSeconds;
            if (durationSeconds > MaxDurationSeconds)
                return MaxDurationSeconds;
            return durationSeconds;
        }
    }
}
