using System;
using Alta.Impact;
using Alta.Networking;
using HarmonyLib;
using RagdollLib.Runtime;
using UnityEngine;

namespace RagdollLib.Patches
{
    /// <summary>
    /// Defensive Harmony patches bridging A Township Tale's native health, explosion,
    /// scene-despawn, and pooling lifecycles into <see cref="RagdollManager"/>.
    /// </summary>
    internal static class HarmonyPatches
    {
        [ThreadStatic]
        private static Vector3? _activeExplosionOrigin;

        [HarmonyPatch(typeof(HealthObject), nameof(HealthObject.OnKilled))]
        private static class HealthObjectOnKilledPatch
        {
            private static void Postfix(HealthObject __instance)
            {
                try
                {
                    RagdollManager.Instance.HandleEntityKilled(__instance);
                }
                catch (Exception ex)
                {
                    RagdollManager.Instance.LogError($"[RagdollLib] Error in HealthObject.OnKilled postfix: {ex}");
                }
            }
        }

        [HarmonyPatch(typeof(HealthObject), nameof(HealthObject.DetectImpact), new[] { typeof(ImpactHit) })]
        private static class HealthObjectDetectImpactPatch
        {
            private static void Postfix(HealthObject __instance, ImpactHit impact)
            {
                try
                {
                    RagdollManager.Instance.HandleImpactDetected(__instance, impact, _activeExplosionOrigin);
                }
                catch (Exception ex)
                {
                    RagdollManager.Instance.LogError($"[RagdollLib] Error in HealthObject.DetectImpact postfix: {ex}");
                }
            }
        }

        [HarmonyPatch(typeof(Explosive), nameof(Explosive.Trigger))]
        private static class ExplosiveTriggerPatch
        {
            private static void Prefix(Explosive __instance)
            {
                try
                {
                    if (__instance != null)
                        _activeExplosionOrigin = __instance.transform.position;
                }
                catch (Exception ex)
                {
                    RagdollManager.Instance.LogError($"[RagdollLib] Error in Explosive.Trigger prefix: {ex}");
                }
            }

            private static void Finalizer()
            {
                _activeExplosionOrigin = null;
            }
        }

        [HarmonyPatch(typeof(NetworkEntity), nameof(NetworkEntity.OnDestroyedByScene), new[] { typeof(bool) })]
        private static class NetworkEntityDestroyedByScenePatch
        {
            private static void Prefix(NetworkEntity __instance)
            {
                try
                {
                    RagdollManager.Instance.HandleEntityDespawnOrPoolRelease(__instance, isDestroying: true);
                }
                catch (Exception ex)
                {
                    RagdollManager.Instance.LogError($"[RagdollLib] Error in NetworkEntity.OnDestroyedByScene prefix: {ex}");
                }
            }
        }

        [HarmonyPatch(typeof(NetworkEntity), "OnDestroy")]
        private static class NetworkEntityOnDestroyPatch
        {
            private static void Prefix(NetworkEntity __instance)
            {
                try
                {
                    RagdollManager.Instance.HandleEntityDespawnOrPoolRelease(__instance, isDestroying: true);
                }
                catch (Exception ex)
                {
                    RagdollManager.Instance.LogError($"[RagdollLib] Error in NetworkEntity.OnDestroy prefix: {ex}");
                }
            }
        }

        [HarmonyPatch(typeof(PooledObject), "OnReleased")]
        private static class PooledObjectOnReleasedPatch
        {
            private static void Prefix(PooledObject __instance)
            {
                try
                {
                    RagdollManager.Instance.HandleEntityDespawnOrPoolRelease(__instance, isDestroying: false);
                }
                catch (Exception ex)
                {
                    RagdollManager.Instance.LogError($"[RagdollLib] Error in PooledObject.OnReleased prefix: {ex}");
                }
            }
        }
    }
}
