using System;
using Alta.Impact;
using UnityEngine;

namespace RagdollLib.API
{
    /// <summary>
    /// Declarative rule allowing third-party mods to automatically trigger ragdolls from specific
    /// weapons, prefabs, impact types, or damage thresholds without writing Harmony patches.
    /// </summary>
    public sealed class RagdollImpulseRule
    {
        /// <summary>Unique rule identifier used for replacement or unregistration.</summary>
        public string RuleId { get; set; }

        /// <summary>
        /// Case-insensitive wildcard (<c>*</c>/<c>?</c>) or substring pattern matched against the
        /// attacking tool, prefab, or wielder name (leave empty to match any source).
        /// </summary>
        public string WeaponOrPrefabPattern { get; set; }

        /// <summary>
        /// Optional wildcard or substring pattern matched against the struck entity's root name
        /// (leave empty to match any creature or target).
        /// </summary>
        public string TargetEntityPattern { get; set; }

        /// <summary>Optional native <see cref="ImpactType"/> filter (<c>null</c> matches all impact types).</summary>
        public ImpactType? ImpactTypeFilter { get; set; }

        /// <summary>Directional impulse magnitude in N*s applied on match.</summary>
        public float ImpulseMagnitude { get; set; } = 220f;

        /// <summary>Upward lift ratio added to the impact direction so targets clear ground friction.</summary>
        public float UpwardBoost { get; set; } = 0.25f;

        /// <summary>Ragdoll duration in seconds before recovery.</summary>
        public float DurationSeconds { get; set; } = 3.0f;

        /// <summary>Minimum final <c>HealthDamage</c> required to trigger this rule.</summary>
        public float MinimumDamage { get; set; }

        /// <summary>
        /// Optional predicate receiving <c>(targetComponent, sourceComponent, healthDamage)</c>;
        /// return <c>false</c> to skip triggering.
        /// </summary>
        public Func<Component, Component, float, bool> CustomFilter { get; set; }
    }
}
