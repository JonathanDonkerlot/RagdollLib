using RagdollLib.Core.Config;
using RagdollLib.Core.Math;
using RagdollLib.Core.Rules;
using Xunit;

namespace RagdollLib.Tests
{
    public sealed class ImpulseRuleRegistryTests
    {
        [Fact]
        public void Registry_MatchesWildcardAndThresholds_AndReplacesByRuleId()
        {
            var registry = new ImpulseRuleRegistry();
            registry.Register(new RagdollImpulseRuleSpec(
                "warhammer",
                "*Warhammer*",
                "*Spriggull*",
                impactTypeFilter: 2,
                impulseMagnitude: 320f,
                upwardBoost: 0.3f,
                durationSeconds: 4.0f,
                minimumDamage: 5f));

            Assert.Null(registry.FindMatch("Iron_Warhammer(Clone)", "Spriggull(Clone)", 2, 2.0f));
            Assert.Null(registry.FindMatch("Iron_Warhammer(Clone)", "Spriggull(Clone)", 1, 10.0f));
            Assert.Null(registry.FindMatch("Iron_Sword(Clone)", "Spriggull(Clone)", 2, 10.0f));

            var match = registry.FindMatch("Iron_Warhammer(Clone)", "Spriggull(Clone)", 2, 12.0f);
            Assert.NotNull(match);
            Assert.Equal(320f, match.ImpulseMagnitude);

            // Replace existing rule by id
            registry.Register(new RagdollImpulseRuleSpec("warhammer", "*Warhammer*", "", null, 450f, 0.2f, 3.0f, 0f));
            Assert.Single(registry.GetAll());
            Assert.Equal(450f, registry.FindMatch("Iron_Warhammer", "Gotera", 0, 1f).ImpulseMagnitude);

            Assert.True(registry.Unregister("warhammer"));
            Assert.Empty(registry.GetAll());
        }

        [Fact]
        public void ComputeImpulse_CapsMagnitudeAndHandlesNaNSafely()
        {
            Vec3 capped = ImpulseRuleRegistry.ComputeImpulse(
                new Vec3(1f, 0f, 0f),
                Vec3.Forward,
                magnitude: 5000f,
                upwardBoost: 0.5f,
                maxImpulseCap: 1200f);

            Assert.InRange(capped.Magnitude, 1199f, 1200.1f);
            Assert.True(capped.Y > 0f);

            Vec3 invalid = ImpulseRuleRegistry.ComputeImpulse(
                new Vec3(float.NaN, 0f, 0f),
                Vec3.Forward,
                magnitude: float.PositiveInfinity,
                upwardBoost: 0.2f,
                maxImpulseCap: 1200f);

            Assert.Equal(Vec3.Zero, invalid);
        }

        [Fact]
        public void ConfigSnapshot_ClampsOutOfRangeValues()
        {
            var cfg = new RagdollConfigSnapshot(
                ragdollOnDeath: true,
                ragdollOnHighImpact: false,
                highImpactDamageThreshold: -5f,
                highImpactImpulse: 99999f,
                defaultRagdollDuration: float.NaN,
                forceMultiplier: 50f,
                allowPlayerRagdoll: true,
                enableDebugLogging: true);

            Assert.Equal(0.1f, cfg.HighImpactDamageThreshold);
            Assert.Equal(2000f, cfg.HighImpactImpulse);
            Assert.Equal(3.0f, cfg.DefaultRagdollDuration);
            Assert.Equal(10.0f, cfg.ForceMultiplier);
        }
    }
}
