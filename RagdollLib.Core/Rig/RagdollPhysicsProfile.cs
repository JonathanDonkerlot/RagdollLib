using System;
using RagdollLib.Core.Math;
using RagdollLib.Core.Model;

namespace RagdollLib.Core.Rig
{
    /// <summary>
    /// Computes stable PhysX bone masses, capsule collider geometries, and non-exploding angular joint limits.
    /// </summary>
    public static class RagdollPhysicsProfile
    {
        /// <summary>Minimum allowed entity total mass in kilograms.</summary>
        public const float MinEntityMass = 5f;

        /// <summary>Maximum allowed entity total mass in kilograms.</summary>
        public const float MaxEntityMass = 150f;

        /// <summary>Default fallback mass when no root Rigidbody mass is present.</summary>
        public const float DefaultEntityMass = 25f;

        /// <summary>Maximum permitted ratio between heaviest and lightest bone Rigidbodies to keep PhysX stable.</summary>
        public const float MaxMassRatio = 8f;

        /// <summary>Returns the relative weight of a <see cref="BoneRole"/> within a rig.</summary>
        public static float GetRoleMassWeight(BoneRole role)
        {
            switch (role)
            {
                case BoneRole.Pelvis: return 0.22f;
                case BoneRole.Spine: return 0.14f;
                case BoneRole.Chest: return 0.18f;
                case BoneRole.Neck: return 0.05f;
                case BoneRole.Head: return 0.09f;
                case BoneRole.LeftUpperArm:
                case BoneRole.RightUpperArm: return 0.06f;
                case BoneRole.LeftLowerArm:
                case BoneRole.RightLowerArm: return 0.045f;
                case BoneRole.LeftUpperLeg:
                case BoneRole.RightUpperLeg: return 0.10f;
                case BoneRole.LeftLowerLeg:
                case BoneRole.RightLowerLeg: return 0.065f;
                case BoneRole.Tail: return 0.045f;
                case BoneRole.GenericSegment: return 0.08f;
                default: return 0.05f;
            }
        }

        /// <summary>
        /// Normalizes per-bone masses to sum to <paramref name="entityTotalMass"/> (clamped to safe bounds)
        /// while clamping the heaviest-to-lightest mass ratio to <see cref="MaxMassRatio"/>.
        /// </summary>
        public static float[] ComputeBalancedMasses(BoneRole[] roles, float entityTotalMass)
        {
            if (roles == null || roles.Length == 0)
                return Array.Empty<float>();

            float targetTotal = Clamp(
                float.IsNaN(entityTotalMass) || float.IsInfinity(entityTotalMass) || entityTotalMass <= 0f
                    ? DefaultEntityMass
                    : entityTotalMass,
                MinEntityMass,
                MaxEntityMass);

            var weights = new float[roles.Length];
            float sum = 0f;
            float maxWeight = 0f;

            for (int i = 0; i < roles.Length; i++)
            {
                float w = GetRoleMassWeight(roles[i]);
                weights[i] = w;
                if (w > maxWeight)
                    maxWeight = w;
            }

            float minAllowedWeight = maxWeight / MaxMassRatio;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] < minAllowedWeight)
                    weights[i] = minAllowedWeight;
                sum += weights[i];
            }

            var masses = new float[roles.Length];
            float scale = sum > 1e-5f ? targetTotal / sum : targetTotal / roles.Length;
            for (int i = 0; i < weights.Length; i++)
            {
                masses[i] = MathFMax(0.5f, weights[i] * scale);
            }

            return masses;
        }

        /// <summary>
        /// Returns stable angular limits and joint damping for the specified <see cref="BoneRole"/>.
        /// </summary>
        public static JointAngularLimits GetJointLimits(BoneRole role)
        {
            switch (role)
            {
                case BoneRole.Spine:
                    return new JointAngularLimits(-25f, 25f, 25f, 20f, 0f, 1.5f);
                case BoneRole.Chest:
                    return new JointAngularLimits(-25f, 30f, 25f, 20f, 0f, 1.5f);
                case BoneRole.Neck:
                    return new JointAngularLimits(-25f, 25f, 25f, 25f, 0f, 1.2f);
                case BoneRole.Head:
                    return new JointAngularLimits(-35f, 35f, 35f, 30f, 0f, 1.0f);
                case BoneRole.LeftUpperArm:
                case BoneRole.RightUpperArm:
                    return new JointAngularLimits(-60f, 75f, 75f, 45f, 0f, 0.8f);
                case BoneRole.LeftLowerArm:
                case BoneRole.RightLowerArm:
                    return new JointAngularLimits(-10f, 110f, 15f, 10f, 0f, 0.6f);
                case BoneRole.LeftUpperLeg:
                case BoneRole.RightUpperLeg:
                    return new JointAngularLimits(-70f, 45f, 45f, 30f, 0f, 1.0f);
                case BoneRole.LeftLowerLeg:
                case BoneRole.RightLowerLeg:
                    return new JointAngularLimits(-110f, 10f, 12f, 10f, 0f, 0.8f);
                case BoneRole.Tail:
                    return new JointAngularLimits(-35f, 35f, 35f, 35f, 0f, 0.5f);
                default:
                    return new JointAngularLimits(-35f, 35f, 35f, 35f, 0f, 1.0f);
            }
        }

        /// <summary>
        /// Estimates local-space capsule collider geometry from the offset toward a primary child bone.
        /// </summary>
        public static void ComputeCapsuleGeometry(
            BoneRole role,
            Vec3 localOffsetToChild,
            bool hasChildOffset,
            float fallbackSegmentLength,
            out float radius,
            out float height,
            out Vec3 centerLocal,
            out int directionAxis)
        {
            float baseRadius = GetDefaultRadius(role);
            float length = hasChildOffset ? localOffsetToChild.Magnitude : fallbackSegmentLength;
            length = Clamp(length, 0.08f, 1.20f);

            radius = Clamp(System.Math.Min(baseRadius, length * 0.45f), 0.03f, 0.35f);
            height = Clamp(System.Math.Max(length, radius * 2.1f), 0.08f, 1.20f);

            if (!hasChildOffset || localOffsetToChild.SqrMagnitude < 1e-6f)
            {
                directionAxis = 1; // Y
                centerLocal = Vec3.Zero;
                return;
            }

            float ax = System.Math.Abs(localOffsetToChild.X);
            float ay = System.Math.Abs(localOffsetToChild.Y);
            float az = System.Math.Abs(localOffsetToChild.Z);

            if (ax >= ay && ax >= az)
                directionAxis = 0;
            else if (ay >= ax && ay >= az)
                directionAxis = 1;
            else
                directionAxis = 2;

            centerLocal = localOffsetToChild * 0.5f;
        }

        private static float GetDefaultRadius(BoneRole role)
        {
            switch (role)
            {
                case BoneRole.Pelvis: return 0.14f;
                case BoneRole.Chest: return 0.15f;
                case BoneRole.Spine: return 0.13f;
                case BoneRole.Head: return 0.11f;
                case BoneRole.Neck: return 0.07f;
                case BoneRole.LeftUpperArm:
                case BoneRole.RightUpperArm: return 0.06f;
                case BoneRole.LeftLowerArm:
                case BoneRole.RightLowerArm: return 0.05f;
                case BoneRole.LeftUpperLeg:
                case BoneRole.RightUpperLeg: return 0.08f;
                case BoneRole.LeftLowerLeg:
                case BoneRole.RightLowerLeg: return 0.06f;
                case BoneRole.Tail: return 0.045f;
                default: return 0.07f;
            }
        }

        private static float Clamp(float val, float min, float max) => val < min ? min : (val > max ? max : val);

        private static float MathFMax(float a, float b) => a > b ? a : b;
    }
}
