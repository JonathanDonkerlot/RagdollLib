using System;
using System.Collections.Generic;
using RagdollLib.Core.Math;
using RagdollLib.Core.Model;

namespace RagdollLib.Core.Rig
{
    /// <summary>
    /// Pure C# skeletal analyzer that converts a tree of <see cref="BoneNodeInput"/> transforms
    /// into a stable, parent-ordered <see cref="RagdollRigBlueprint"/>.
    /// </summary>
    public static class RagdollRigAnalyzer
    {
        /// <summary>Maximum number of dynamic Rigidbodies generated for a single ragdoll.</summary>
        public const int MaxRagdollBones = 16;

        /// <summary>Minimum world-space distance between topological fallback bones.</summary>
        public const float MinFallbackBoneSpacing = 0.06f;

        /// <summary>
        /// Analyzes <paramref name="nodes"/> and builds a <see cref="RagdollRigBlueprint"/>,
        /// or returns <c>null</c> if the hierarchy lacks at least 2 usable rigid body segments.
        /// </summary>
        public static RagdollRigBlueprint Analyze(
            IReadOnlyList<BoneNodeInput> nodes,
            float entityTotalMass = RagdollPhysicsProfile.DefaultEntityMass,
            float fallbackSegmentLength = 0.22f)
        {
            if (nodes == null || nodes.Count < 2)
                return null;

            // Build children adjacency
            var children = new List<int>[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
                children[i] = new List<int>();

            var rootIndices = new List<int>();
            for (int i = 0; i < nodes.Count; i++)
            {
                int p = nodes[i].ParentIndex;
                if (p >= 0 && p < nodes.Count && p != i)
                    children[p].Add(i);
                else
                    rootIndices.Add(i);
            }

            // Pass 1: Canonical classification
            var selectedIndices = new List<int>();
            var selectedRoles = new Dictionary<int, BoneRole>();
            bool usedFallback = false;

            for (int i = 0; i < nodes.Count; i++)
            {
                BoneNodeInput n = nodes[i];
                if (IsInsideExcludedSubtree(i, nodes))
                    continue;

                BoneRole role = BoneClassifier.Classify(n.Name, n.IsExcludedComponent);
                if (role != BoneRole.Unknown)
                {
                    // Avoid duplicate roles for identical canonical slots (except Spine/Tail which can have multiple segments)
                    if (role != BoneRole.Spine && role != BoneRole.Tail && ContainsRole(selectedRoles, role))
                        continue;

                    selectedIndices.Add(i);
                    selectedRoles[i] = role;
                    if (selectedIndices.Count >= MaxRagdollBones)
                        break;
                }
            }

            // Pass 2: Topological fallback if canonical matching found fewer than 3 bones
            if (selectedIndices.Count < 3)
            {
                selectedIndices.Clear();
                selectedRoles.Clear();
                usedFallback = true;

                int rootIdx = FindBestSkeletonRoot(nodes, children, rootIndices);
                if (rootIdx < 0)
                    return null;

                TraverseTopologicalFallback(rootIdx, -1, nodes, children, selectedIndices, selectedRoles);
            }

            if (selectedIndices.Count < 2)
                return null;

            // Ensure a single Pelvis root exists and precedes all descendants
            int pelvisNodeIdx = FindPelvisNodeIndex(selectedIndices, selectedRoles, nodes);
            if (pelvisNodeIdx < 0)
                return null;

            selectedRoles[pelvisNodeIdx] = BoneRole.Pelvis;

            // Order selected bones topologically from root downward so ParentPlanIndex < ChildPlanIndex
            var orderedNodes = OrderTopologically(selectedIndices, pelvisNodeIdx, nodes);
            if (orderedNodes.Count < 2)
                return null;

            var nodeToPlanIdx = new Dictionary<int, int>(orderedNodes.Count);
            var rolesArray = new BoneRole[orderedNodes.Count];
            for (int i = 0; i < orderedNodes.Count; i++)
            {
                int nodeIdx = orderedNodes[i];
                nodeToPlanIdx[nodeIdx] = i;
                rolesArray[i] = selectedRoles[nodeIdx];
            }

            float[] masses = RagdollPhysicsProfile.ComputeBalancedMasses(rolesArray, entityTotalMass);

            // Map each planned bone's parent plan index (nearest selected ancestor, falling back to pelvis=0)
            var parentPlanIndices = new int[orderedNodes.Count];
            for (int i = 0; i < orderedNodes.Count; i++)
            {
                if (i == 0)
                {
                    parentPlanIndices[i] = -1;
                    continue;
                }

                int parentPlan = 0;
                int cursor = nodes[orderedNodes[i]].ParentIndex;
                while (cursor >= 0 && cursor < nodes.Count)
                {
                    if (nodeToPlanIdx.TryGetValue(cursor, out int foundPlanIdx))
                    {
                        parentPlan = foundPlanIdx;
                        break;
                    }
                    cursor = nodes[cursor].ParentIndex;
                }
                parentPlanIndices[i] = parentPlan;
            }

            // Find a primary child direction for each planned bone to size its capsule
            var primaryChildPlan = new int[orderedNodes.Count];
            for (int i = 0; i < primaryChildPlan.Length; i++)
                primaryChildPlan[i] = -1;

            for (int i = 1; i < orderedNodes.Count; i++)
            {
                int p = parentPlanIndices[i];
                if (p >= 0 && primaryChildPlan[p] < 0)
                    primaryChildPlan[p] = i;
            }

            var plans = new List<RagdollBonePlan>(orderedNodes.Count);
            float totalMass = 0f;

            for (int i = 0; i < orderedNodes.Count; i++)
            {
                int nodeIdx = orderedNodes[i];
                BoneNodeInput node = nodes[nodeIdx];
                BoneRole role = rolesArray[i];
                float mass = masses[i];
                totalMass += mass;

                Vec3 childOffset = Vec3.Zero;
                bool hasChild = false;

                int childPlan = primaryChildPlan[i];
                if (childPlan >= 0)
                {
                    BoneNodeInput childNode = nodes[orderedNodes[childPlan]];
                    childOffset = childNode.WorldPosition - node.WorldPosition;
                    hasChild = childOffset.SqrMagnitude > 1e-5f;
                }
                else if (children[nodeIdx].Count > 0)
                {
                    BoneNodeInput rawChild = nodes[children[nodeIdx][0]];
                    childOffset = rawChild.WorldPosition - node.WorldPosition;
                    hasChild = childOffset.SqrMagnitude > 1e-5f;
                }

                // Estimate local offset magnitude while preserving primary local axis when child is a direct child
                Vec3 localChildOffset = childOffset;
                if (childPlan >= 0 && nodes[orderedNodes[childPlan]].ParentIndex == nodeIdx)
                {
                    localChildOffset = nodes[orderedNodes[childPlan]].LocalPosition;
                    hasChild = localChildOffset.SqrMagnitude > 1e-5f;
                }
                else if (children[nodeIdx].Count > 0)
                {
                    localChildOffset = nodes[children[nodeIdx][0]].LocalPosition;
                    hasChild = localChildOffset.SqrMagnitude > 1e-5f;
                }

                RagdollPhysicsProfile.ComputeCapsuleGeometry(
                    role,
                    localChildOffset,
                    hasChild,
                    fallbackSegmentLength,
                    out float radius,
                    out float height,
                    out Vec3 centerLocal,
                    out int directionAxis);

                JointAngularLimits limits = i == 0
                    ? default
                    : RagdollPhysicsProfile.GetJointLimits(role);

                plans.Add(new RagdollBonePlan(
                    nodeIdx,
                    parentPlanIndices[i],
                    node.Name,
                    role,
                    mass,
                    radius,
                    height,
                    centerLocal,
                    directionAxis,
                    limits));
            }

            return new RagdollRigBlueprint(0, plans, totalMass, usedFallback);
        }

        private static bool IsInsideExcludedSubtree(int nodeIdx, IReadOnlyList<BoneNodeInput> nodes)
        {
            int cursor = nodeIdx;
            int safety = 0;
            while (cursor >= 0 && cursor < nodes.Count && safety++ < 128)
            {
                if (nodes[cursor].IsExcludedComponent)
                    return true;
                cursor = nodes[cursor].ParentIndex;
            }
            return false;
        }

        private static bool ContainsRole(Dictionary<int, BoneRole> map, BoneRole role)
        {
            foreach (var kv in map)
            {
                if (kv.Value == role)
                    return true;
            }
            return false;
        }

        private static int FindBestSkeletonRoot(
            IReadOnlyList<BoneNodeInput> nodes,
            List<int>[] children,
            List<int> rootIndices)
        {
            int bestIdx = -1;
            int bestSubtree = -1;

            for (int i = 0; i < rootIndices.Count; i++)
            {
                int r = rootIndices[i];
                if (nodes[r].IsExcludedComponent)
                    continue;

                int count = CountReachable(r, nodes, children);
                if (count > bestSubtree)
                {
                    bestSubtree = count;
                    bestIdx = r;
                }
            }

            return bestIdx;
        }

        private static int CountReachable(int root, IReadOnlyList<BoneNodeInput> nodes, List<int>[] children)
        {
            int count = 0;
            var stack = new Stack<int>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                int cur = stack.Pop();
                if (nodes[cur].IsExcludedComponent)
                    continue;
                count++;
                var ch = children[cur];
                for (int i = 0; i < ch.Count; i++)
                    stack.Push(ch[i]);
            }
            return count;
        }

        private static void TraverseTopologicalFallback(
            int nodeIdx,
            int lastSelectedIdx,
            IReadOnlyList<BoneNodeInput> nodes,
            List<int>[] children,
            List<int> selectedIndices,
            Dictionary<int, BoneRole> selectedRoles)
        {
            if (selectedIndices.Count >= MaxRagdollBones)
                return;

            BoneNodeInput node = nodes[nodeIdx];
            if (node.IsExcludedComponent)
                return;

            bool isRoot = lastSelectedIdx < 0;
            bool skipName = !isRoot && BoneClassifier.ShouldExclude(node.Name, node.IsExcludedComponent);

            int nextParentSelected = lastSelectedIdx;
            if (!skipName)
            {
                float dist = isRoot
                    ? float.MaxValue
                    : Vec3.Distance(node.WorldPosition, nodes[lastSelectedIdx].WorldPosition);

                if (isRoot || dist >= MinFallbackBoneSpacing)
                {
                    selectedIndices.Add(nodeIdx);
                    selectedRoles[nodeIdx] = isRoot ? BoneRole.Pelvis : BoneRole.GenericSegment;
                    nextParentSelected = nodeIdx;
                }
            }

            var ch = children[nodeIdx];
            for (int i = 0; i < ch.Count; i++)
            {
                if (selectedIndices.Count >= MaxRagdollBones)
                    break;
                TraverseTopologicalFallback(ch[i], nextParentSelected, nodes, children, selectedIndices, selectedRoles);
            }
        }

        private static int FindPelvisNodeIndex(
            List<int> selectedIndices,
            Dictionary<int, BoneRole> selectedRoles,
            IReadOnlyList<BoneNodeInput> nodes)
        {
            for (int i = 0; i < selectedIndices.Count; i++)
            {
                int idx = selectedIndices[i];
                if (selectedRoles[idx] == BoneRole.Pelvis)
                    return idx;
            }

            // Fallback to shallowest selected node
            int bestIdx = -1;
            int bestDepth = int.MaxValue;
            for (int i = 0; i < selectedIndices.Count; i++)
            {
                int idx = selectedIndices[i];
                int depth = GetDepth(idx, nodes);
                if (depth < bestDepth)
                {
                    bestDepth = depth;
                    bestIdx = idx;
                }
            }
            return bestIdx;
        }

        private static int GetDepth(int idx, IReadOnlyList<BoneNodeInput> nodes)
        {
            int depth = 0;
            int cursor = nodes[idx].ParentIndex;
            while (cursor >= 0 && cursor < nodes.Count && depth < 128)
            {
                depth++;
                cursor = nodes[cursor].ParentIndex;
            }
            return depth;
        }

        private static List<int> OrderTopologically(
            List<int> selectedIndices,
            int pelvisNodeIdx,
            IReadOnlyList<BoneNodeInput> nodes)
        {
            var ordered = new List<int>(selectedIndices.Count) { pelvisNodeIdx };
            var remaining = new List<int>(selectedIndices.Count);

            for (int i = 0; i < selectedIndices.Count; i++)
            {
                if (selectedIndices[i] != pelvisNodeIdx)
                    remaining.Add(selectedIndices[i]);
            }

            // Sort remaining by hierarchy depth ascending so parents are guaranteed to appear before children
            remaining.Sort((a, b) =>
            {
                int da = GetDepth(a, nodes);
                int db = GetDepth(b, nodes);
                int cmp = da.CompareTo(db);
                return cmp != 0 ? cmp : a.CompareTo(b);
            });

            ordered.AddRange(remaining);
            return ordered;
        }
    }
}
