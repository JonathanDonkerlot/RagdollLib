using RagdollLib.Core.Model;
using UnityEngine;

namespace RagdollLib.Rig
{
    internal sealed class RagdollBoneHandle
    {
        public Transform BoneTransform { get; }
        public BoneRole Role { get; }
        public Rigidbody Body { get; }
        public CapsuleCollider Collider { get; }
        public ConfigurableJoint Joint { get; }
        public Vector3 OriginalLocalPosition { get; }
        public Quaternion OriginalLocalRotation { get; }
        public bool CreatedRigidbody { get; }
        public bool CreatedCollider { get; }
        public bool CreatedJoint { get; }

        public RagdollBoneHandle(
            Transform boneTransform,
            BoneRole role,
            Rigidbody body,
            CapsuleCollider collider,
            ConfigurableJoint joint,
            bool createdRigidbody,
            bool createdCollider,
            bool createdJoint)
        {
            BoneTransform = boneTransform;
            Role = role;
            Body = body;
            Collider = collider;
            Joint = joint;
            OriginalLocalPosition = boneTransform.localPosition;
            OriginalLocalRotation = boneTransform.localRotation;
            CreatedRigidbody = createdRigidbody;
            CreatedCollider = createdCollider;
            CreatedJoint = createdJoint;
        }
    }
}
