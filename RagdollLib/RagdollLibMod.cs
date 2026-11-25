using System;
using System.IO;
using MelonLoader;
using MelonLoader.Utils;
using RagdollLib.Config;
using RagdollLib.Core.Config;
using RagdollLib.Runtime;

namespace RagdollLib
{
    /// <summary>
    /// MelonLoader entry point for <b>RagdollLib</b> — procedural active ragdoll physics
    /// and public modding API for <em>A Township Tale</em>.
    /// </summary>
    public sealed class RagdollLibMod : MelonMod
    {
        private readonly RagdollConfig _config = new RagdollConfig();
        private bool _abortedIl2Cpp;

        /// <summary>Called once by MelonLoader when the mod is initialized.</summary>
        public override void OnInitializeMelon()
        {
            if (File.Exists(Path.Combine(MelonEnvironment.GameRootDirectory, "GameAssembly.dll")))
            {
                _abortedIl2Cpp = true;
                LoggerInstance.Error("[RagdollLib] IL2CPP GameAssembly.dll detected; RagdollLib requires Mono PCVR and will remain inactive.");
                return;
            }

            _config.Initialize();
            RagdollManager.Instance.Initialize(_config, LoggerInstance);

            try
            {
                HarmonyInstance.PatchAll(typeof(RagdollLibMod).Assembly);
            }
            catch (Exception ex)
            {
                LoggerInstance.Error($"[RagdollLib] Failed to apply Harmony patches: {ex}");
            }

            RagdollConfigSnapshot snap = _config.Snapshot;
            LoggerInstance.Msg(
                $"[RagdollLib] Initialized v1.0.0 | RagdollOnDeath={snap.RagdollOnDeath}, " +
                $"RagdollOnHighImpact={snap.RagdollOnHighImpact} (Threshold={snap.HighImpactDamageThreshold:F1}, Impulse={snap.HighImpactImpulse:F1}), " +
                $"DefaultDuration={snap.DefaultRagdollDuration:F2}s, ForceMultiplier={snap.ForceMultiplier:F2}, " +
                $"AllowPlayerRagdoll={snap.AllowPlayerRagdoll}, Debug={snap.EnableDebugLogging}");
        }

        /// <summary>Refreshes validated runtime configuration when MelonPreferences reload.</summary>
        public override void OnPreferencesLoaded()
        {
            if (_abortedIl2Cpp)
                return;

            _config.Refresh();
        }

        /// <summary>Cleans up any active ragdoll controllers and cached blueprints when a scene unloads.</summary>
        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            if (_abortedIl2Cpp)
                return;

            RagdollManager.Instance.ClearAll(destroyControllers: true);
        }

        /// <summary>Cleanly restores entities and removes Harmony patches on shutdown.</summary>
        public override void OnDeinitializeMelon()
        {
            if (_abortedIl2Cpp)
                return;

            RagdollManager.Instance.ClearAll(destroyControllers: true);

            try
            {
                HarmonyInstance?.UnpatchSelf();
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning($"[RagdollLib] Error during Harmony unpatch: {ex.Message}");
            }
        }
    }
}
