using System.Linq;
using RagdollLib.Core.Math;
using RagdollLib.Core.Model;
using RagdollLib.Core.Rig;
using Xunit;

namespace RagdollLib.Tests
{
    public sealed class RagdollPhysicsProfileTests
    {
        [Fact]
        public void ComputeBalancedMasses_PreservesTotalAndClampsMassRatio()
        {
            var roles = new[]
            {
                BoneRole.Pelvis,
                BoneRole.Spine,
                BoneRole.Chest,
                BoneRole.Head,
                BoneRole.LeftUpperArm,
                BoneRole.LeftLowerArm,
                BoneRole.RightUpperArm,
                BoneRole.RightLowerArm
            };

            float[] masses = RagdollPhysicsProfile.ComputeBalancedMasses(roles, 50f);

            Assert.Equal(roles.Length, masses.Length);
            float sum = masses.Sum();
            Assert.InRange(sum, 49.5f, 50.5f);

            float min = masses.Min();
            float max = masses.Max();
            Assert.True(min > 0f);
            Assert.True(max / min <= RagdollPhysicsProfile.MaxMassRatio + 0.01f);
        }

        [Fact]
        public void ComputeCapsuleGeometry_SelectsDominantAxisAndClampsDimensions()
        {
            RagdollPhysicsProfile.ComputeCapsuleGeometry(
                BoneRole.LeftUpperArm,
                new Vec3(-0.32f, 0.02f, 0.01f),
                hasChildOffset: true,
                fallbackSegmentLength: 0.25f,
                out float radius,
                out float height,
                out Vec3 centerLocal,
                out int axis);

            Assert.Equal(0, axis); // X dominant
            Assert.InRange(radius, 0.03f, 0.35f);
            Assert.InRange(height, 0.08f, 1.20f);
            Assert.Equal(-0.16f, centerLocal.X, 3);
        }

        [Fact]
        public void GetJointLimits_ProducesNonZeroBoundedRanges()
        {
            JointAngularLimits elbow = RagdollPhysicsProfile.GetJointLimits(BoneRole.LeftLowerArm);
            Assert.True(elbow.LowTwist <= 0f);
            Assert.True(elbow.HighTwist > 0f);
            Assert.True(elbow.Swing1 > 0f);
            Assert.True(elbow.AngularDamper > 0f);
        }
    }
}
