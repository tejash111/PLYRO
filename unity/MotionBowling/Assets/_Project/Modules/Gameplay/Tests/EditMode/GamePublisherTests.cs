using System;
using System.Collections.Generic;
using Bowling.Contracts;
using Bowling.Testing;
using NUnit.Framework;

namespace Bowling.Gameplay.Tests
{
    /// <summary>
    /// What <see cref="GamePublisher"/> guarantees: <c>Current</c> is assigned before any event (R22), a scored roll
    /// raises StateChanged, RollScored, SnapshotChanged in that order, and one throwing subscriber is logged without
    /// stopping the others (P-8).
    /// </summary>
    public sealed class GamePublisherTests
    {
        private static readonly RollResult AnyResult = new RollResult(PinMask.All, PinMask.None, false, RollEndReason.Settled);

        private RecordingGameLog _log;
        private GamePublisher _publisher;

        [SetUp]
        public void SetUp()
        {
            _log = new RecordingGameLog();
            _publisher = new GamePublisher(_log);
        }

        private static GameSnapshot Snapshot(GameState state)
        {
            return SnapshotBuilder.Lobby(state, 10, new DifficultyProfile(Difficulty.Standard, 0.5f, false));
        }

        // Subscribes to all three events and notes, for each, what Current held while the handler ran.
        private List<string> Record(GamePublisher publisher)
        {
            var log = new List<string>();
            publisher.StateChanged += s => log.Add("State:" + s + ":" + publisher.Current.State);
            publisher.RollScored += o => log.Add("Rolled:" + o.PlayerIndex + ":" + o.Mark + ":" + publisher.Current.State);
            publisher.SnapshotChanged += s => log.Add("Snapshot:" + s.State + ":" + ReferenceEquals(s, publisher.Current));
            return log;
        }

        [Test]
        public void CurrentIsNullUntilSeeded()
        {
            Assert.IsNull(_publisher.Current);
        }

        [Test]
        public void SeedAssignsCurrentQuietly()
        {
            List<string> log = Record(_publisher);
            GameSnapshot snap = Snapshot(GameState.Attract);

            _publisher.Seed(snap);

            Assert.AreSame(snap, _publisher.Current);
            Assert.IsEmpty(log);
        }

        [Test]
        public void PublishStateAssignsCurrentFirst()
        {
            List<string> log = Record(_publisher);
            GameSnapshot awaitReady = Snapshot(GameState.AwaitReady);

            _publisher.PublishState(awaitReady);

            Assert.That(log, Is.EqualTo(new[] { "State:AwaitReady:AwaitReady", "Snapshot:AwaitReady:True" }));
            Assert.AreSame(awaitReady, _publisher.Current);
        }

        [Test]
        public void PublishScoredRollUsesTheR22Order()
        {
            List<string> log = Record(_publisher);
            GameSnapshot scoring = Snapshot(GameState.Scoring);

            _publisher.PublishScoredRoll(scoring, new RollOutcome(1, AnyResult, MarkKind.Spare));

            Assert.That(log, Is.EqualTo(new[] { "State:Scoring:Scoring", "Rolled:1:Spare:Scoring", "Snapshot:Scoring:True" }));
            Assert.AreSame(scoring, _publisher.Current);
        }

        [Test]
        public void PublishingWithNoSubscribersIsHarmless()
        {
            GameSnapshot scoring = Snapshot(GameState.Scoring);

            Assert.DoesNotThrow(() => _publisher.PublishState(scoring));
            Assert.DoesNotThrow(() => _publisher.PublishScoredRoll(scoring, new RollOutcome(0, AnyResult, MarkKind.Count)));
            Assert.IsEmpty(_log.Entries);
        }

        [TestCase("StateChanged")]
        [TestCase("RollScored")]
        [TestCase("SnapshotChanged")]
        public void AThrowingSubscriberIsLoggedAndTheOthersRun(string eventName)
        {
            var boom = new InvalidOperationException("boom");
            switch (eventName)
            {
                case "StateChanged":
                    _publisher.StateChanged += _ => throw boom;
                    break;
                case "RollScored":
                    _publisher.RollScored += _ => throw boom;
                    break;
                default:
                    _publisher.SnapshotChanged += _ => throw boom;
                    break;
            }

            List<string> log = Record(_publisher);

            _publisher.PublishScoredRoll(Snapshot(GameState.Scoring), new RollOutcome(1, AnyResult, MarkKind.Spare));

            Assert.That(log, Is.EqualTo(new[] { "State:Scoring:Scoring", "Rolled:1:Spare:Scoring", "Snapshot:Scoring:True" }));
            Assert.AreEqual(1, _log.Entries.Count);
            GameLogEntry entry = _log.Entries[0];
            Assert.AreEqual(GameLogLevel.Error, entry.Level);
            Assert.AreEqual("Gameplay", entry.Category);
            Assert.AreEqual(eventName + " subscriber failed", entry.Message);
            Assert.AreSame(boom, entry.Exception);
        }

        [Test]
        public void NullLogThrows()
        {
            Assert.Throws<ArgumentNullException>(() => new GamePublisher(null));
        }

        [Test]
        public void NullSnapshotThrows()
        {
            Assert.Throws<ArgumentNullException>(() => _publisher.Seed(null));
            Assert.Throws<ArgumentNullException>(() => _publisher.PublishState(null));
            Assert.Throws<ArgumentNullException>(() => _publisher.PublishScoredRoll(null, new RollOutcome(0, AnyResult, MarkKind.Count)));
        }
    }
}
