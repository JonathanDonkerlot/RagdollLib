using System;
using System.Collections.Generic;
using Alta.Character;
using Alta.Impact;
using Alta.Intelligence;
using Alta.Networking;
using MelonLoader;
using RagdollLib.API;
using RagdollLib.Config;
using RagdollLib.Core.Config;
using RagdollLib.Core.Math;
using RagdollLib.Core.Model;
using RagdollLib.Core.Rules;
using RagdollLib.Rig;
using UnityEngine;

namespace RagdollLib.Runtime
{
    internal sealed class RagdollManager
    {
        public static RagdollManager Instance { get; } = new RagdollManager();

        private readonly object _sync = new object();
        private readonly Dictionary<int, RagdollController> _controllers = new Dictionary<int, RagdollController>();
        private readonly Dictionary<RagdollImpulseRuleSpec, RagdollImpulseRule> _ruleMap = new Dictionary<RagdollImpulseRuleSpec, RagdollImpulseRule>();
        private readonly ImpulseRuleRegistry _ruleRegistry = new ImpulseRuleRegistry();

        private RagdollConfig _config;
        private MelonLogger.Instance _logger;

        private RagdollManager()
        {
        }

        public void Initialize(RagdollConfig config, MelonLogger.Instance logger)
        {
            _config = config;
            _logger = logger;
        }

        public RagdollConfigSnapshot ConfigSnapshot => _config?.Snapshot ?? new RagdollConfigSnapshot();

        public void LogDebug(string msg)
        {
            if (ConfigSnapshot.EnableDebugLogging)
                _logger?.Msg(msg);
        }

        public void LogWarning(string msg) => _logger?.Warning(msg);
        public void LogError(string msg) => _logger?.Error(msg);

        public bool TriggerRagdoll(
            Component targetEntity,
            Vector3 impulseForce,
            Vector3 worldHitPoint,
            bool hasHitPoint,
            float durationSeconds,
            bool isDeath)
        {
            if (targetEntity == null)
                return false;

            if (!TryResolveEntity(targetEntity, out GameObject entityRoot, out Transform skeletonScope, out bool isPlayer, out bool isLocalPlayer))
                return false;

            if (isPlayer && !ConfigSnapshot.AllowPlayerRagdoll)
                return false;

            RagdollController controller = GetOrCreateController(entityRoot, skeletonScope, isPlayer, isLocalPlayer);
            if (controller == null || !controller.HasValidRig)
                return false;

            RagdollConfigSnapshot cfg = ConfigSnapshot;
            float mult = cfg.ForceMultiplier;
            Vec3 rawImpulse = new Vec3(impulseForce.x * mult, impulseForce.y * mult, impulseForce.z * mult);
            Vec3 capped = rawImpulse.IsFinite
                ? Vec3.ClampMagnitude(rawImpulse, cfg.MaxImpulseMagnitude)
                : Vec3.Zero;

            Vector3 finalImpulse = new Vector3(capped.X, capped.Y, capped.Z);
            float effectiveDuration = durationSeconds > 0f ? durationSeconds : cfg.DefaultRagdollDuration;

            bool started = controller.Activate(targetEntity, finalImpulse, worldHitPoint, hasHitPoint, effectiveDuration, isDeath);
            if (started)
            {
                LogDebug($"[RagdollLib] Activated ragdoll on '{entityRoot.name}' (death={isDeath}, duration={effectiveDuration:F2}s, impulse={finalImpulse.magnitude:F1}).");
            }
            return started;
        }

        public void RecoverRagdoll(Component targetEntity)
        {
            if (!TryGetExistingController(targetEntity, out RagdollController controller))
                return;

            controller.Recover(targetEntity);
        }

        public bool IsRagdolled(Component targetEntity)
        {
            return TryGetExistingController(targetEntity, out RagdollController controller) && controller.IsRagdolled;
        }

        public RagdollState GetRagdollState(Component targetEntity)
        {
            return TryGetExistingController(targetEntity, out RagdollController controller)
                ? controller.State
                : RagdollState.Animated;
        }

        public void RegisterRule(RagdollImpulseRule rule)
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));

            if (string.IsNullOrEmpty(rule.RuleId))
                rule.RuleId = Guid.NewGuid().ToString("N");

            var spec = new RagdollImpulseRuleSpec(
                rule.RuleId,
                rule.WeaponOrPrefabPattern,
                rule.TargetEntityPattern,
                rule.ImpactTypeFilter.HasValue ? (int?)((int)rule.ImpactTypeFilter.Value) : null,
                rule.ImpulseMagnitude,
                rule.UpwardBoost,
                rule.DurationSeconds,
                rule.MinimumDamage);

            lock (_sync)
            {
                // Remove any previous mapping sharing this RuleId
                RagdollImpulseRuleSpec oldSpec = null;
                foreach (var kv in _ruleMap)
                {
                    if (string.Equals(kv.Key.RuleId, rule.RuleId, StringComparison.OrdinalIgnoreCase))
                    {
                        oldSpec = kv.Key;
                        break;
                    }
                }
                if (oldSpec != null)
                    _ruleMap.Remove(oldSpec);

                _ruleMap[spec] = rule;
                _ruleRegistry.Register(spec);
            }
        }

        public bool UnregisterRule(string ruleId)
        {
            if (string.IsNullOrEmpty(ruleId))
                return false;

            lock (_sync)
            {
                RagdollImpulseRuleSpec toRemove = null;
                foreach (var kv in _ruleMap)
                {
                    if (string.Equals(kv.Key.RuleId, ruleId, StringComparison.OrdinalIgnoreCase))
                    {
                        toRemove = kv.Key;
                        break;
                    }
                }
                if (toRemove != null)
                    _ruleMap.Remove(toRemove);

                return _ruleRegistry.Unregister(ruleId);
            }
        }

        public void ClearRules()
        {
            lock (_sync)
            {
                _ruleMap.Clear();
                _ruleRegistry.Clear();
            }
        }

        public IReadOnlyList<RagdollImpulseRule> GetRules()
        {
            lock (_sync)
            {
                var list = new List<RagdollImpulseRule>(_ruleMap.Count);
                foreach (var kv in _ruleMap)
                    list.Add(kv.Value);
                return list;
            }
        }

        public void HandleEntityKilled(HealthObject health)
        {
            if (health == null || !ConfigSnapshot.RagdollOnDeath)
                return;

            DamageData lastHit = health.LastHit;
            Vector3 impulse = Vector3.up * (ConfigSnapshot.HighImpactImpulse * 0.4f);
            Vector3 hitPoint = health.Core != null ? health.Core.position : health.transform.position;
            bool hasPoint = false;

            if (lastHit is ImpactHit ih)
            {
                Vector3 dir = ih.Direction;
                if (dir.sqrMagnitude > 1e-5f)
                    impulse = (dir.normalized + (Vector3.up * 0.35f)).normalized * ConfigSnapshot.HighImpactImpulse;
                if (ih.Point.sqrMagnitude > 1e-5f)
                {
                    hitPoint = ih.Point;
                    hasPoint = true;
                }
            }

            TriggerRagdoll(health, impulse, hitPoint, hasPoint, 0f, isDeath: true);
        }

        public void HandleImpactDetected(HealthObject health, ImpactHit impact, Vector3? explosionOrigin)
        {
            if (health == null || impact == null)
                return;

            float damage = impact.HealthDamage;
            Component sourceComp = ResolveSourceComponent(impact);
            string sourceName = ResolveSourceName(impact, sourceComp, explosionOrigin.HasValue);

            if (!TryResolveEntity(health, out GameObject entityRoot, out _, out bool isPlayer, out _))
                return;

            if (isPlayer && !ConfigSnapshot.AllowPlayerRagdoll)
                return;

            // 1. Evaluate custom modder rules first
            RagdollImpulseRuleSpec matchedSpec;
            lock (_sync)
            {
                matchedSpec = _ruleRegistry.FindMatch(
                    sourceName,
                    entityRoot.name,
                    (int)impact.ImpactType,
                    damage,
                    spec =>
                    {
                        if (_ruleMap.TryGetValue(spec, out RagdollImpulseRule r) && r.CustomFilter != null)
                            return r.CustomFilter(health, sourceComp, damage);
                        return true;
                    });
            }

            if (matchedSpec != null)
            {
                Vector3 dir = ComputeImpactDirection(impact, entityRoot.transform.position, sourceComp, explosionOrigin);
                Vec3 imp = ImpulseRuleRegistry.ComputeImpulse(
                    new Vec3(dir.x, dir.y, dir.z),
                    Vec3.Forward,
                    matchedSpec.ImpulseMagnitude,
                    matchedSpec.UpwardBoost,
                    ConfigSnapshot.MaxImpulseMagnitude);

                Vector3 hitPt = impact.Point.sqrMagnitude > 1e-5f ? impact.Point : entityRoot.transform.position;
                TriggerRagdoll(health, new Vector3(imp.X, imp.Y, imp.Z), hitPt, impact.Point.sqrMagnitude > 1e-5f, matchedSpec.DurationSeconds, isDeath: false);
                return;
            }

            // 2. Fallback to built-in high-impact gameplay toggle
            if (ConfigSnapshot.RagdollOnHighImpact && damage >= ConfigSnapshot.HighImpactDamageThreshold)
            {
                Vector3 dir = ComputeImpactDirection(impact, entityRoot.transform.position, sourceComp, explosionOrigin);
                Vec3 imp = ImpulseRuleRegistry.ComputeImpulse(
                    new Vec3(dir.x, dir.y, dir.z),
                    Vec3.Forward,
                    ConfigSnapshot.HighImpactImpulse,
                    0.30f,
                    ConfigSnapshot.MaxImpulseMagnitude);

                Vector3 hitPt = impact.Point.sqrMagnitude > 1e-5f ? impact.Point : entityRoot.transform.position;
                TriggerRagdoll(health, new Vector3(imp.X, imp.Y, imp.Z), hitPt, impact.Point.sqrMagnitude > 1e-5f, ConfigSnapshot.DefaultRagdollDuration, isDeath: false);
            }
        }

        public void HandleEntityDespawnOrPoolRelease(Component comp, bool isDestroying)
        {
            if (comp == null)
                return;

            if (TryGetExistingController(comp, out RagdollController controller))
            {
                controller.ForceResetForPoolOrDespawn(isDestroying);
            }
        }

        public void ClearAll(bool destroyControllers)
        {
            List<RagdollController> snapshot;
            lock (_sync)
            {
                snapshot = new List<RagdollController>(_controllers.Values);
                if (destroyControllers)
                    _controllers.Clear();
            }

            for (int i = 0; i < snapshot.Count; i++)
            {
                RagdollController c = snapshot[i];
                if (c != null)
                {
                    c.ForceResetForPoolOrDespawn(isDestroying: destroyControllers);
                    if (destroyControllers)
                        UnityEngine.Object.Destroy(c);
                }
            }

            RagdollRigBuilder.ClearCache();
        }

        private RagdollController GetOrCreateController(GameObject entityRoot, Transform skeletonScope, bool isPlayer, bool isLocalPlayer)
        {
            int id = entityRoot.GetInstanceID();
            lock (_sync)
            {
                if (_controllers.TryGetValue(id, out RagdollController existing) && existing != null)
                    return existing;
            }

            RagdollController created = entityRoot.GetComponent<RagdollController>();
            if (created == null)
                created = entityRoot.AddComponent<RagdollController>();

            created.Initialize(entityRoot, skeletonScope, isPlayer, isLocalPlayer, ctrl =>
            {
                lock (_sync)
                {
                    _controllers.Remove(id);
                }
            });

            lock (_sync)
            {
                _controllers[id] = created;
            }

            return created;
        }

        private bool TryGetExistingController(Component target, out RagdollController controller)
        {
            controller = null;
            if (target == null)
                return false;

            controller = target.GetComponentInParent<RagdollController>();
            if (controller != null)
                return true;

            if (TryResolveEntity(target, out GameObject root, out _, out _, out _))
            {
                int id = root.GetInstanceID();
                lock (_sync)
                {
                    if (_controllers.TryGetValue(id, out RagdollController found) && found != null)
                    {
                        controller = found;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryResolveEntity(
            Component target,
            out GameObject entityRoot,
            out Transform skeletonScope,
            out bool isPlayerCharacter,
            out bool isLocalPlayer)
        {
            entityRoot = null;
            skeletonScope = null;
            isPlayerCharacter = false;
            isLocalPlayer = false;

            if (target == null)
                return false;

            PlayerCharacter pc = target.GetComponentInParent<PlayerCharacter>();
            if (pc != null)
            {
                entityRoot = pc.gameObject;
                isPlayerCharacter = true;
                isLocalPlayer = pc.IsLocalPlayer;
                skeletonScope = pc.Dummy != null ? pc.Dummy.transform : pc.transform;
                return true;
            }

            NetworkEntity netEntity = target.GetComponentInParent<NetworkEntity>();
            if (netEntity != null)
            {
                NetworkEntity prefabRoot = netEntity.PrefabRoot != null ? netEntity.PrefabRoot : netEntity;
                entityRoot = prefabRoot.gameObject;
                skeletonScope = entityRoot.transform;
                return true;
            }

            HealthObject ho = target.GetComponentInParent<HealthObject>();
            if (ho != null)
            {
                entityRoot = ho.gameObject;
                skeletonScope = entityRoot.transform;
                return true;
            }

            Agent agent = target.GetComponentInParent<Agent>();
            if (agent != null)
            {
                entityRoot = agent.transform.parent != null ? agent.transform.parent.gameObject : agent.gameObject;
                skeletonScope = entityRoot.transform;
                return true;
            }

            Animator anim = target.GetComponentInParent<Animator>();
            if (anim != null)
            {
                entityRoot = anim.transform.root != null ? anim.transform.root.gameObject : anim.gameObject;
                skeletonScope = anim.transform;
                return true;
            }

            entityRoot = target.transform.root != null ? target.transform.root.gameObject : target.gameObject;
            skeletonScope = entityRoot.transform;
            return entityRoot != null;
        }

        private static Component ResolveSourceComponent(ImpactHit impact)
        {
            if (impact is ImpactHitWithTool toolHit && toolHit.Tool != null)
                return toolHit.Tool;
            if (impact.PlayerWielder != null)
                return impact.PlayerWielder;
            if (impact.Wielder != null)
                return impact.Wielder;
            return null;
        }

        private static string ResolveSourceName(ImpactHit impact, Component sourceComp, bool isExplosion)
        {
            if (sourceComp != null)
                return sourceComp.gameObject.name;
            if (isExplosion || impact.ImpactType == ImpactType.Explosive)
                return "Explosive";
            return impact.ImpactType.ToString();
        }

        private static Vector3 ComputeImpactDirection(
            ImpactHit impact,
            Vector3 targetPos,
            Component sourceComp,
            Vector3? explosionOrigin)
        {
            if (explosionOrigin.HasValue)
            {
                Vector3 radial = targetPos - explosionOrigin.Value;
                if (radial.sqrMagnitude > 1e-5f)
                    return radial.normalized;
            }

            if (impact.Direction.sqrMagnitude > 1e-5f)
                return impact.Direction.normalized;

            if (sourceComp != null)
            {
                Vector3 away = targetPos - sourceComp.transform.position;
                if (away.sqrMagnitude > 1e-5f)
                    return away.normalized;
            }

            if (impact.Normal.sqrMagnitude > 1e-5f)
                return (-impact.Normal).normalized;

            return Vector3.forward;
        }
    }
}
