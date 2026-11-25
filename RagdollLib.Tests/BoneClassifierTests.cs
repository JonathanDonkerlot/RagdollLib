using System.Collections.Generic;
using RagdollLib.Core.Math;
using RagdollLib.Core.Model;
using RagdollLib.Core.Rig;
using Xunit;

namespace RagdollLib.Tests
{
    public sealed class BoneClassifierTests
    {
        [Fact]
        public void Classify_SpriggullSkeleton_MapsMajorJointsAndExcludesMicroBones()
        {
            // Verified against MelonLoader/GameObjectDump.txt lines 155073-155254
            var nodes = new List<BoneNodeInput>
            {
                new BoneNodeInput(0, -1, "skin_root_jnt", new Vec3(0, 0.5f, 0), new Vec3(0, 0.5f, 0)),
                new BoneNodeInput(1, 0, "skin_back1_jnt", new Vec3(0, 0.12f, 0.05f), new Vec3(0, 0.62f, 0.05f)),
                new BoneNodeInput(2, 1, "skin_chest_jnt", new Vec3(0, 0.14f, 0.06f), new Vec3(0, 0.76f, 0.11f)),
                new BoneNodeInput(3, 2, "skin_neck_jnt", new Vec3(0, 0.12f, 0.05f), new Vec3(0, 0.88f, 0.16f)),
                new BoneNodeInput(4, 3, "skin_head_jnt", new Vec3(0, 0.14f, 0.04f), new Vec3(0, 1.02f, 0.20f)),
                new BoneNodeInput(5, 4, "skin_jaw_jnt", new Vec3(0, -0.04f, 0.08f), new Vec3(0, 0.98f, 0.28f), isExcludedComponent: true),
                new BoneNodeInput(6, 4, "skin_L_eye_jnt", new Vec3(-0.04f, 0.02f, 0.04f), new Vec3(-0.04f, 1.04f, 0.24f)),
                new BoneNodeInput(7, 4, "skin_feather1_jnt", new Vec3(0, 0.08f, -0.02f), new Vec3(0, 1.10f, 0.18f)),
                new BoneNodeInput(8, 1, "skin_L_clavicle_jnt", new Vec3(-0.08f, 0.06f, 0), new Vec3(-0.08f, 0.68f, 0.05f)),
                new BoneNodeInput(9, 8, "skin_L_shoulder_jnt", new Vec3(-0.12f, -0.02f, 0), new Vec3(-0.20f, 0.66f, 0.05f)),
                new BoneNodeInput(10, 9, "skin_L_elbow_jnt", new Vec3(-0.16f, -0.10f, 0), new Vec3(-0.36f, 0.56f, 0.05f)),
                new BoneNodeInput(11, 10, "skin_L_wrist_jnt", new Vec3(-0.10f, -0.08f, 0), new Vec3(-0.46f, 0.48f, 0.05f)),
                new BoneNodeInput(12, 0, "skin_L_thigh_jnt", new Vec3(-0.10f, -0.15f, 0), new Vec3(-0.10f, 0.35f, 0)),
                new BoneNodeInput(13, 12, "skin_L_knee_jnt", new Vec3(0, -0.18f, 0.02f), new Vec3(-0.10f, 0.17f, 0.02f)),
                new BoneNodeInput(14, 13, "skin_L_ankle_jnt", new Vec3(0, -0.12f, -0.02f), new Vec3(-0.10f, 0.05f, 0)),
                new BoneNodeInput(15, 14, "skin_L_toeA1_jnt", new Vec3(0, -0.02f, 0.04f), new Vec3(-0.10f, 0.03f, 0.04f)),
                new BoneNodeInput(16, 0, "skin_tail1_jnt", new Vec3(0, 0.05f, -0.14f), new Vec3(0, 0.55f, -0.14f))
            };

            RagdollRigBlueprint bp = RagdollRigAnalyzer.Analyze(nodes, 30f);

            Assert.NotNull(bp);
            Assert.False(bp.UsedTopologicalFallback);
            Assert.Equal(BoneRole.Pelvis, bp.Bones[bp.PelvisPlanIndex].Role);
            Assert.DoesNotContain(bp.Bones, b => b.Name.Contains("jaw"));
            Assert.DoesNotContain(bp.Bones, b => b.Name.Contains("eye"));
            Assert.DoesNotContain(bp.Bones, b => b.Name.Contains("feather"));
            Assert.DoesNotContain(bp.Bones, b => b.Name.Contains("toe"));
            Assert.DoesNotContain(bp.Bones, b => b.Name.Contains("clavicle"));
            Assert.Contains(bp.Bones, b => b.Role == BoneRole.Chest);
            Assert.Contains(bp.Bones, b => b.Role == BoneRole.Head);
            Assert.Contains(bp.Bones, b => b.Role == BoneRole.LeftUpperArm);
            Assert.Contains(bp.Bones, b => b.Role == BoneRole.LeftLowerArm);
            Assert.Contains(bp.Bones, b => b.Role == BoneRole.LeftUpperLeg);
            Assert.Contains(bp.Bones, b => b.Role == BoneRole.LeftLowerLeg);

            // Parent ordering invariant
            for (int i = 0; i < bp.Bones.Count; i++)
            {
                if (i == 0) Assert.Equal(-1, bp.Bones[i].ParentPlanIndex);
                else Assert.True(bp.Bones[i].ParentPlanIndex >= 0 && bp.Bones[i].ParentPlanIndex < i);
            }
        }

        [Fact]
        public void Analyze_ExoticSegmentedCreature_UsesTopologicalFallback()
        {
            var nodes = new List<BoneNodeInput>
            {
                new BoneNodeInput(0, -1, "wyrm_segment_A", Vec3.Zero, new Vec3(0, 0.5f, 0)),
                new BoneNodeInput(1, 0, "wyrm_segment_B", new Vec3(0, 0, 0.3f), new Vec3(0, 0.5f, 0.3f)),
                new BoneNodeInput(2, 1, "wyrm_segment_C", new Vec3(0, 0, 0.3f), new Vec3(0, 0.5f, 0.6f)),
                new BoneNodeInput(3, 2, "wyrm_segment_D", new Vec3(0, 0, 0.3f), new Vec3(0, 0.5f, 0.9f)),
                new BoneNodeInput(4, 3, "wyrm_segment_micro", new Vec3(0, 0, 0.01f), new Vec3(0, 0.5f, 0.91f))
            };

            RagdollRigBlueprint bp = RagdollRigAnalyzer.Analyze(nodes, 40f);

            Assert.NotNull(bp);
            Assert.True(bp.UsedTopologicalFallback);
            Assert.Equal(4, bp.Bones.Count);
            Assert.Equal(BoneRole.Pelvis, bp.Bones[0].Role);
            Assert.All(bp.Bones, b => Assert.DoesNotContain("micro", b.Name));
        }

        [Fact]
        public void Analyze_EmptyOrSingleNode_ReturnsNull()
        {
            Assert.Null(RagdollRigAnalyzer.Analyze(null));
            Assert.Null(RagdollRigAnalyzer.Analyze(new[] { new BoneNodeInput(0, -1, "root_jnt", Vec3.Zero, Vec3.Zero) }));
        }
    }
}
