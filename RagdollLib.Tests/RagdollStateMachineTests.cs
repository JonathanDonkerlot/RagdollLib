using RagdollLib.Core.Model;
using RagdollLib.Core.State;
using Xunit;

namespace RagdollLib.Tests
{
    public sealed class RagdollStateMachineTests
    {
        [Fact]
        public void Lifecycle_ActiveRagdoll_TicksToRecoveryAndResetsCleanly()
        {
            var sm = new RagdollStateMachine();
            Assert.Equal(RagdollState.Animated, sm.State);
            Assert.False(sm.IsRagdolled);

            bool newlyEntered = sm.EnterRagdoll(2.0f, isDeath: false);
            Assert.True(newlyEntered);
            Assert.Equal(RagdollState.ActiveRagdoll, sm.State);
            Assert.True(sm.IsRagdolled);

            Assert.False(sm.Tick(1.25f));
            Assert.InRange(sm.RemainingDuration, 0.74f, 0.76f);

            // Re-trigger extends remaining duration
            Assert.False(sm.EnterRagdoll(1.5f, isDeath: false));
            Assert.InRange(sm.RemainingDuration, 1.49f, 1.51f);

            Assert.True(sm.Tick(1.6f));
            Assert.True(sm.BeginRecovery());
            Assert.Equal(RagdollState.Recovering, sm.State);

            sm.CompleteRecovery();
            Assert.Equal(RagdollState.Animated, sm.State);
            Assert.False(sm.IsRagdolled);
        }

        [Fact]
        public void DeathRagdoll_DoesNotAutoRecoverUntilForceReset()
        {
            var sm = new RagdollStateMachine();
            Assert.True(sm.EnterRagdoll(0f, isDeath: true));
            Assert.Equal(RagdollState.DeadRagdoll, sm.State);
            Assert.True(sm.IsDeathRagdoll);

            // Timed hits must not downgrade DeadRagdoll
            Assert.False(sm.EnterRagdoll(5.0f, isDeath: false));
            Assert.Equal(RagdollState.DeadRagdoll, sm.State);

            Assert.False(sm.Tick(100f));
            sm.ForceReset();
            Assert.Equal(RagdollState.Animated, sm.State);
            Assert.False(sm.IsDeathRagdoll);
        }
    }
}
