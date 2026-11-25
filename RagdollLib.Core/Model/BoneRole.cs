namespace RagdollLib.Core.Model
{
    /// <summary>
    /// Semantic role assigned to a skeletal bone during procedural ragdoll generation.
    /// </summary>
    public enum BoneRole
    {
        /// <summary>Unclassified or ignored bone.</summary>
        Unknown = 0,

        /// <summary>Root/pelvis/hips anchor bone.</summary>
        Pelvis = 1,

        /// <summary>Lower or mid spine bone.</summary>
        Spine = 2,

        /// <summary>Upper torso or chest bone.</summary>
        Chest = 3,

        /// <summary>Neck bone.</summary>
        Neck = 4,

        /// <summary>Head or cranium bone.</summary>
        Head = 5,

        /// <summary>Left upper arm (humerus/shoulder).</summary>
        LeftUpperArm = 6,

        /// <summary>Left lower arm (forearm/elbow).</summary>
        LeftLowerArm = 7,

        /// <summary>Right upper arm (humerus/shoulder).</summary>
        RightUpperArm = 8,

        /// <summary>Right lower arm (forearm/elbow).</summary>
        RightLowerArm = 9,

        /// <summary>Left upper leg (thigh/hip).</summary>
        LeftUpperLeg = 10,

        /// <summary>Left lower leg (calf/knee).</summary>
        LeftLowerLeg = 11,

        /// <summary>Right upper leg (thigh/hip).</summary>
        RightUpperLeg = 12,

        /// <summary>Right lower leg (calf/knee).</summary>
        RightLowerLeg = 13,

        /// <summary>Tail segment bone.</summary>
        Tail = 14,

        /// <summary>Generic structural segment discovered by topological fallback on non-humanoid rigs.</summary>
        GenericSegment = 15
    }
}
