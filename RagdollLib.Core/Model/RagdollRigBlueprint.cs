using System;
using System.Collections.Generic;
using RagdollLib.Core.Math;

namespace RagdollLib.Core.Model
{
    /// <summary>
    /// Symmetric angular limits and joint damping parameters for a <c>ConfigurableJoint</c>.
    /// </summary>
    public readonly struct JointAngularLimits
    {
        /// <summary>Lower twist limit in degrees (negative or zero).</summary>
        public float LowTwist { get; }

        /// <summary>Upper twist limit in degrees (positive or zero).</summary>
        public float HighTwist { get; }

        /// <summary>Primary swing limit in degrees (non-negative).</summary>
        public float Swing1 { get; }

        /// <summary>Secondary swing limit in degrees (non-negative).</summary>
        public float Swing2 { get; }

        /// <summary>Angular drive position spring (kept low for limp ragdoll motion).</summary>
        public float AngularSpring { get; }

        /// <summary>Angular drive damper preventing high-frequency joint jitter.</summary>
        public float AngularDamper { get; }

        /// <summary>Initializes a new <see cref="JointAngularLimits"/>.</summary>
        public JointAngularLimits(
            float lowTwist,
            float highTwist,
            float swing1,
            float swing2,
            float angularSpring,
            float angularDamper)
        {
            LowTwist = lowTwist;
            HighTwist = highTwist;
            Swing1 = swing1;
            Swing2 = swing2;
            AngularSpring = angularSpring;
            AngularDamper = angularDamper;
        }
    }

    /// <summary>
    /// Planned physics configuration for a single bone in a procedural ragdoll rig.
    /// </summary>
    public sealed class RagdollBonePlan
    {
        /// <summary>Index into the source <see cref="BoneNodeInput"/> list.</summary>
        public int NodeIndex { get; }

        /// <summary>Index of the parent plan entry in <see cref="RagdollRigBlueprint.Bones"/>, or -1 for the pelvis root.</summary>
        public int ParentPlanIndex { get; }

        /// <summary>Bone transform name.</summary>
        public string Name { get; }

        /// <summary>Assigned semantic <see cref="BoneRole"/>.</summary>
        public BoneRole Role { get; }

        /// <summary>Mass in kilograms assigned to this bone's <c>Rigidbody</c>.</summary>
        public float Mass { get; }

        /// <summary>Capsule collider radius in meters.</summary>
        public float CapsuleRadius { get; }

        /// <summary>Capsule collider total height in meters.</summary>
        public float CapsuleHeight { get; }

        /// <summary>Local-space center offset for the <c>CapsuleCollider</c>.</summary>
        public Vec3 CapsuleCenterLocal { get; }

        /// <summary>Unity CapsuleCollider direction axis (0 = X, 1 = Y, 2 = Z).</summary>
        public int CapsuleDirectionAxis { get; }

        /// <summary>Joint angular constraints connecting this bone to its parent plan bone.</summary>
        public JointAngularLimits Limits { get; }

        /// <summary>Initializes a new <see cref="RagdollBonePlan"/>.</summary>
        public RagdollBonePlan(
            int nodeIndex,
            int parentPlanIndex,
            string name,
            BoneRole role,
            float mass,
            float capsuleRadius,
            float capsuleHeight,
            Vec3 capsuleCenterLocal,
            int capsuleDirectionAxis,
            JointAngularLimits limits)
        {
            NodeIndex = nodeIndex;
            ParentPlanIndex = parentPlanIndex;
            Name = name ?? string.Empty;
            Role = role;
            Mass = mass;
            CapsuleRadius = capsuleRadius;
            CapsuleHeight = capsuleHeight;
            CapsuleCenterLocal = capsuleCenterLocal;
            CapsuleDirectionAxis = capsuleDirectionAxis;
            Limits = limits;
        }
    }

    /// <summary>
    /// Complete reusable rig blueprint produced for a creature or character archetype.
    /// </summary>
    public sealed class RagdollRigBlueprint
    {
        /// <summary>Index of the pelvis/root bone in <see cref="Bones"/>.</summary>
        public int PelvisPlanIndex { get; }

        /// <summary>Ordered list of planned physics bones (parents always precede children).</summary>
        public IReadOnlyList<RagdollBonePlan> Bones { get; }

        /// <summary>Total mass distributed across all planned bones.</summary>
        public float TotalMass { get; }

        /// <summary>True when topology fallback was used rather than canonical named limbs.</summary>
        public bool UsedTopologicalFallback { get; }

        /// <summary>Initializes a new <see cref="RagdollRigBlueprint"/>.</summary>
        public RagdollRigBlueprint(
            int pelvisPlanIndex,
            IReadOnlyList<RagdollBonePlan> bones,
            float totalMass,
            bool usedTopologicalFallback)
        {
            PelvisPlanIndex = pelvisPlanIndex;
            Bones = bones ?? Array.Empty<RagdollBonePlan>();
            TotalMass = totalMass;
            UsedTopologicalFallback = usedTopologicalFallback;
        }
    }
}
