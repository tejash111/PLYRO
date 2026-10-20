using System;
using System.Collections.Generic;
using System.Reflection;
using Bowling.Contracts;
using NUnit.Framework;

namespace Bowling.Testing.Tests
{
    // Pinned semantics of FakeGameEvents (ruling R21: Current is replaced before any event is raised, and
    // StateChanged fires on every actual state change).
    public class FakeGameEventsTests
    {
        private static GameSnapshot Snapshot()
        {
            return new GameSnapshot
            {
                State = GameState.AwaitThrow,
                Players = new List<PlayerSnapshot>
                {
                    new PlayerSnapshot { DisplayName = "Player 1", ColorIndex = 0 },
                    new PlayerSnapshot { DisplayName = "Player 2", ColorIndex = 1 },
                },
                CurrentPlayer = 1,
                CurrentFrame = 4,
                CurrentBall = 2,
                FrameCount = 5,
                Difficulty = new DifficultyProfile(Difficulty.Pro, 0.3f, true),
            };
        }

        private static List<string> Record(FakeGameEvents events)
        {
            var log = new List<string>();
            events.StateChanged += s => log.Add("StateChanged:" + s + ":" + events.Current.State);
            events.SnapshotChanged += s => log.Add("SnapshotChanged:" + s.State + ":" + ReferenceEquals(s, events.Current));
            events.RollScored += o => log.Add("RollScored:" + o.PlayerIndex + ":" + o.Mark);
            return log;
        }

        [Test]
        public void StartsWithAnAttractSnapshotAndNoPlayers()
        {
            var events = new FakeGameEvents();

            Assert.IsNotNull(events.Current);
            Assert.AreEqual(GameState.Attract, events.Current.State);
            Assert.IsNotNull(events.Current.Players);
            Assert.AreEqual(0, events.Current.Players.Count);
        }

        [Test]
        public void SetStatePublishesACopyWithTheNewStateAndRaisesStateChangedThenSnapshotChanged()
        {
            var events = new FakeGameEvents();
            GameSnapshot before = Snapshot();
            events.Publish(before);
            List<string> log = Record(events);

            events.SetState(GameState.Rolling);

            Assert.That(log, Is.EqualTo(new[] { "StateChanged:Rolling:Rolling", "SnapshotChanged:Rolling:True" }),
                "Current already holds the new snapshot inside both handlers");
            GameSnapshot after = events.Current;
            Assert.AreNotSame(before, after, "a new snapshot, not the old one mutated");
            Assert.AreEqual(GameState.AwaitThrow, before.State, "the previous snapshot is untouched");
            Assert.AreEqual(GameState.Rolling, after.State);
        }

        [Test]
        public void SetStateCarriesEveryOtherFieldOfThePreviousSnapshot()
        {
            var events = new FakeGameEvents();
            GameSnapshot before = Snapshot();
            events.Publish(before);

            events.SetState(GameState.Scoring);

            GameSnapshot after = events.Current;
            Assert.AreSame(before.Players, after.Players);
            Assert.AreEqual(1, after.CurrentPlayer);
            Assert.AreEqual(4, after.CurrentFrame);
            Assert.AreEqual(2, after.CurrentBall);
            Assert.AreEqual(5, after.FrameCount);
            Assert.AreEqual(Difficulty.Pro, after.Difficulty.Level);
            Assert.AreEqual(0.3f, after.Difficulty.Assist);
            Assert.IsTrue(after.Difficulty.Bumpers);
        }

        [Test]
        public void SetStateChainsOnTheSnapshotItPublishedLast()
        {
            var events = new FakeGameEvents();
            events.Publish(Snapshot());

            events.SetState(GameState.Rolling);
            events.SetState(GameState.Scoring);

            Assert.AreEqual(GameState.Scoring, events.Current.State);
            Assert.AreEqual(4, events.Current.CurrentFrame);
        }

        [Test]
        public void SetStateToTheSameStateStillRaisesBothEvents()
        {
            var events = new FakeGameEvents();
            List<string> log = Record(events);

            events.SetState(GameState.Attract);

            Assert.That(log, Is.EqualTo(new[] { "StateChanged:Attract:Attract", "SnapshotChanged:Attract:True" }));
        }

        [Test]
        public void PublishReplacesCurrentThenRaisesStateChangedThenSnapshotChangedWhenTheStateDiffers()
        {
            var events = new FakeGameEvents();
            List<string> log = Record(events);
            GameSnapshot snapshot = Snapshot();

            events.Publish(snapshot);

            Assert.AreSame(snapshot, events.Current);
            Assert.That(log, Is.EqualTo(new[] { "StateChanged:AwaitThrow:AwaitThrow", "SnapshotChanged:AwaitThrow:True" }),
                "Attract to AwaitThrow is a state change, so StateChanged fires first, with Current already replaced");
        }

        [Test]
        public void PublishRaisesOnlySnapshotChangedWhenTheStateIsUnchanged()
        {
            var events = new FakeGameEvents();
            GameSnapshot first = Snapshot();
            events.Publish(first);
            List<string> log = Record(events);
            GameSnapshot second = Snapshot();
            second.CurrentFrame = 5;

            events.Publish(second);

            Assert.AreSame(second, events.Current);
            Assert.That(log, Is.EqualTo(new[] { "SnapshotChanged:AwaitThrow:True" }));
        }


        [Test]
        public void RaiseRollScoredForwardsTheOutcome()
        {
            var events = new FakeGameEvents();
            List<string> log = Record(events);
            RollResult result = new RollResult(PinMask.All, PinMask.None, false, RollEndReason.Settled);

            events.RaiseRollScored(new RollOutcome(2, result, MarkKind.Strike));

            Assert.That(log, Is.EqualTo(new[] { "RollScored:2:Strike" }));
        }

        [Test]
        public void PublishScoredRollRaisesInR22Order()
        {
            var events = new FakeGameEvents();
            events.Publish(Snapshot());
            List<string> log = Record(events);
            GameSnapshot scoring = Snapshot();
            scoring.State = GameState.Scoring;

            events.PublishScoredRoll(scoring, new RollOutcome(1, default, MarkKind.Strike));

            Assert.AreSame(scoring, events.Current);
            Assert.That(log, Is.EqualTo(new[] { "StateChanged:Scoring:Scoring", "RollScored:1:Strike", "SnapshotChanged:Scoring:True" }),
                "AwaitThrow to Scoring is a state change; Current is already replaced inside every handler");
        }

        [Test]
        public void PublishScoredRollInTheSameStateSkipsStateChanged()
        {
            var events = new FakeGameEvents();
            GameSnapshot scoring = Snapshot();
            scoring.State = GameState.Scoring;
            events.Publish(scoring);
            List<string> log = Record(events);
            GameSnapshot next = Snapshot();
            next.State = GameState.Scoring;

            events.PublishScoredRoll(next, new RollOutcome(1, default, MarkKind.Strike));

            Assert.AreSame(next, events.Current);
            Assert.That(log, Is.EqualTo(new[] { "RollScored:1:Strike", "SnapshotChanged:Scoring:True" }));
        }

        [Test]
        public void PublishScoredRollRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new FakeGameEvents().PublishScoredRoll(null, default));
        }

        [Test]
        public void PublishRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new FakeGameEvents().Publish(null));
        }

        [Test]
        public void GameSnapshotStillHasTheSevenFieldsSetStateCopies()
        {
            // FakeGameEvents.SetState copies GameSnapshot field by field. If this fails, a field was added:
            // update the copy in FakeGameEvents and the carry-over assertions above.
            FieldInfo[] fields = typeof(GameSnapshot).GetFields(BindingFlags.Public | BindingFlags.Instance);

            Assert.AreEqual(7, fields.Length);
        }
    }
}
