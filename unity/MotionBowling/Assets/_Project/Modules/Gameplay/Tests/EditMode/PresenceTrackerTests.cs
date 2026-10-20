using NUnit.Framework;

namespace Bowling.Gameplay.Tests
{
    /// <summary>
    /// <see cref="PresenceTracker"/> on its own (P-3): absence counts from the moment presence became false, a repeated
    /// observation keeps that moment, and presence returning resets it.
    /// </summary>
    public sealed class PresenceTrackerTests
    {
        [Test]
        public void PresentAtConstructionIsNotAbsent()
        {
            var tracker = new PresenceTracker(true, 0.0);

            Assert.AreEqual(0.0, tracker.AbsentFor(2.0));
        }

        [Test]
        public void AbsenceCountsFromWhenPresenceWentFalse()
        {
            var tracker = new PresenceTracker(true, 0.0);

            tracker.Observe(false, 1.0);

            Assert.AreEqual(2.0, tracker.AbsentFor(3.0));
        }

        [Test]
        public void ARepeatedAbsenceKeepsTheStart()
        {
            var tracker = new PresenceTracker(true, 0.0);
            tracker.Observe(false, 1.0);

            tracker.Observe(false, 2.0);

            Assert.AreEqual(2.0, tracker.AbsentFor(3.0));
        }

        [Test]
        public void PresenceReturningResetsTheAbsence()
        {
            var tracker = new PresenceTracker(true, 0.0);
            tracker.Observe(false, 1.0);

            tracker.Observe(true, 4.0);

            Assert.AreEqual(0.0, tracker.AbsentFor(5.0));
        }

        [Test]
        public void AbsentAtConstructionCountsFromConstruction()
        {
            var tracker = new PresenceTracker(false, 1.0);

            Assert.AreEqual(0.5, tracker.AbsentFor(1.5));
        }
    }
}
