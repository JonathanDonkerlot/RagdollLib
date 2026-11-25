namespace RagdollLib.Core.Config
{
    /// <summary>
    /// Immutable, validated snapshot of <c>RagdollLib</c> runtime configuration.
    /// </summary>
    public sealed class RagdollConfigSnapshot
    {
        /// <summary>Default maximum impulse magnitude cap in N*s to avoid PhysX tunneling or NaN launches.</summary>
        public const float DefaultMaxImpulseMagnitude = 1200f;

        /// <summary>Enables automatic ragdolls when entities reach 0 health (<c>HealthObject.OnKilled</c>).</summary>
        public bool RagdollOnDeath { get; }

        /// <summary>Enables automatic ragdolls when single-hit damage meets or exceeds <see cref="HighImpactDamageThreshold"/>.</summary>
        public bool RagdollOnHighImpact { get; }

        /// <summary>Minimum health damage required to trigger <see cref="RagdollOnHighImpact"/>.</summary>
        public float HighImpactDamageThreshold { get; }

        /// <summary>Base impulse magnitude applied when <see cref="RagdollOnHighImpact"/> triggers.</summary>
        public float HighImpactImpulse { get; }

        /// <summary>Default duration in seconds before active ragdolls recover.</summary>
        public float DefaultRagdollDuration { get; }

        /// <summary>Global force multiplier applied to impulses.</summary>
        public float ForceMultiplier { get; }

        /// <summary>Allows ragdoll activation on <c>PlayerCharacter</c> visual rigs.</summary>
        public bool AllowPlayerRagdoll { get; }

        /// <summary>Enables verbose debug logging prefixed with <c>[RagdollLib]</c>.</summary>
        public bool EnableDebugLogging { get; }

        /// <summary>Hard upper bound on applied impulse magnitude.</summary>
        public float MaxImpulseMagnitude { get; }

        /// <summary>Creates a validated <see cref="RagdollConfigSnapshot"/>.</summary>
        public RagdollConfigSnapshot(
            bool ragdollOnDeath = true,
            bool ragdollOnHighImpact = true,
            float highImpactDamageThreshold = 15.0f,
            float highImpactImpulse = 180.0f,
            float defaultRagdollDuration = 3.0f,
            float forceMultiplier = 1.0f,
            bool allowPlayerRagdoll = false,
            bool enableDebugLogging = false,
            float maxImpulseMagnitude = DefaultMaxImpulseMagnitude)
        {
            RagdollOnDeath = ragdollOnDeath;
            RagdollOnHighImpact = ragdollOnHighImpact;
            HighImpactDamageThreshold = Clamp(highImpactDamageThreshold, 0.1f, 10000f, 15.0f);
            HighImpactImpulse = Clamp(highImpactImpulse, 1f, 2000f, 180.0f);
            DefaultRagdollDuration = Clamp(defaultRagdollDuration, 0.2f, 60f, 3.0f);
            ForceMultiplier = Clamp(forceMultiplier, 0.05f, 10.0f, 1.0f);
            AllowPlayerRagdoll = allowPlayerRagdoll;
            EnableDebugLogging = enableDebugLogging;
            MaxImpulseMagnitude = Clamp(maxImpulseMagnitude, 50f, 5000f, DefaultMaxImpulseMagnitude);
        }

        private static float Clamp(float value, float min, float max, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return fallback;
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
