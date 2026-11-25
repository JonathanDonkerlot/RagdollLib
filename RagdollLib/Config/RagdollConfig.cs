using MelonLoader;
using RagdollLib.Core.Config;

namespace RagdollLib.Config
{
    /// <summary>
    /// MelonPreferences configuration wrapper under <c>[RagdollLib]</c> in <c>UserData/MelonPreferences.cfg</c>.
    /// </summary>
    public sealed class RagdollConfig
    {
        private MelonPreferences_Category _category;

        /// <summary>Enables automatic ragdoll physics when an entity reaches zero health (<c>HealthObject.OnKilled</c>).</summary>
        public MelonPreferences_Entry<bool> RagdollOnDeath { get; private set; }

        /// <summary>Enables automatic ragdoll physics when a single hit deals at least <see cref="HighImpactDamageThreshold"/> damage.</summary>
        public MelonPreferences_Entry<bool> RagdollOnHighImpact { get; private set; }

        /// <summary>Minimum <c>HealthDamage</c> required to trigger <see cref="RagdollOnHighImpact"/>.</summary>
        public MelonPreferences_Entry<float> HighImpactDamageThreshold { get; private set; }

        /// <summary>Base impulse magnitude (N*s) applied when <see cref="RagdollOnHighImpact"/> triggers.</summary>
        public MelonPreferences_Entry<float> HighImpactImpulse { get; private set; }

        /// <summary>Default duration in seconds before active ragdolls recover.</summary>
        public MelonPreferences_Entry<float> DefaultRagdollDuration { get; private set; }

        /// <summary>Global force multiplier applied to ragdoll impulses.</summary>
        public MelonPreferences_Entry<float> ForceMultiplier { get; private set; }

        /// <summary>Allows triggering ragdolls on <c>PlayerCharacter</c> visual rigs.</summary>
        public MelonPreferences_Entry<bool> AllowPlayerRagdoll { get; private set; }

        /// <summary>Enables verbose <c>[RagdollLib]</c> diagnostic logging.</summary>
        public MelonPreferences_Entry<bool> EnableDebugLogging { get; private set; }

        /// <summary>Current validated immutable snapshot of all preferences.</summary>
        public RagdollConfigSnapshot Snapshot { get; private set; } = new RagdollConfigSnapshot();

        /// <summary>Creates the <c>[RagdollLib]</c> preference category and entries.</summary>
        public void Initialize()
        {
            _category = MelonPreferences.CreateCategory("RagdollLib", "RagdollLib");

            RagdollOnDeath = _category.CreateEntry(
                "RagdollOnDeath",
                true,
                "Ragdoll On Death",
                "Automatically enables procedural ragdoll physics when creatures/entities are killed.",
                false,
                false,
                null);

            RagdollOnHighImpact = _category.CreateEntry(
                "RagdollOnHighImpact",
                true,
                "Ragdoll On High Impact",
                "Automatically triggers temporary ragdoll physics when a single hit exceeds HighImpactDamageThreshold.",
                false,
                false,
                null);

            HighImpactDamageThreshold = _category.CreateEntry(
                "HighImpactDamageThreshold",
                15.0f,
                "High Impact Damage Threshold",
                "Minimum health damage required for built-in high-impact ragdoll triggering.",
                false,
                false,
                null);

            HighImpactImpulse = _category.CreateEntry(
                "HighImpactImpulse",
                180.0f,
                "High Impact Impulse",
                "Base impulse magnitude (N*s) applied on built-in high-impact triggers.",
                false,
                false,
                null);

            DefaultRagdollDuration = _category.CreateEntry(
                "DefaultRagdollDuration",
                3.0f,
                "Default Ragdoll Duration",
                "Default duration in seconds before temporary ragdolls recover.",
                false,
                false,
                null);

            ForceMultiplier = _category.CreateEntry(
                "ForceMultiplier",
                1.0f,
                "Force Multiplier",
                "Global multiplier (0.05 to 10.0) applied to ragdoll impulses.",
                false,
                false,
                null);

            AllowPlayerRagdoll = _category.CreateEntry(
                "AllowPlayerRagdoll",
                false,
                "Allow Player Ragdoll",
                "Allows triggering ragdolls on PlayerCharacter visual rigs (disabled by default).",
                false,
                false,
                null);

            EnableDebugLogging = _category.CreateEntry(
                "EnableDebugLogging",
                false,
                "Enable Debug Logging",
                "Logs detailed rig generation, impulse rule, and recovery diagnostics.",
                false,
                false,
                null);

            Refresh();
        }

        /// <summary>Rebuilds <see cref="Snapshot"/> from current preference values.</summary>
        public void Refresh()
        {
            Snapshot = new RagdollConfigSnapshot(
                ragdollOnDeath: RagdollOnDeath?.Value ?? true,
                ragdollOnHighImpact: RagdollOnHighImpact?.Value ?? true,
                highImpactDamageThreshold: HighImpactDamageThreshold?.Value ?? 15.0f,
                highImpactImpulse: HighImpactImpulse?.Value ?? 180.0f,
                defaultRagdollDuration: DefaultRagdollDuration?.Value ?? 3.0f,
                forceMultiplier: ForceMultiplier?.Value ?? 1.0f,
                allowPlayerRagdoll: AllowPlayerRagdoll?.Value ?? false,
                enableDebugLogging: EnableDebugLogging?.Value ?? false);
        }
    }
}
