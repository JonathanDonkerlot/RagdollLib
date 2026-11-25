namespace RagdollLib.Core.Rules
{
    /// <summary>
    /// Engine-independent rule specification used to match weapon/prefab impacts and compute ragdoll impulses.
    /// </summary>
    public sealed class RagdollImpulseRuleSpec
    {
        /// <summary>Unique identifier for this rule.</summary>
        public string RuleId { get; }

        /// <summary>Case-insensitive wildcard (<c>*</c>/<c>?</c>) or substring pattern matched against attacker tool/prefab name.</summary>
        public string WeaponOrPrefabPattern { get; }

        /// <summary>Optional pattern matched against the struck target entity name (null/empty matches all targets).</summary>
        public string TargetEntityPattern { get; }

        /// <summary>Optional <c>Alta.Impact.ImpactType</c> integer filter (null matches all impact types).</summary>
        public int? ImpactTypeFilter { get; }

        /// <summary>Horizontal/directional impulse magnitude in N*s.</summary>
        public float ImpulseMagnitude { get; }

        /// <summary>Upward impulse fraction or lift added so struck targets clear ground friction.</summary>
        public float UpwardBoost { get; }

        /// <summary>Ragdoll duration in seconds when this rule triggers.</summary>
        public float DurationSeconds { get; }

        /// <summary>Minimum health damage required for this rule to trigger.</summary>
        public float MinimumDamage { get; }

        /// <summary>Initializes a new <see cref="RagdollImpulseRuleSpec"/>.</summary>
        public RagdollImpulseRuleSpec(
            string ruleId,
            string weaponOrPrefabPattern,
            string targetEntityPattern,
            int? impactTypeFilter,
            float impulseMagnitude,
            float upwardBoost,
            float durationSeconds,
            float minimumDamage)
        {
            RuleId = ruleId ?? string.Empty;
            WeaponOrPrefabPattern = weaponOrPrefabPattern ?? string.Empty;
            TargetEntityPattern = targetEntityPattern ?? string.Empty;
            ImpactTypeFilter = impactTypeFilter;
            ImpulseMagnitude = impulseMagnitude;
            UpwardBoost = upwardBoost;
            DurationSeconds = durationSeconds;
            MinimumDamage = minimumDamage;
        }
    }
}
