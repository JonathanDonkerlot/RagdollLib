using System.Collections.Generic;
using UnityEngine;

namespace RagdollLib.Rig
{
    internal sealed class RagdollRigInstance
    {
        public RagdollBoneHandle Pelvis { get; }
        public IReadOnlyList<RagdollBoneHandle> Bones { get; }
        public Transform OriginalPelvisParent { get; }
        public int OriginalPelvisSiblingIndex { get; }
        public IReadOnlyList<Collider> ExternalColliders { get; }
        public IReadOnlyList<SkinnedMeshRenderer> SkinnedMeshes { get; }
        public bool[] OriginalUpdateWhenOffscreen { get; }
        public bool[] OriginalExternalColliderEnabled { get; }
        public float PelvisHeightOffset { get; }

        public RagdollRigInstance(
            RagdollBoneHandle pelvis,
            IReadOnlyList<RagdollBoneHandle> bones,
            IReadOnlyList<Collider> externalColliders,
            IReadOnlyList<SkinnedMeshRenderer> skinnedMeshes,
            Transform entityRoot)
        {
            Pelvis = pelvis;
            Bones = bones;
            OriginalPelvisParent = pelvis.BoneTransform.parent;
            OriginalPelvisSiblingIndex = pelvis.BoneTransform.GetSiblingIndex();
            ExternalColliders = externalColliders;
            SkinnedMeshes = skinnedMeshes;

            OriginalUpdateWhenOffscreen = new bool[skinnedMeshes.Count];
            for (int i = 0; i < skinnedMeshes.Count; i++)
                OriginalUpdateWhenOffscreen[i] = skinnedMeshes[i] != null && skinnedMeshes[i].updateWhenOffscreen;

            OriginalExternalColliderEnabled = new bool[externalColliders.Count];
            for (int i = 0; i < externalColliders.Count; i++)
                OriginalExternalColliderEnabled[i] = externalColliders[i] != null && externalColliders[i].enabled;

            PelvisHeightOffset = entityRoot != null
                ? Mathf.Clamp(pelvis.BoneTransform.position.y - entityRoot.position.y, 0f, 1.5f)
                : 0f;
        }

        public RagdollBoneHandle FindClosestBone(Vector3 worldPoint)
        {
            RagdollBoneHandle best = Pelvis;
            float bestDistSqr = float.MaxValue;

            for (int i = 0; i < Bones.Count; i++)
            {
                RagdollBoneHandle handle = Bones[i];
                if (handle.BoneTransform == null || handle.Body == null)
                    continue;

                float d = (handle.BoneTransform.position - worldPoint).sqrMagnitude;
                if (d < bestDistSqr)
                {
                    bestDistSqr = d;
                    best = handle;
                }
            }

            return best;
        }
    }
}
