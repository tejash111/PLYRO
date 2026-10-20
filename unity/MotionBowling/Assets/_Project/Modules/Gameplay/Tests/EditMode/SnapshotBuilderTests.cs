using System;
using System.Collections.Generic;
using Bowling.Contracts;
using NUnit.Framework;

namespace Bowling.Gameplay.Tests
{
    /// <summary>What <see cref="SnapshotBuilder"/> publishes: lobby snapshots, session mirrors, and immutability.</summary>
    public sealed class SnapshotBuilderTests
    {
        private static readonly DifficultyProfile StandardProfile = new DifficultyProfile(Difficulty.Standard, 0.5f, false);

        // The rack with its lowest-numbered pins removed.
        private static PinMask Knock(PinMask rack, int pins)
        {
            PinMask after = rack;
            for (int bit = 1; pins > 0 && bit <= (int)PinMask.All; bit <<= 1)
            {
                var pin = (PinMask)bit;
                if ((rack & pin) != PinMask.None)
                {
                    after &= ~pin;
                    pins--;
                }
            }

            return after;
        }

        private static MarkKind Roll(GameSession session, int pins)
        {
            return session.ScoreRoll(new RollResult(session.Rack, Knock(session.Rack, pins), false, RollEndReason.Settled));
        }

        private static void AssertRolls(IReadOnlyList<RollMark> actual, params (int Pins, MarkKind Kind)[] expected)
        {
            Assert.AreEqual(expected.Length, actual.Count);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].Pins, actual[i].Pins, $"roll {i} pins");
                Assert.AreEqual(expected[i].Kind, actual[i].Kind, $"roll {i} kind");
            }
        }

        [Test]
        public void LobbySnapshotHasNoPlayers()
        {
            GameSnapshot snapshot = SnapshotBuilder.Lobby(GameState.ModeSelect, 3, StandardProfile);

            Assert.AreEqual(GameState.ModeSelect, snapshot.State);
            Assert.IsNotNull(snapshot.Players);
            Assert.AreEqual(0, snapshot.Players.Count);
            Assert.AreEqual(0, snapshot.CurrentPlayer);
            Assert.AreEqual(1, snapshot.CurrentFrame);
            Assert.AreEqual(1, snapshot.CurrentBall);
            Assert.AreEqual(3, snapshot.FrameCount);
            Assert.AreEqual(StandardProfile, snapshot.Difficulty);
        }

        [Test]
        public void InGameMirrorsTheSession()
        {
            var session = new GameSession(2, 3, StandardProfile);
            Roll(session, 10);
            session.Advance();
            Roll(session, 7);

            GameSnapshot snapshot = SnapshotBuilder.InGame(GameState.Scoring, session);

            Assert.AreEqual(GameState.Scoring, snapshot.State);
            Assert.AreEqual(2, snapshot.Players.Count);
            Assert.AreEqual(3, snapshot.FrameCount);
            Assert.AreEqual(StandardProfile, snapshot.Difficulty);

            PlayerSnapshot first = snapshot.Players[0];
            Assert.AreEqual("Player 1", first.DisplayName);
            Assert.AreEqual(0, first.ColorIndex);
            Assert.AreEqual(3, first.Frames.Count);
            Assert.AreEqual(1, first.Frames[0].FrameNumber);
            Assert.AreEqual(3, first.Frames[2].FrameNumber);
            AssertRolls(first.Frames[0].Rolls, (10, MarkKind.Strike));
            Assert.IsNull(first.Frames[0].Cumulative);
            Assert.AreEqual(0, first.RunningTotal);

            PlayerSnapshot second = snapshot.Players[1];
            Assert.AreEqual("Player 2", second.DisplayName);
            Assert.AreEqual(1, second.ColorIndex);
            AssertRolls(second.Frames[0].Rolls, (7, MarkKind.Count));
            Assert.AreEqual(0, second.Frames[1].Rolls.Count);

            // P-11: during Scoring the position names the roll just scored.
            Assert.AreEqual(1, snapshot.CurrentPlayer);
            Assert.AreEqual(1, snapshot.CurrentFrame);
            Assert.AreEqual(1, snapshot.CurrentBall);
        }

        [Test]
        public void APublishedSnapshotNeverChanges()
        {
            var session = new GameSession(1, 3, StandardProfile);
            Roll(session, 10);
            GameSnapshot first = SnapshotBuilder.InGame(GameState.Scoring, session);

            session.Advance();
            Roll(session, 3);
            session.Advance();
            Roll(session, 4);
            GameSnapshot later = SnapshotBuilder.InGame(GameState.Scoring, session);

            Assert.IsNull(first.Players[0].Frames[0].Cumulative);
            Assert.AreEqual(0, first.Players[0].Frames[1].Rolls.Count);
            Assert.AreEqual(0, first.Players[0].RunningTotal);
            Assert.AreEqual(17, later.Players[0].Frames[0].Cumulative);

            Assert.AreNotSame(first, later);
            Assert.AreNotSame(first.Players, later.Players);
            Assert.AreNotSame(first.Players[0], later.Players[0]);
            Assert.AreNotSame(first.Players[0].Frames, later.Players[0].Frames);
            Assert.AreNotSame(first.Players[0].Frames[0], later.Players[0].Frames[0]);
            Assert.AreNotSame(first.Players[0].Frames[0].Rolls, later.Players[0].Frames[0].Rolls);
        }

        [Test]
        public void NullSessionThrows()
        {
            Assert.Throws<ArgumentNullException>(() => SnapshotBuilder.InGame(GameState.Scoring, null));
        }
    }
}
