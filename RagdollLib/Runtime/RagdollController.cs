using System;
using System.Collections.Generic;
using Alta.Character;
using Alta.Intelligence;
using Alta.Networking;
using Alta.Networking.Animation;
using Alta.Pathfinding;
using HarmonyLib;
using RagdollLib.API;
using RagdollLib.Core.Model;
using RagdollLib.Core.State;
using RagdollLib.Rig;
using UnityEngine;

namespace RagdollLib.Runtime
{
    /// <summary>
    /// Per-entity MonoBehaviour managing procedural ragdoll activation, pelvis-root sync,
    /// ground-safe recovery, and pool/despawn cleanup.
    /// </summary>
    internal sealed class RagdollController : MonoBehaviour
    {
        private static readonly AccessTools.FieldRef<Walker, float> WalkerLastPhysicsHeightRef =
            ResolveWalkerField<float>("lastPhysicsHeight");
        private static readonly AccessTools.FieldRef<Walker, Vector2> WalkerVelocityXzRef =
            ResolveWalkerField<Vector2>("velocityXZ");
        private static readonly AccessTools.FieldRef<Walker, float> WalkerVelocityYRef =
            ResolveWalkerField<float>("velocityY");
        private static readonly AccessTools.FieldRef<Walker, float> WalkerLastDeltaRef =
            ResolveWalkerField<float>("lastDelta");
        private static readonly AccessTools.FieldRef<Walker, bool> WalkerIsWalkingRef =
            ResolveWalkerField<bool>("isWalking");
        private static readonly AccessTools.FieldRef<Walker, bool> WalkerIsPivotingRef =
            ResolveWalkerField<bool>("isPivoting");

        private readonly RagdollStateMachine _stateMachine = new RagdollStateMachine();

        private RagdollRigInstance _rig;
        private bool _isPlayerCharacter;
        private bool _isLocalPlayer;

        // Cached ATT components on the entity
        private NetworkEntity _networkEntity;
        private Agent _agent;
        private Walker _walker;
        private Navigator _navigator;
        private Director _director;
        private NetworkRigidbody _networkRigidbody;
        private Rigidbody _rootRigidbody;
        private CharacterIKController _ikController;
        private NetworkedIKBody _networkedIkBody;
        private BodyController _bodyController;
        private readonly List<Animator> _animators = new List<Animator>();
        private readonly List<NetworkAnimator> _networkAnimators = new List<NetworkAnimator>();
        private readonly List<WalkerAnimator> _walkerAnimators = new List<WalkerAnimator>();

        // Saved pre-ragdoll states for lossless restoration
        private bool _savedAgentEnabled;
        private bool _savedWalkerEnabled;
        private bool _savedWalkerGoActive;
        private bool _savedRootRbKinematic;
        private bool _savedRootRbUseGravity;
        private bool _savedRootRbDetectCollisions;
        private RigidbodyConstraints _savedRootRbConstraints;
        private bool _savedNetRbSuspended;
        private bool _savedIkControllerEnabled;
        private bool _savedNetIkBodyEnabled;
        private bool _savedBodyControllerEnabled;
        private bool[] _savedAnimatorEnabled;
        private bool[] _savedNetAnimatorEnabled;
        private bool[] _savedWalkerAnimatorEnabled;
        private bool _controllersDisabled;
        private bool _pelvisDetached;
        private bool _isShuttingDown;
        private Vector3 _preRagdollRootPos;
        private float _chunkEvalTimer;

        private System.Action<RagdollController> _onCleanedUp;

        public RagdollState State => _stateMachine.State;
        public bool IsRagdolled => _stateMachine.IsRagdolled;

        public void Initialize(
            GameObject entityRoot,
            Transform skeletonScope,
            bool isPlayerCharacter,
            bool isLocalPlayer,
            System.Action<RagdollController> onCleanedUp)
        {
            _isPlayerCharacter = isPlayerCharacter;
            _isLocalPlayer = isLocalPlayer;
            _onCleanedUp = onCleanedUp;

            CacheEntityComponents(entityRoot, skeletonScope);
            _rig = RagdollRigBuilder.Build(entityRoot, skeletonScope, isPlayerCharacter);
        }

        public bool HasValidRig => _rig != null;

        public bool Activate(
            Component callerComponent,
            Vector3 clampedImpulse,
            Vector3 worldHitPoint,
            bool hasHitPoint,
            float durationSeconds,
            bool isDeath)
        {
            if (_rig == null || _isShuttingDown)
                return false;

            bool newlyEntered = _stateMachine.EnterRagdoll(durationSeconds, isDeath);
            if (newlyEntered)
            {
                EnterRagdollPhysics();
            }

            ApplyImpulse(clampedImpulse, worldHitPoint, hasHitPoint);

            if (newlyEntered)
            {
                RagdollAPI.RaiseStarted(new RagdollEventArgs(
                    gameObject,
                    callerComponent != null ? callerComponent : this,
                    clampedImpulse,
                    hasHitPoint ? worldHitPoint : _rig.Pelvis.BoneTransform.position,
                    isDeath ? 0f : _stateMachine.RemainingDuration,
                    isDeath,
                    _stateMachine.State));
            }

            return true;
        }

        public void Recover(Component callerComponent)
        {
            if (_rig == null || _isShuttingDown)
                return;

            if (!_stateMachine.BeginRecovery())
                return;

            ExitRagdollPhysics(performGroundRealign: true);
            _stateMachine.CompleteRecovery();

            RagdollAPI.RaiseRecovered(new RagdollEventArgs(
                gameObject,
                callerComponent != null ? callerComponent : this,
                Vector3.zero,
                transform.position,
                0f,
                false,
                RagdollState.Animated));
        }

        public void ForceResetForPoolOrDespawn(bool isDestroying)
        {
            if (_isShuttingDown && !isDestroying)
                return;

            if (_stateMachine.IsRagdolled || _controllersDisabled || _pelvisDetached)
            {
                ExitRagdollPhysics(performGroundRealign: false, isDestroying: isDestroying);
            }

            _stateMachine.ForceReset();

            if (isDestroying)
            {
                _isShuttingDown = true;
                _onCleanedUp?.Invoke(this);
            }
        }

        private void FixedUpdate()
        {
            if (_rig == null || _isShuttingDown)
                return;

            if (_stateMachine.State != RagdollState.ActiveRagdoll && _stateMachine.State != RagdollState.DeadRagdoll)
                return;

            if (_isLocalPlayer || !_pelvisDetached || _rig.Pelvis.BoneTransform == null)
                return;

            Vector3 pelvisPos = _rig.Pelvis.BoneTransform.position;

            if (NetworkSceneManager.IsServer)
            {
                // Keep root Rigidbody awake and moving to the resting pelvis so
                // NetworkRigidbody.PrepareForWrite continuously broadcasts true position to clients.
                Vector3 targetRootPos = new Vector3(pelvisPos.x, pelvisPos.y - _rig.PelvisHeightOffset, pelvisPos.z);
                if (_rootRigidbody != null)
                {
                    _rootRigidbody.MovePosition(targetRootPos);
                    _rootRigidbody.velocity = Vector3.zero;
                    _rootRigidbody.angularVelocity = Vector3.zero;
                    _rootRigidbody.WakeUp();
                }
                else
                {
                    transform.position = targetRootPos;
                }

                _chunkEvalTimer += Time.fixedDeltaTime;
                if (_chunkEvalTimer >= 0.25f)
                {
                    _chunkEvalTimer = 0f;
                    _networkEntity?.EvaluateChunk(false);
                }
            }
            else if ((transform.position - _preRagdollRootPos).sqrMagnitude > 0.04f)
            {
                // Gently guide client pelvis toward server-synced root position when diverged > 0.25m
                Vector3 serverPelvisTarget = transform.position + new Vector3(0f, _rig.PelvisHeightOffset, 0f);
                Vector3 delta = serverPelvisTarget - pelvisPos;
                if (delta.sqrMagnitude > 0.0625f && _rig.Pelvis.Body != null && !_rig.Pelvis.Body.isKinematic)
                {
                    Vector3 desired = Vector3.ClampMagnitude(delta * 4.0f, 8.0f);
                    _rig.Pelvis.Body.AddForce(desired - (_rig.Pelvis.Body.velocity * 0.25f), ForceMode.Acceleration);
                }
            }
        }

        private void Update()
        {
            if (_rig == null || _isShuttingDown)
                return;

            if (_stateMachine.State == RagdollState.ActiveRagdoll || _stateMachine.State == RagdollState.DeadRagdoll)
            {
                if (_stateMachine.Tick(Time.deltaTime))
                {
                    Recover(this);
                }
            }
        }

        private void OnDisable()
        {
            // Defensive cleanup if entity or pool disables the GameObject directly
            if (_stateMachine.IsRagdolled || _controllersDisabled || _pelvisDetached)
            {
                ForceResetForPoolOrDespawn(isDestroying: false);
            }
        }

        private void OnDestroy()
        {
            ForceResetForPoolOrDespawn(isDestroying: true);
        }

        private void CacheEntityComponents(GameObject entityRoot, Transform skeletonScope)
        {
            _networkEntity = entityRoot.GetComponent<NetworkEntity>();
            _agent = entityRoot.GetComponentInChildren<Agent>(true);
            _walker = entityRoot.GetComponentInChildren<Walker>(true);
            _navigator = entityRoot.GetComponentInChildren<Navigator>(true);
            _director = entityRoot.GetComponentInChildren<Director>(true);
            _networkRigidbody = entityRoot.GetComponent<NetworkRigidbody>();
            _rootRigidbody = entityRoot.GetComponent<Rigidbody>();
            _ikController = entityRoot.GetComponentInChildren<CharacterIKController>(true);
            _networkedIkBody = entityRoot.GetComponentInChildren<NetworkedIKBody>(true);
            _bodyController = entityRoot.GetComponentInChildren<BodyController>(true);

            Transform animRoot = skeletonScope != null ? skeletonScope : entityRoot.transform;
            _animators.Clear();
            _animators.AddRange(animRoot.GetComponentsInChildren<Animator>(true));
            _networkAnimators.Clear();
            _networkAnimators.AddRange(entityRoot.GetComponentsInChildren<NetworkAnimator>(true));
            _walkerAnimators.Clear();
            _walkerAnimators.AddRange(entityRoot.GetComponentsInChildren<WalkerAnimator>(true));
        }

        private void EnterRagdollPhysics()
        {
            _chunkEvalTimer = 0f;

            if (!_controllersDisabled)
            {
                CaptureAndDisableControllers();
                _controllersDisabled = true;
            }

            Vector3 inheritedVelocity = _rootRigidbody != null ? _rootRigidbody.velocity : Vector3.zero;
            if (float.IsNaN(inheritedVelocity.x) || float.IsNaN(inheritedVelocity.y) || float.IsNaN(inheritedVelocity.z))
                inheritedVelocity = Vector3.zero;

            // Keep SkinnedMeshRenderers rendering while detached or spread beyond default bounds
            for (int i = 0; i < _rig.SkinnedMeshes.Count; i++)
            {
                SkinnedMeshRenderer smr = _rig.SkinnedMeshes[i];
                if (smr != null)
                    smr.updateWhenOffscreen = true;
            }

            // Disable pre-existing standing colliders so they do not prop up or collide with ragdoll limbs
            for (int i = 0; i < _rig.ExternalColliders.Count; i++)
            {
                Collider ext = _rig.ExternalColliders[i];
                if (ext != null)
                    ext.enabled = false;
            }

            // Detach pelvis from root while ragdolled (never detach local VR player camera hierarchy)
            if (!_isLocalPlayer && _rig.Pelvis.BoneTransform != null)
            {
                _rig.Pelvis.BoneTransform.SetParent(null, true);
                _pelvisDetached = true;
            }

            // Enable bone colliders and switch Rigidbodies: isKinematic=false BEFORE ContinuousDynamic
            for (int i = 0; i < _rig.Bones.Count; i++)
            {
                RagdollBoneHandle h = _rig.Bones[i];
                if (h.Collider != null)
                    h.Collider.enabled = true;

                if (h.Body != null)
                {
                    h.Body.isKinematic = false;
                    h.Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    h.Body.velocity = inheritedVelocity;
                    h.Body.angularVelocity = Vector3.zero;
                    h.Body.WakeUp();
                }
            }
        }

        private void ExitRagdollPhysics(bool performGroundRealign, bool isDestroying = false)
        {
            Vector3 pelvisWorldPos = _rig.Pelvis.BoneTransform != null
                ? _rig.Pelvis.BoneTransform.position
                : transform.position;
            Vector3 pelvisWorldForward = _rig.Pelvis.BoneTransform != null
                ? _rig.Pelvis.BoneTransform.forward
                : transform.forward;

            // Switch Rigidbodies safely: Discrete BEFORE isKinematic=true
            for (int i = 0; i < _rig.Bones.Count; i++)
            {
                RagdollBoneHandle h = _rig.Bones[i];
                if (h.Body != null)
                {
                    if (!h.Body.isKinematic)
                    {
                        h.Body.velocity = Vector3.zero;
                        h.Body.angularVelocity = Vector3.zero;
                    }
                    h.Body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                    h.Body.isKinematic = true;
                    h.Body.Sleep();
                }

                if (h.Collider != null)
                    h.Collider.enabled = false;
            }

            // Re-attach or destroy detached pelvis cleanly
            if (_pelvisDetached && _rig.Pelvis.BoneTransform != null)
            {
                if (isDestroying && (_rig.OriginalPelvisParent == null || !_rig.OriginalPelvisParent.gameObject.activeInHierarchy))
                {
                    Destroy(_rig.Pelvis.BoneTransform.gameObject);
                }
                else if (_rig.OriginalPelvisParent != null)
                {
                    _rig.Pelvis.BoneTransform.SetParent(_rig.OriginalPelvisParent, true);
                    _rig.Pelvis.BoneTransform.SetSiblingIndex(_rig.OriginalPelvisSiblingIndex);
                }
                _pelvisDetached = false;
            }

            Vector3 recoveredPos = transform.position;
            bool hasRecoveredPos = false;
            if (performGroundRealign && !_isLocalPlayer && !isDestroying)
            {
                recoveredPos = RealignRootToGround(pelvisWorldPos, pelvisWorldForward);
                hasRecoveredPos = true;
            }

            // Restore original local bone pose before re-enabling Animator
            if (!isDestroying)
            {
                for (int i = 0; i < _rig.Bones.Count; i++)
                {
                    RagdollBoneHandle h = _rig.Bones[i];
                    if (h.BoneTransform == null)
                        continue;

                    h.BoneTransform.localPosition = h.OriginalLocalPosition;
                    h.BoneTransform.localRotation = h.OriginalLocalRotation;
                }
            }

            for (int i = 0; i < _rig.SkinnedMeshes.Count; i++)
            {
                SkinnedMeshRenderer smr = _rig.SkinnedMeshes[i];
                if (smr != null)
                    smr.updateWhenOffscreen = _rig.OriginalUpdateWhenOffscreen[i];
            }

            for (int i = 0; i < _rig.ExternalColliders.Count; i++)
            {
                Collider ext = _rig.ExternalColliders[i];
                if (ext != null)
                    ext.enabled = _rig.OriginalExternalColliderEnabled[i];
            }

            if (_controllersDisabled)
            {
                RestoreControllers(isDestroying, recoveredPos, hasRecoveredPos);
                _controllersDisabled = false;
            }
        }

        private void CaptureAndDisableControllers()
        {
            _preRagdollRootPos = transform.position;

            if (_agent != null)
            {
                _savedAgentEnabled = _agent.IsEnabled;
                if (NetworkSceneManager.IsServer)
                    _agent.IsEnabled = false;
            }

            if (_walker != null)
            {
                _savedWalkerEnabled = _walker.enabled;
                _savedWalkerGoActive = _walker.gameObject.activeSelf;
                if (NetworkSceneManager.IsServer)
                    _walker.StopNormalizedMove();
                _walker.enabled = false;
            }

            if (_navigator != null && NetworkSceneManager.IsServer)
            {
                _navigator.Stop();
            }

            if (_director != null && NetworkSceneManager.IsServer)
            {
                _director.Stop();
            }

            if (_networkRigidbody != null)
            {
                _savedNetRbSuspended = _networkRigidbody.isSuspended;
                if (!NetworkSceneManager.IsServer)
                    _networkRigidbody.isSuspended = true;
            }

            if (_rootRigidbody != null)
            {
                _savedRootRbKinematic = _rootRigidbody.isKinematic;
                _savedRootRbUseGravity = _rootRigidbody.useGravity;
                _savedRootRbDetectCollisions = _rootRigidbody.detectCollisions;
                _savedRootRbConstraints = _rootRigidbody.constraints;

                _rootRigidbody.velocity = Vector3.zero;
                _rootRigidbody.angularVelocity = Vector3.zero;
                _rootRigidbody.useGravity = false;
                _rootRigidbody.detectCollisions = false;

                if (NetworkSceneManager.IsServer)
                {
                    // Keep non-kinematic and awake on server so NetworkRigidbody.PrepareForWrite syncs MovePosition
                    _rootRigidbody.isKinematic = false;
                    _rootRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
                    _rootRigidbody.WakeUp();
                }
                else
                {
                    _rootRigidbody.isKinematic = true;
                }
            }

            if (_isPlayerCharacter)
            {
                if (_ikController != null)
                {
                    _savedIkControllerEnabled = _ikController.enabled;
                    _ikController.enabled = false;
                }
                if (_networkedIkBody != null)
                {
                    _savedNetIkBodyEnabled = _networkedIkBody.enabled;
                    _networkedIkBody.enabled = false;
                }
                if (_bodyController != null)
                {
                    _savedBodyControllerEnabled = _bodyController.enabled;
                    _bodyController.DisableTracking();
                    _bodyController.enabled = false;
                }
            }

            _savedWalkerAnimatorEnabled = new bool[_walkerAnimators.Count];
            for (int i = 0; i < _walkerAnimators.Count; i++)
            {
                WalkerAnimator wa = _walkerAnimators[i];
                _savedWalkerAnimatorEnabled[i] = wa != null && wa.enabled;
                if (wa != null)
                    wa.enabled = false;
            }

            _savedNetAnimatorEnabled = new bool[_networkAnimators.Count];
            for (int i = 0; i < _networkAnimators.Count; i++)
            {
                NetworkAnimator na = _networkAnimators[i];
                _savedNetAnimatorEnabled[i] = na != null && na.enabled;
                if (na != null)
                    na.enabled = false;
            }

            _savedAnimatorEnabled = new bool[_animators.Count];
            for (int i = 0; i < _animators.Count; i++)
            {
                Animator anim = _animators[i];
                _savedAnimatorEnabled[i] = anim != null && anim.enabled;
                if (anim != null)
                    anim.enabled = false;
            }
        }

        private void RestoreControllers(bool isDestroying, Vector3 recoveredPosition, bool hasRecoveredPosition)
        {
            if (_savedAnimatorEnabled != null)
            {
                for (int i = 0; i < _animators.Count && i < _savedAnimatorEnabled.Length; i++)
                {
                    if (_animators[i] != null)
                    {
                        _animators[i].enabled = _savedAnimatorEnabled[i];
                        if (!isDestroying && _savedAnimatorEnabled[i])
                            _animators[i].Rebind();
                    }
                }
            }

            if (_savedNetAnimatorEnabled != null)
            {
                for (int i = 0; i < _networkAnimators.Count && i < _savedNetAnimatorEnabled.Length; i++)
                {
                    if (_networkAnimators[i] != null)
                        _networkAnimators[i].enabled = _savedNetAnimatorEnabled[i];
                }
            }

            if (_savedWalkerAnimatorEnabled != null)
            {
                for (int i = 0; i < _walkerAnimators.Count && i < _savedWalkerAnimatorEnabled.Length; i++)
                {
                    if (_walkerAnimators[i] != null)
                        _walkerAnimators[i].enabled = _savedWalkerAnimatorEnabled[i];
                }
            }

            if (_isPlayerCharacter)
            {
                if (_bodyController != null)
                {
                    _bodyController.enabled = _savedBodyControllerEnabled;
                    if (_savedBodyControllerEnabled)
                        _bodyController.EnableTracking();
                }
                if (_networkedIkBody != null)
                    _networkedIkBody.enabled = _savedNetIkBodyEnabled;
                if (_ikController != null)
                    _ikController.enabled = _savedIkControllerEnabled;
            }

            if (_rootRigidbody != null)
            {
                _rootRigidbody.useGravity = _savedRootRbUseGravity;
                _rootRigidbody.constraints = _savedRootRbConstraints;
                _rootRigidbody.detectCollisions = _savedRootRbDetectCollisions;
                _rootRigidbody.isKinematic = _savedRootRbKinematic;
                if (!isDestroying && hasRecoveredPosition)
                {
                    _rootRigidbody.position = recoveredPosition;
                    _rootRigidbody.velocity = Vector3.zero;
                    _rootRigidbody.angularVelocity = Vector3.zero;
                    Physics.SyncTransforms();
                    if (NetworkSceneManager.IsServer && !_savedRootRbKinematic)
                        _rootRigidbody.WakeUp();
                }
            }

            if (_networkRigidbody != null)
            {
                _networkRigidbody.isSuspended = _savedNetRbSuspended;
            }

            if (_walker != null)
            {
                if (!isDestroying && NetworkSceneManager.IsServer)
                {
                    if (_savedWalkerGoActive && !_walker.gameObject.activeSelf)
                        _walker.gameObject.SetActive(true);
                    if (hasRecoveredPosition)
                        ResetWalkerState(recoveredPosition.y);
                    _walker.ClearBlocked();
                }
                _walker.enabled = _savedWalkerEnabled;
            }

            if (!isDestroying && NetworkSceneManager.IsServer)
            {
                if (hasRecoveredPosition)
                {
                    if (_director != null)
                        _director.TeleportTo(recoveredPosition, true);
                    else if (_navigator != null)
                        _navigator.TeleportTo(recoveredPosition, true);
                }
                else if (_navigator != null)
                {
                    _navigator.ResetPosition();
                }

                _networkEntity?.EvaluateChunk(false);
            }

            if (_agent != null && !isDestroying && NetworkSceneManager.IsServer)
            {
                _agent.IsEnabled = _savedAgentEnabled;
            }
        }

        private void ApplyImpulse(Vector3 impulse, Vector3 worldHitPoint, bool hasHitPoint)
        {
            if (impulse.sqrMagnitude < 1e-6f)
                return;

            if (hasHitPoint)
            {
                RagdollBoneHandle targetBone = _rig.FindClosestBone(worldHitPoint);
                if (targetBone != null && targetBone.Body != null)
                {
                    targetBone.Body.AddForceAtPosition(impulse * 0.70f, worldHitPoint, ForceMode.Impulse);
                }

                if (_rig.Pelvis.Body != null)
                {
                    _rig.Pelvis.Body.AddForce(impulse * 0.30f, ForceMode.Impulse);
                }
            }
            else
            {
                // Distribute impulse across pelvis and torso bones for natural center-of-mass knockback
                if (_rig.Pelvis.Body != null)
                {
                    _rig.Pelvis.Body.AddForce(impulse * 0.55f, ForceMode.Impulse);
                }

                var torso = new List<Rigidbody>();
                for (int i = 0; i < _rig.Bones.Count; i++)
                {
                    RagdollBoneHandle h = _rig.Bones[i];
                    if (h != _rig.Pelvis && h.Body != null &&
                        (h.Role == BoneRole.Spine || h.Role == BoneRole.Chest || h.Role == BoneRole.Head || h.Role == BoneRole.GenericSegment))
                    {
                        torso.Add(h.Body);
                    }
                }

                if (torso.Count > 0)
                {
                    Vector3 share = (impulse * 0.45f) / torso.Count;
                    for (int i = 0; i < torso.Count; i++)
                        torso[i].AddForce(share, ForceMode.Impulse);
                }
                else if (_rig.Pelvis.Body != null)
                {
                    _rig.Pelvis.Body.AddForce(impulse * 0.45f, ForceMode.Impulse);
                }
            }
        }

        private Vector3 RealignRootToGround(Vector3 pelvisWorldPos, Vector3 pelvisForward)
        {
            int layerMask = _walker != null && _walker.GroundLayers.value != 0
                ? _walker.GroundLayers.value
                : Physics.DefaultRaycastLayers;

            // On remote clients, prefer server-synced root X/Z if server moved the entity during ragdoll
            Vector3 anchorPos = pelvisWorldPos;
            if (!NetworkSceneManager.IsServer &&
                (transform.position - _preRagdollRootPos).sqrMagnitude > 0.04f &&
                (transform.position - pelvisWorldPos).sqrMagnitude < 16f)
            {
                anchorPos = new Vector3(transform.position.x, pelvisWorldPos.y, transform.position.z);
            }

            Vector3 rayOrigin = anchorPos + (Vector3.up * 0.75f);
            Vector3 targetPos = new Vector3(anchorPos.x, pelvisWorldPos.y - _rig.PelvisHeightOffset, anchorPos.z);

            RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 6.0f, layerMask, QueryTriggerInteraction.Ignore);
            float bestDist = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCol = hits[i].collider;
                if (hitCol == null || hitCol.transform.IsChildOf(transform))
                    continue;
                if (hitCol.GetComponentInParent<RagdollController>() != null ||
                    hitCol.GetComponentInParent<PlayerCharacter>() != null)
                    continue;

                if (hits[i].distance < bestDist)
                {
                    bestDist = hits[i].distance;
                    targetPos = hits[i].point + new Vector3(0f, 0.05f, 0f);
                }
            }

            Quaternion uprightRot = transform.rotation;
            Vector3 flatForward = new Vector3(pelvisForward.x, 0f, pelvisForward.z);
            if (flatForward.sqrMagnitude > 1e-4f)
                uprightRot = Quaternion.LookRotation(flatForward.normalized, Vector3.up);

            transform.SetPositionAndRotation(targetPos, uprightRot);
            if (_rootRigidbody != null)
            {
                _rootRigidbody.position = targetPos;
                _rootRigidbody.rotation = uprightRot;
                _rootRigidbody.velocity = Vector3.zero;
                _rootRigidbody.angularVelocity = Vector3.zero;
            }
            Physics.SyncTransforms();

            return targetPos;
        }

        private void ResetWalkerState(float groundY)
        {
            if (_walker == null)
                return;

            try
            {
                if (WalkerLastPhysicsHeightRef != null) WalkerLastPhysicsHeightRef(_walker) = groundY;
                if (WalkerVelocityXzRef != null) WalkerVelocityXzRef(_walker) = Vector2.zero;
                if (WalkerVelocityYRef != null) WalkerVelocityYRef(_walker) = 0f;
                if (WalkerLastDeltaRef != null) WalkerLastDeltaRef(_walker) = 0f;
                if (WalkerIsWalkingRef != null) WalkerIsWalkingRef(_walker) = false;
                if (WalkerIsPivotingRef != null) WalkerIsPivotingRef(_walker) = false;
            }
            catch
            {
                // Ignore if private field layout differs.
            }
        }

        private static AccessTools.FieldRef<Walker, T> ResolveWalkerField<T>(string fieldName)
        {
            try
            {
                return AccessTools.FieldRefAccess<Walker, T>(fieldName);
            }
            catch
            {
                return null;
            }
        }
    }
}
