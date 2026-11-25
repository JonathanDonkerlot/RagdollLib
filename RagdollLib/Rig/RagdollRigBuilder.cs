using System;
using System.Collections.Generic;
using Alta.Character;
using Alta.Impact;
using Alta.Networking;
using RagdollLib.Core.Math;
using RagdollLib.Core.Model;
using RagdollLib.Core.Rig;
using UnityEngine;

namespace RagdollLib.Rig
{
    internal static class RagdollRigBuilder
    {
        private static readonly RigBlueprintCache BlueprintCache = new RigBlueprintCache();

        public static void ClearCache() => BlueprintCache.Clear();

        public static RagdollRigInstance Build(GameObject entityRoot, Transform skeletonScope, bool isPlayerDummy)
        {
            if (entityRoot == null)
                return null;

            Transform searchRoot = skeletonScope != null ? skeletonScope : entityRoot.transform;
            List<SkinnedMeshRenderer> renderers = CollectSkinnedRenderers(searchRoot);

            if (!TryExtractBoneNodes(searchRoot, renderers, out List<Transform> transforms, out List<BoneNodeInput> inputs))
                return null;

            string cacheKey = RigBlueprintCache.BuildArchetypeKey(entityRoot, inputs.Count);
            if (!BlueprintCache.TryGet(cacheKey, out RagdollRigBlueprint blueprint))
            {
                Rigidbody rootRb = entityRoot.GetComponent<Rigidbody>();
                float entityMass = rootRb != null && rootRb.mass > 0.1f
                    ? rootRb.mass
                    : RagdollPhysicsProfile.DefaultEntityMass;

                blueprint = RagdollRigAnalyzer.Analyze(inputs, entityMass);
                if (blueprint == null)
                    return null;

                BlueprintCache.Set(cacheKey, blueprint);
            }

            var createdHandles = new List<RagdollBoneHandle>(blueprint.Bones.Count);
            var ragdollColliderSet = new HashSet<Collider>();
            var handlesByPlanIndex = new RagdollBoneHandle[blueprint.Bones.Count];

            for (int i = 0; i < blueprint.Bones.Count; i++)
            {
                RagdollBonePlan plan = blueprint.Bones[i];
                if (plan.NodeIndex < 0 || plan.NodeIndex >= transforms.Count)
                    return CleanupPartial(createdHandles);

                Transform bone = transforms[plan.NodeIndex];
                if (bone == null)
                    return CleanupPartial(createdHandles);

                Rigidbody existingRb = bone.GetComponent<Rigidbody>();
                bool createdRb = existingRb == null;
                Rigidbody rb = existingRb != null ? existingRb : bone.gameObject.AddComponent<Rigidbody>();

                // Initialize safely in kinematic discrete mode; ContinuousDynamic is applied after un-kinematicizing
                rb.mass = plan.Mass;
                rb.drag = 0.25f;
                rb.angularDrag = 2.0f;
                rb.useGravity = true;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                rb.isKinematic = true;
                rb.maxDepenetrationVelocity = 5.0f;
                rb.maxAngularVelocity = 15.0f;
                rb.solverIterations = 12;
                rb.solverVelocityIterations = 4;

                CapsuleCollider existingCol = bone.GetComponent<CapsuleCollider>();
                bool createdCol = existingCol == null;
                CapsuleCollider col = existingCol != null ? existingCol : bone.gameObject.AddComponent<CapsuleCollider>();
                col.radius = plan.CapsuleRadius;
                col.height = plan.CapsuleHeight;
                col.direction = plan.CapsuleDirectionAxis;
                col.center = new Vector3(plan.CapsuleCenterLocal.X, plan.CapsuleCenterLocal.Y, plan.CapsuleCenterLocal.Z);
                col.isTrigger = false;
                col.enabled = false;
                ragdollColliderSet.Add(col);

                ConfigurableJoint joint = null;
                bool createdJoint = false;
                if (plan.ParentPlanIndex >= 0 && plan.ParentPlanIndex < i)
                {
                    RagdollBoneHandle parentHandle = handlesByPlanIndex[plan.ParentPlanIndex];
                    if (parentHandle != null && parentHandle.Body != null)
                    {
                        joint = ConfigureJoint(bone.gameObject, parentHandle.Body, plan.Limits);
                        createdJoint = true;
                    }
                }

                var handle = new RagdollBoneHandle(bone, plan.Role, rb, col, joint, createdRb, createdCol, createdJoint);
                handlesByPlanIndex[i] = handle;
                createdHandles.Add(handle);
            }

            if (createdHandles.Count < 2)
                return CleanupPartial(createdHandles);

            // Disable self-collision between ALL ragdoll bone colliders so joints never jitter or launch
            for (int i = 0; i < createdHandles.Count; i++)
            {
                for (int j = i + 1; j < createdHandles.Count; j++)
                {
                    Physics.IgnoreCollision(createdHandles[i].Collider, createdHandles[j].Collider, true);
                }
            }

            // Collect pre-existing non-trigger colliders on the entity and ignore collisions with ragdoll bones
            var externalColliders = new List<Collider>();
            Collider[] allEntityColliders = (isPlayerDummy ? searchRoot : entityRoot.transform).GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < allEntityColliders.Length; i++)
            {
                Collider ext = allEntityColliders[i];
                if (ext == null || ext.isTrigger || ragdollColliderSet.Contains(ext))
                    continue;

                externalColliders.Add(ext);
                for (int j = 0; j < createdHandles.Count; j++)
                {
                    Physics.IgnoreCollision(ext, createdHandles[j].Collider, true);
                }
            }

            RagdollBoneHandle pelvis = createdHandles[blueprint.PelvisPlanIndex];
            return new RagdollRigInstance(pelvis, createdHandles, externalColliders, renderers, entityRoot.transform);
        }

        private static ConfigurableJoint ConfigureJoint(GameObject boneObj, Rigidbody connectedBody, JointAngularLimits limits)
        {
            ConfigurableJoint joint = boneObj.AddComponent<ConfigurableJoint>();
            joint.connectedBody = connectedBody;
            joint.anchor = Vector3.zero;
            joint.autoConfigureConnectedAnchor = true;

            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;

            joint.angularXMotion = ConfigurableJointMotion.Limited;
            joint.angularYMotion = ConfigurableJointMotion.Limited;
            joint.angularZMotion = ConfigurableJointMotion.Limited;

            joint.lowAngularXLimit = new SoftJointLimit { limit = limits.LowTwist };
            joint.highAngularXLimit = new SoftJointLimit { limit = limits.HighTwist };
            joint.angularYLimit = new SoftJointLimit { limit = limits.Swing1 };
            joint.angularZLimit = new SoftJointLimit { limit = limits.Swing2 };

            var drive = new JointDrive
            {
                positionSpring = limits.AngularSpring,
                positionDamper = limits.AngularDamper,
                maximumForce = float.MaxValue
            };
            joint.angularXDrive = drive;
            joint.angularYZDrive = drive;

            joint.enableCollision = false;
            joint.enablePreprocessing = false;
            joint.projectionMode = JointProjectionMode.PositionAndRotation;
            joint.projectionDistance = 0.05f;
            joint.projectionAngle = 10f;

            return joint;
        }

        private static List<SkinnedMeshRenderer> CollectSkinnedRenderers(Transform searchRoot)
        {
            SkinnedMeshRenderer[] found = searchRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var list = new List<SkinnedMeshRenderer>(found.Length);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null)
                    list.Add(found[i]);
            }
            return list;
        }

        private static bool TryExtractBoneNodes(
            Transform searchRoot,
            List<SkinnedMeshRenderer> renderers,
            out List<Transform> transforms,
            out List<BoneNodeInput> inputs)
        {
            transforms = new List<Transform>();
            inputs = new List<BoneNodeInput>();

            // Prefer the skeleton root from the most detailed SkinnedMeshRenderer (LOD0)
            Transform skeletonRoot = FindPrimarySkeletonRoot(searchRoot, renderers);
            if (skeletonRoot == null)
            {
                Animator anim = searchRoot.GetComponentInChildren<Animator>(true);
                if (anim != null)
                {
                    if (anim.isHuman)
                        skeletonRoot = anim.GetBoneTransform(HumanBodyBones.Hips);
                    if (skeletonRoot == null)
                        skeletonRoot = anim.transform;
                }
            }

            if (skeletonRoot == null)
                return false;

            var indexMap = new Dictionary<Transform, int>();
            TraverseHierarchy(skeletonRoot, -1, searchRoot, transforms, inputs, indexMap);
            return inputs.Count >= 2;
        }

        private static Transform FindPrimarySkeletonRoot(Transform searchRoot, List<SkinnedMeshRenderer> renderers)
        {
            SkinnedMeshRenderer best = null;
            int bestBoneCount = 0;

            for (int i = 0; i < renderers.Count; i++)
            {
                SkinnedMeshRenderer smr = renderers[i];
                if (smr == null || smr.bones == null)
                    continue;

                if (smr.bones.Length > bestBoneCount)
                {
                    bestBoneCount = smr.bones.Length;
                    best = smr;
                }
            }

            if (best == null)
                return null;

            if (best.rootBone != null && best.rootBone.IsChildOf(searchRoot))
                return best.rootBone;

            // Walk upward from first valid bone to highest bone before Animator/Body container
            Transform[] bones = best.bones;
            for (int i = 0; i < bones.Length; i++)
            {
                Transform b = bones[i];
                if (b == null || !b.IsChildOf(searchRoot))
                    continue;

                Transform cursor = b;
                while (cursor.parent != null && cursor.parent != searchRoot && cursor.parent.GetComponent<Animator>() == null)
                {
                    cursor = cursor.parent;
                }
                return cursor;
            }

            return null;
        }

        private static void TraverseHierarchy(
            Transform current,
            int parentIdx,
            Transform searchRoot,
            List<Transform> transforms,
            List<BoneNodeInput> inputs,
            Dictionary<Transform, int> indexMap)
        {
            if (current == null || indexMap.ContainsKey(current))
                return;

            bool excluded = IsProtectedComponentNode(current, searchRoot);
            int idx = transforms.Count;
            indexMap[current] = idx;
            transforms.Add(current);

            Vector3 lp = current.localPosition;
            Vector3 wp = current.position;
            inputs.Add(new BoneNodeInput(
                idx,
                parentIdx,
                current.name,
                new Vec3(lp.x, lp.y, lp.z),
                new Vec3(wp.x, wp.y, wp.z),
                excluded));

            if (excluded)
                return;

            int childCount = current.childCount;
            for (int i = 0; i < childCount; i++)
            {
                TraverseHierarchy(current.GetChild(i), idx, searchRoot, transforms, inputs, indexMap);
            }
        }

        private static bool IsProtectedComponentNode(Transform t, Transform searchRoot)
        {
            if (t == searchRoot)
                return false;

            // Embedded NetworkEntity (e.g. Spriggull jaw or player wrist_mov), Interactors, FixedImpactors, Cameras, MoveToBone
            if (t.GetComponent<NetworkEntity>() != null ||
                t.GetComponent<Interactor>() != null ||
                t.GetComponent<FixedImpactor>() != null ||
                t.GetComponent<MoveToBone>() != null ||
                t.GetComponent<Camera>() != null ||
                t.GetComponent<LODGroup>() != null)
            {
                return true;
            }

            return false;
        }

        private static RagdollRigInstance CleanupPartial(List<RagdollBoneHandle> handles)
        {
            for (int i = handles.Count - 1; i >= 0; i--)
            {
                RagdollBoneHandle h = handles[i];
                if (h.CreatedJoint && h.Joint != null) UnityEngine.Object.Destroy(h.Joint);
                if (h.CreatedCollider && h.Collider != null) UnityEngine.Object.Destroy(h.Collider);
                if (h.CreatedRigidbody && h.Body != null) UnityEngine.Object.Destroy(h.Body);
            }
            return null;
        }
    }
}
