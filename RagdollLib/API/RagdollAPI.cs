using System;
using System.Collections.Generic;
using RagdollLib.Core.Model;
using RagdollLib.Runtime;
using UnityEngine;

namespace RagdollLib.API
{
    /// <summary>
    /// Public entry point for triggering, querying, and recovering procedural ragdoll physics
    /// on <em>A Township Tale</em> creatures, NPCs, and character rigs.
    /// </summary>
    public static class RagdollAPI
    {
        /// <summary>
        /// Fired immediately after an entity transitions into ragdoll physics.
        /// </summary>
        public static event Action<RagdollEventArgs> OnRagdollStarted;

        /// <summary>
        /// Fired after an entity cleanly finishes recovering back to animated/AI control.
        /// </summary>
        public static event Action<RagdollEventArgs> OnRagdollRecovered;

        /// <summary>
        /// Activates ragdoll physics on <paramref name="targetEntity"/>, disables conflicting Animator/AI/Walker
        /// controllers, and applies <paramref name="impulseForce"/> at <paramref name="worldHitPoint"/>.
        /// </summary>
        /// <param name="targetEntity">Any component on or inside the target entity (e.g., <c>Collider</c>, <c>ImpactDetector</c>, <c>HealthObject</c>, <c>Agent</c>, or <c>Transform</c>).</param>
        /// <param name="impulseForce">World-space impulse vector (N*s).</param>
        /// <param name="worldHitPoint">World-space point where the impact occurred; used to select the nearest limb bone.</param>
        /// <param name="durationSeconds">Seconds before automatic recovery (must be &gt; 0; clamped internally).</param>
        /// <returns><c>true</c> if ragdoll physics are active on the target; <c>false</c> if the target has no valid rig or was rejected.</returns>
        public static bool TriggerRagdoll(
            Component targetEntity,
            Vector3 impulseForce,
            Vector3 worldHitPoint,
            float durationSeconds = 3.0f)
        {
            return RagdollManager.Instance.TriggerRagdoll(
                targetEntity,
                impulseForce,
                worldHitPoint,
                hasHitPoint: true,
                durationSeconds: durationSeconds,
                isDeath: false);
        }

        /// <summary>
        /// Activates ragdoll physics on <paramref name="targetEntity"/> and applies <paramref name="impulseForce"/>
        /// at the rig's center of mass (pelvis/chest).
        /// </summary>
        /// <param name="targetEntity">Any component on or inside the target entity.</param>
        /// <param name="impulseForce">World-space impulse vector (N*s).</param>
        /// <param name="durationSeconds">Seconds before automatic recovery.</param>
        /// <returns><c>true</c> if ragdoll physics are active on the target; <c>false</c> otherwise.</returns>
        public static bool TriggerRagdoll(
            Component targetEntity,
            Vector3 impulseForce,
            float durationSeconds = 3.0f)
        {
            return RagdollManager.Instance.TriggerRagdoll(
                targetEntity,
                impulseForce,
                Vector3.zero,
                hasHitPoint: false,
                durationSeconds: durationSeconds,
                isDeath: false);
        }

        /// <summary>
        /// Cleanly exits ragdoll state on <paramref name="targetEntity"/>, realigns the root transform
        /// to safe ground using a downward raycast, and restores animation and AI locomotion.
        /// </summary>
        /// <param name="targetEntity">Any component on or inside the target entity.</param>
        public static void RecoverRagdoll(Component targetEntity)
        {
            RagdollManager.Instance.RecoverRagdoll(targetEntity);
        }

        /// <summary>
        /// Returns <c>true</c> if <paramref name="targetEntity"/> is currently in an active or death ragdoll state.
        /// </summary>
        /// <param name="targetEntity">Any component on or inside the target entity.</param>
        public static bool IsRagdolled(Component targetEntity)
        {
            return RagdollManager.Instance.IsRagdolled(targetEntity);
        }

        /// <summary>
        /// Queries the detailed <see cref="RagdollState"/> of <paramref name="targetEntity"/>.
        /// </summary>
        /// <param name="targetEntity">Any component on or inside the target entity.</param>
        public static RagdollState GetRagdollState(Component targetEntity)
        {
            return RagdollManager.Instance.GetRagdollState(targetEntity);
        }

        /// <summary>
        /// Registers a custom <see cref="RagdollImpulseRule"/> so specific weapons, prefabs, or impact types
        /// automatically trigger ragdolls without writing custom Harmony patches.
        /// </summary>
        public static void RegisterImpulseRule(RagdollImpulseRule rule)
        {
            RagdollManager.Instance.RegisterRule(rule);
        }

        /// <summary>
        /// Convenience overload that registers a weapon/prefab impulse rule and returns the created <see cref="RagdollImpulseRule"/>.
        /// </summary>
        public static RagdollImpulseRule RegisterImpulseRule(
            string weaponOrPrefabPattern,
            float impulseMagnitude,
            float upwardBoost = 0.25f,
            float durationSeconds = 3.0f,
            float minimumDamage = 0f,
            string ruleId = null)
        {
            var rule = new RagdollImpulseRule
            {
                RuleId = string.IsNullOrEmpty(ruleId) ? Guid.NewGuid().ToString("N") : ruleId,
                WeaponOrPrefabPattern = weaponOrPrefabPattern,
                ImpulseMagnitude = impulseMagnitude,
                UpwardBoost = upwardBoost,
                DurationSeconds = durationSeconds,
                MinimumDamage = minimumDamage
            };
            RegisterImpulseRule(rule);
            return rule;
        }

        /// <summary>
        /// Unregisters a previously registered <see cref="RagdollImpulseRule"/> by <paramref name="ruleId"/>.
        /// </summary>
        public static bool UnregisterImpulseRule(string ruleId)
        {
            return RagdollManager.Instance.UnregisterRule(ruleId);
        }

        /// <summary>
        /// Removes all registered <see cref="RagdollImpulseRule"/> entries.
        /// </summary>
        public static void ClearImpulseRules()
        {
            RagdollManager.Instance.ClearRules();
        }

        /// <summary>
        /// Returns a snapshot list of all currently registered <see cref="RagdollImpulseRule"/> entries.
        /// </summary>
        public static IReadOnlyList<RagdollImpulseRule> GetImpulseRules()
        {
            return RagdollManager.Instance.GetRules();
        }

        internal static void RaiseStarted(RagdollEventArgs args)
        {
            Action<RagdollEventArgs> handler = OnRagdollStarted;
            if (handler == null)
                return;

            foreach (Delegate d in handler.GetInvocationList())
            {
                try
                {
                    ((Action<RagdollEventArgs>)d)(args);
                }
                catch (Exception ex)
                {
                    RagdollManager.Instance.LogError($"[RagdollLib] Exception in OnRagdollStarted subscriber: {ex}");
                }
            }
        }

        internal static void RaiseRecovered(RagdollEventArgs args)
        {
            Action<RagdollEventArgs> handler = OnRagdollRecovered;
            if (handler == null)
                return;

            foreach (Delegate d in handler.GetInvocationList())
            {
                try
                {
                    ((Action<RagdollEventArgs>)d)(args);
                }
                catch (Exception ex)
                {
                    RagdollManager.Instance.LogError($"[RagdollLib] Exception in OnRagdollRecovered subscriber: {ex}");
                }
            }
        }
    }
}
