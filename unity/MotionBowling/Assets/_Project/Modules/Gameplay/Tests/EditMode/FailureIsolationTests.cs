using System;
using System.Linq;
using Bowling.Contracts;
using Bowling.Testing;
using NUnit.Framework;

namespace Bowling.Gameplay.Tests
{
    /// <summary>
    /// Unexpected inputs and failing collaborators never break the game (spec section 11, P-8, P-9, P-10): a throwing
    /// subscriber is logged and the game plays on, an event the state doesn't take is ignored with a warning, and a
    /// rack that differs from the simulator's is warned about and scored as Gameplay has it. The failing leaderboard
    /// write (P-12) is covered by <c>GameEndFlowTests.AFailingSubmitIsLoggedAndTheOthersStillGo</c>.
    /// </summary>
    public sealed class FailureIsolationTests
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

        private GameSnapshot Current => _rig.Service.Current;

        [TestCase("StateChanged")]
        [TestCase("RollScored")]
        [TestCase("SnapshotChanged")]
        public void AThrowingSubscriberDoesNotStopTheGame(string eventName)
        {
            var failure = new InvalidOperationException("The subscriber refused " + eventName + ".");
            int raisesBefore = Raises(eventName);
            Subscribe(eventName, () => throw failure);

            _rig.PlayToFirstBall();
            _rig.Knock(10);
            _rig.EndScoring();

            int raises = Raises(eventName) - raisesBefore;
            Assert.AreEqual(GameState.AwaitReady, Current.State);
            Assert.That(_rig.Events, Has.None.EndsWith(" stale"), "every other subscriber still saw each event in order");
            Assert.AreEqual(1, _rig.Outcomes.Count);
            Assert.Greater(raises, 0);
            Assert.AreEqual(raises, _rig.Log.Entries.Count, "one entry per raise, and nothing else is logged");
            Assert.That(_rig.Log.Entries, Has.All.Matches<GameLogEntry>(e =>
                e.Level == GameLogLevel.Error && e.Category == "Gameplay"
                && e.Message == eventName + " subscriber failed" && ReferenceEquals(e.Exception, failure)));
        }

        [Test]
        public void AttractIgnoresARollResult()
        {
            AssertIgnored("RollCompleted ignored in Attract", () =>
            {
                _rig.Sim.Roll(GameplayRig.Throw);
                _rig.Sim.CompleteWith(PinMask.None);
            });
        }

        [Test]
        public void JoinIgnoresAThrow()
        {
            _rig.Input.SetPresent(true);
            _rig.Service.Tick();
            Assert.AreEqual(GameState.Join, Current.State);

            // Arming raises PhaseChanged(WaitingForReady), which stays silent: only the Thrown that follows warns.
            AssertIgnored("Thrown ignored in Join", () =>
            {
                _rig.Throws.Arm(default);
                _rig.Throws.EmitThrow(GameplayRig.Throw);
            });
            Assert.AreEqual(0, _rig.Sim.RollCount);
        }

        [Test]
        public void JoinIgnoresARejection()
        {
            _rig.Input.SetPresent(true);
            _rig.Service.Tick();
            Assert.AreEqual(GameState.Join, Current.State);

            AssertIgnored("Rejected ignored in Join", () =>
            {
                _rig.Throws.Arm(default);
                _rig.Throws.Reject(ThrowRejection.LowConfidence);
            });
        }

        [Test]
        public void AwaitReadyIgnoresACalibrationResult()
        {
            _rig.PlayToFirstBall();

            AssertIgnored("CalibrationCompleted ignored in AwaitReady", () =>
            {
                _rig.Calibrator.Begin();
                _rig.Calibrator.Complete(GameplayRig.Profile);
            });
        }

        [Test]
        public void AwaitThrowIgnoresACalibrationFailure()
        {
            _rig.PlayToFirstBall();
            _rig.Throws.SetPhase(SwingPhase.Ready);
            Assert.AreEqual(GameState.AwaitThrow, Current.State);

            AssertIgnored("CalibrationFailed ignored in AwaitThrow", () =>
            {
                _rig.Calibrator.Begin();
                _rig.Calibrator.Fail(CalibrationFailure.Timeout);
            });
        }

        [Test]
        public void AMismatchedRackIsWarnedAndScoredAgainstGameplaysRack()
        {
            _rig.PlayToFirstBall();
            _rig.Sim.SetRack(PinMask.P1);

            _rig.Bowl(PinMask.None);

            Assert.AreEqual(MarkKind.Strike, _rig.Outcomes[0].Mark, "Gameplay's rack is All, so nothing standing is a Strike");
            Assert.AreEqual(PinMask.P1, _rig.Outcomes[0].Result.StandingBefore, "the simulator's result passes through unchanged");
            Assert.AreEqual(10, Current.Players[0].Frames[0].Rolls[0].Pins);
            Assert.That(_rig.Log.Entries, Has.Exactly(1).Matches<GameLogEntry>(e =>
                e.Level == GameLogLevel.Warn && e.Category == "Gameplay"
                && e.Message == "StandingBefore P1 differs from the rack All; scoring against the rack"));
            Assert.AreEqual(1, _rig.Log.Entries.Count);
        }

        // Runs the action, which raises an event the current state doesn't take: it logs one warning with the message,
        // and publishes nothing.
        private void AssertIgnored(string message, Action action)
        {
            GameState state = Current.State;
            int eventsBefore = _rig.Events.Count;
            int logBefore = _rig.Log.Entries.Count;

            action();

            Assert.AreEqual(state, Current.State);
            Assert.AreEqual(eventsBefore, _rig.Events.Count);
            Assert.AreEqual(logBefore + 1, _rig.Log.Entries.Count, "one warning, and no other entry");
            GameLogEntry entry = _rig.Log.Entries[logBefore];
            Assert.AreEqual(GameLogLevel.Warn, entry.Level);
            Assert.AreEqual("Gameplay", entry.Category);
            Assert.AreEqual(message, entry.Message);
        }

        private void Subscribe(string eventName, Action thrower)
        {
            switch (eventName)
            {
                case "StateChanged":
                    _rig.Service.StateChanged += state => thrower();
                    break;
                case "RollScored":
                    _rig.Service.RollScored += outcome => thrower();
                    break;
                case "SnapshotChanged":
                    _rig.Service.SnapshotChanged += snapshot => thrower();
                    break;
                default:
                    throw new ArgumentException("Unknown event " + eventName, nameof(eventName));
            }
        }

        // How many times the rig has seen the event raised, from the lists it keeps.
        private int Raises(string eventName)
        {
            switch (eventName)
            {
                case "StateChanged":
                    return _rig.States.Count;
                case "RollScored":
                    return _rig.Outcomes.Count;
                case "SnapshotChanged":
                    return _rig.Events.Count(e => e.StartsWith("Snapshot:", StringComparison.Ordinal));
                default:
                    throw new ArgumentException("Unknown event " + eventName, nameof(eventName));
            }
        }
    }
}
