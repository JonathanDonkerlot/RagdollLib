namespace RagdollLib.Core.Model
{
    /// <summary>
    /// Represents the lifecycle state of a ragdoll-capable entity.
    /// </summary>
    public enum RagdollState
    {
        /// <summary>Entity is controlled normally by Animator, IK, and AI/Walker controllers.</summary>
        Animated = 0,

        /// <summary>Entity is under active temporary ragdoll physics and will recover after its timer expires.</summary>
        ActiveRagdoll = 1,

        /// <summary>Entity is transitioning out of ragdoll back to grounded animated/AI state.</summary>
        Recovering = 2,

        /// <summary>Entity is in terminal death ragdoll state until despawn or pool release.</summary>
        DeadRagdoll = 3
    }
}
