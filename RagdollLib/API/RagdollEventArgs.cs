using System;
using RagdollLib.Core.Model;
using UnityEngine;

namespace RagdollLib.API
{
    /// <summary>
    /// Event payload emitted when an entity enters or exits ragdoll physics.
    /// </summary>
    public sealed class RagdollEventArgs : EventArgs
    {
        /// <summary>Resolved root <see cref="GameObject"/> of the ragdolled entity.</summary>
        public GameObject TargetRoot { get; }

        /// <summary>The <see cref="Component"/> passed into <see cref="RagdollAPI"/> or triggering the event.</summary>
        public Component TargetComponent { get; }

        /// <summary>World-space impulse vector applied when entering ragdoll (zero on recovery).</summary>
        public Vector3 ImpulseForce { get; }

        /// <summary>World-space hit point where directional impulse was applied.</summary>
        public Vector3 WorldHitPoint { get; }

        /// <summary>Requested duration in seconds (zero for death ragdolls or recovery events).</summary>
        public float DurationSeconds { get; }

        /// <summary>True if the ragdoll was triggered as a terminal death ragdoll.</summary>
        public bool IsDeathRagdoll { get; }

        /// <summary>Current <see cref="RagdollState"/> at the moment the event fired.</summary>
        public RagdollState State { get; }

        /// <summary>Initializes a new <see cref="RagdollEventArgs"/>.</summary>
        public RagdollEventArgs(
            GameObject targetRoot,
            Component targetComponent,
            Vector3 impulseForce,
            Vector3 worldHitPoint,
            float durationSeconds,
            bool isDeathRagdoll,
            RagdollState state)
        {
            TargetRoot = targetRoot;
            TargetComponent = targetComponent;
            ImpulseForce = impulseForce;
            WorldHitPoint = worldHitPoint;
            DurationSeconds = durationSeconds;
            IsDeathRagdoll = isDeathRagdoll;
            State = state;
        }
    }
}
