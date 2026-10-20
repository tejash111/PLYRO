using Bowling.Contracts;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Bowling.Gameplay.Tests
{
    /// <summary>
    /// Acceptance criterion 2: <c>GameplayService.Tick()</c> allocates nothing in the steady states (AwaitThrow and
    /// Rolling). Each test ticks once as a warm-up, then measures 100 ticks at 64 Hz.
    /// </summary>
    public sealed class TickAllocationTests
    {
        private GameplayRig _rig;

        [SetUp]
        public void SetUp()
        {
            _rig = new GameplayRig();
        }

        [TearDown]
        public void TearDown()
        {
            _rig?.Dispose();
        }

        [Test]
        public void TickInAwaitThrowAllocatesNothing()
        {
            _rig.PlayToFirstBall();
            _rig.Throws.SetPhase(SwingPhase.Ready);
            Assert.AreEqual(GameState.AwaitThrow, _rig.Service.Current.State);
            _rig.Service.Tick();    // warm-up

            Assert.That(Ticks100TimesAt64Hz, Is.Not.AllocatingGCMemory());

            Assert.AreEqual(GameState.AwaitThrow, _rig.Service.Current.State, "the ticks stayed in the state under test");
        }

        [Test]
        public void TickWhileRollingAllocatesNothing()
        {
            _rig.PlayToFirstBall();
            _rig.Throws.SetPhase(SwingPhase.Ready);
            _rig.Throws.EmitThrow(GameplayRig.Throw);
            Assert.AreEqual(GameState.Rolling, _rig.Service.Current.State);
            _rig.Service.Tick();    // warm-up

            Assert.That(Ticks100TimesAt64Hz, Is.Not.AllocatingGCMemory());

            Assert.AreEqual(GameState.Rolling, _rig.Service.Current.State, "the ticks stayed in the state under test");
        }

        private void Ticks100TimesAt64Hz()
        {
            for (int i = 0; i < 100; i++)
            {
                _rig.Clock.Advance(1.0 / 64);
                _rig.Service.Tick();
            }
        }
    }
}
