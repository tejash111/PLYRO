using System;
using System.Collections.Generic;
using Bowling.Contracts;
using Bowling.Testing;
using NUnit.Framework;

namespace Bowling.Gameplay.Tests
{
    /// <summary>
    /// Pins <see cref="FakeGameEvents.PublishScoredRoll"/> to the real <see cref="GamePublisher"/>: UI and
    /// Presentation tests drive the fake, so a scored roll must reach their handlers in the same order (R22), with
    /// <c>Current</c> already replaced, as it does in the game.
    /// </summary>
    public sealed class PublisherParityTests
    {
        private static readonly RollResult AnyResult = new RollResult(PinMask.All, PinMask.None, false, RollEndReason.Settled);

        private static GameSnapshot Snapshot(GameState state)
        {
            return SnapshotBuilder.Lobby(state, 10, new DifficultyProfile(Difficulty.Standard, 0.5f, false));
        }

        // Subscribes one recorder format to any source, given its three registrations and a way to read its Current.
        private static List<string> Record(
            Action<Action<GameState>> onState,
            Action<Action<RollOutcome>> onRoll,
            Action<Action<GameSnapshot>> onSnapshot,
            Func<GameSnapshot> current)
        {
            var log = new List<string>();
            onState(s => log.Add("State:" + s + ":" + current().State));
            onRoll(o => log.Add("Rolled:" + o.PlayerIndex + ":" + o.Mark + ":" + current().State));
            onSnapshot(s => log.Add("Snapshot:" + s.State + ":" + ReferenceEquals(s, current())));
            return log;
        }

        [Test]
        public void ScoredRollMatchesTheFake()
        {
            var publisher = new GamePublisher(new RecordingGameLog());
            var fake = new FakeGameEvents();
            publisher.Seed(Snapshot(GameState.Rolling));
            fake.Publish(Snapshot(GameState.Rolling));
            List<string> real = Record(h => publisher.StateChanged += h, h => publisher.RollScored += h,
                h => publisher.SnapshotChanged += h, () => publisher.Current);
            List<string> faked = Record(h => fake.StateChanged += h, h => fake.RollScored += h,
                h => fake.SnapshotChanged += h, () => fake.Current);
            GameSnapshot scoring = Snapshot(GameState.Scoring);
            var outcome = new RollOutcome(1, AnyResult, MarkKind.Spare);

            publisher.PublishScoredRoll(scoring, outcome);
            fake.PublishScoredRoll(scoring, outcome);

            Assert.That(real, Is.EqualTo(new[] { "State:Scoring:Scoring", "Rolled:1:Spare:Scoring", "Snapshot:Scoring:True" }));
            Assert.That(faked, Is.EqualTo(real), "the fake raises what the real publisher raises, in the same order");
            Assert.AreSame(scoring, publisher.Current);
            Assert.AreSame(scoring, fake.Current);
        }
    }
}
