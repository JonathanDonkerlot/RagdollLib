using System;
using System.Collections.Generic;
using RagdollLib.Core.Math;

namespace RagdollLib.Core.Rules
{
    /// <summary>
    /// Thread-safe registry for matching weapon/prefab impulse rules and computing safe impulse vectors.
    /// </summary>
    public sealed class ImpulseRuleRegistry
    {
        private readonly object _sync = new object();
        private readonly List<RagdollImpulseRuleSpec> _rules = new List<RagdollImpulseRuleSpec>();

        /// <summary>Registers or replaces a rule by <see cref="RagdollImpulseRuleSpec.RuleId"/>.</summary>
        public void Register(RagdollImpulseRuleSpec rule)
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));

            lock (_sync)
            {
                if (!string.IsNullOrEmpty(rule.RuleId))
                {
                    for (int i = 0; i < _rules.Count; i++)
                    {
                        if (string.Equals(_rules[i].RuleId, rule.RuleId, StringComparison.OrdinalIgnoreCase))
                        {
                            _rules[i] = rule;
                            return;
                        }
                    }
                }
                _rules.Add(rule);
            }
        }

        /// <summary>Removes a rule by its <paramref name="ruleId"/>.</summary>
        public bool Unregister(string ruleId)
        {
            if (string.IsNullOrEmpty(ruleId))
                return false;

            lock (_sync)
            {
                for (int i = 0; i < _rules.Count; i++)
                {
                    if (string.Equals(_rules[i].RuleId, ruleId, StringComparison.OrdinalIgnoreCase))
                    {
                        _rules.RemoveAt(i);
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>Clears all registered rules.</summary>
        public void Clear()
        {
            lock (_sync)
            {
                _rules.Clear();
            }
        }

        /// <summary>Returns a snapshot copy of all registered rules.</summary>
        public IReadOnlyList<RagdollImpulseRuleSpec> GetAll()
        {
            lock (_sync)
            {
                return _rules.ToArray();
            }
        }

        /// <summary>
        /// Finds the first rule matching the source weapon/prefab name, target entity name, impact type, and damage.
        /// Returns <c>null</c> if no rule matches.
        /// </summary>
        public RagdollImpulseRuleSpec FindMatch(
            string sourceName,
            string targetName,
            int impactType,
            float damage,
            Func<RagdollImpulseRuleSpec, bool> extraFilter = null)
        {
            lock (_sync)
            {
                for (int i = 0; i < _rules.Count; i++)
                {
                    RagdollImpulseRuleSpec rule = _rules[i];
                    if (damage < rule.MinimumDamage)
                        continue;

                    if (rule.ImpactTypeFilter.HasValue && rule.ImpactTypeFilter.Value != impactType)
                        continue;

                    if (!string.IsNullOrEmpty(rule.WeaponOrPrefabPattern) &&
                        !MatchesPattern(sourceName, rule.WeaponOrPrefabPattern))
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(rule.TargetEntityPattern) &&
                        !MatchesPattern(targetName, rule.TargetEntityPattern))
                    {
                        continue;
                    }

                    if (extraFilter != null && !extraFilter(rule))
                        continue;

                    return rule;
                }
            }

            return null;
        }

        /// <summary>
        /// Matches <paramref name="text"/> against <paramref name="pattern"/> using case-insensitive
        /// wildcard matching when <c>*</c> or <c>?</c> is present, or substring matching otherwise.
        /// </summary>
        public static bool MatchesPattern(string text, string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                return true;
            if (string.IsNullOrEmpty(text))
                return false;

            if (pattern.IndexOf('*') < 0 && pattern.IndexOf('?') < 0)
                return text.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;

            return WildcardMatch(text.ToLowerInvariant(), pattern.ToLowerInvariant());
        }

        /// <summary>
        /// Computes a finite, magnitude-capped impulse vector with optional upward lift.
        /// </summary>
        public static Vec3 ComputeImpulse(
            Vec3 rawDirection,
            Vec3 fallbackDirection,
            float magnitude,
            float upwardBoost,
            float maxImpulseCap)
        {
            if (float.IsNaN(magnitude) || float.IsInfinity(magnitude) || magnitude <= 0f)
                return Vec3.Zero;

            Vec3 dir = rawDirection.IsFinite && rawDirection.SqrMagnitude > 1e-6f
                ? rawDirection.Normalized
                : (fallbackDirection.IsFinite && fallbackDirection.SqrMagnitude > 1e-6f
                    ? fallbackDirection.Normalized
                    : Vec3.Forward);

            float safeBoost = float.IsNaN(upwardBoost) || float.IsInfinity(upwardBoost) ? 0f : upwardBoost;
            Vec3 combined = (dir + (Vec3.Up * safeBoost)).Normalized;
            if (combined.SqrMagnitude < 1e-6f)
                combined = Vec3.Up;

            Vec3 result = combined * magnitude;
            return Vec3.ClampMagnitude(result, maxImpulseCap);
        }

        private static bool WildcardMatch(string text, string pattern)
        {
            int t = 0;
            int p = 0;
            int starIdx = -1;
            int matchIdx = 0;

            while (t < text.Length)
            {
                if (p < pattern.Length && (pattern[p] == '?' || pattern[p] == text[t]))
                {
                    t++;
                    p++;
                }
                else if (p < pattern.Length && pattern[p] == '*')
                {
                    starIdx = p++;
                    matchIdx = t;
                }
                else if (starIdx != -1)
                {
                    p = starIdx + 1;
                    t = ++matchIdx;
                }
                else
                {
                    return false;
                }
            }

            while (p < pattern.Length && pattern[p] == '*')
                p++;

            return p == pattern.Length;
        }
    }
}
