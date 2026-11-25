using System.Collections.Generic;
using Alta.Networking;
using RagdollLib.Core.Model;
using UnityEngine;

namespace RagdollLib.Rig
{
    /// <summary>
    /// Caches analyzed <see cref="RagdollRigBlueprint"/> definitions per creature/character archetype
    /// so repeated spawns avoid redundant skeletal classification.
    /// </summary>
    public sealed class RigBlueprintCache
    {
        private readonly object _sync = new object();
        private readonly Dictionary<string, RagdollRigBlueprint> _cache = new Dictionary<string, RagdollRigBlueprint>();

        /// <summary>
        /// Computes a stable cache key from an entity root <see cref="GameObject"/>.
        /// </summary>
        public static string BuildArchetypeKey(GameObject entityRoot, int boneCount)
        {
            if (entityRoot == null)
                return $"null:{boneCount}";

            NetworkEntity netEntity = entityRoot.GetComponent<NetworkEntity>();
            if (netEntity != null && netEntity.Prefab != null)
            {
                return $"prefab:{netEntity.Prefab.Hash}:{boneCount}";
            }

            string cleanName = entityRoot.name ?? "Entity";
            int cloneIdx = cleanName.IndexOf("(Clone)", System.StringComparison.Ordinal);
            if (cloneIdx > 0)
                cleanName = cleanName.Substring(0, cloneIdx);

            // Strip instance-id prefix like "65373 - Spriggull" if present
            int dashIdx = cleanName.IndexOf(" - ", System.StringComparison.Ordinal);
            if (dashIdx > 0 && int.TryParse(cleanName.Substring(0, dashIdx).Trim(), out _))
                cleanName = cleanName.Substring(dashIdx + 3);

            return $"name:{cleanName.Trim()}:{boneCount}";
        }

        /// <summary>Attempts to retrieve a cached blueprint.</summary>
        public bool TryGet(string key, out RagdollRigBlueprint blueprint)
        {
            lock (_sync)
            {
                return _cache.TryGetValue(key, out blueprint);
            }
        }

        /// <summary>Stores a validated blueprint in the cache.</summary>
        public void Set(string key, RagdollRigBlueprint blueprint)
        {
            if (string.IsNullOrEmpty(key) || blueprint == null)
                return;

            lock (_sync)
            {
                _cache[key] = blueprint;
            }
        }

        /// <summary>Clears all cached blueprints (called on scene unload).</summary>
        public void Clear()
        {
            lock (_sync)
            {
                _cache.Clear();
            }
        }
    }
}
