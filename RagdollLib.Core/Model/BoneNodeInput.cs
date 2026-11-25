using RagdollLib.Core.Math;

namespace RagdollLib.Core.Model
{
    /// <summary>
    /// Engine-independent snapshot of a transform node inside an entity's bone hierarchy.
    /// </summary>
    public sealed class BoneNodeInput
    {
        /// <summary>Zero-based index of this node in the input array.</summary>
        public int Index { get; }

        /// <summary>Index of the parent node, or -1 if this is a skeleton root.</summary>
        public int ParentIndex { get; }

        /// <summary>Raw GameObject/Transform name.</summary>
        public string Name { get; }

        /// <summary>Local position relative to its parent transform.</summary>
        public Vec3 LocalPosition { get; }

        /// <summary>World-space position at extraction time.</summary>
        public Vec3 WorldPosition { get; }

        /// <summary>
        /// True when the transform hosts an embedded interactive/networked component
        /// (such as <c>NetworkEntity</c>, <c>Interactor</c>, <c>FixedImpactor</c>, or <c>MoveToBone</c>)
        /// and therefore must never be turned into a ragdoll body.
        /// </summary>
        public bool IsExcludedComponent { get; }

        /// <summary>
        /// Initializes a new <see cref="BoneNodeInput"/>.
        /// </summary>
        public BoneNodeInput(
            int index,
            int parentIndex,
            string name,
            Vec3 localPosition,
            Vec3 worldPosition,
            bool isExcludedComponent = false)
        {
            Index = index;
            ParentIndex = parentIndex;
            Name = name ?? string.Empty;
            LocalPosition = localPosition;
            WorldPosition = worldPosition;
            IsExcludedComponent = isExcludedComponent;
        }
    }
}
