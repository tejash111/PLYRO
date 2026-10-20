using System;
using System.Collections.Generic;
using Bowling.Contracts;
using Bowling.Testing;
using UnityEngine;

namespace Bowling.Gameplay.Tests
{
    /// <summary>
    /// A <see cref="GameplayService"/> on the <c>Bowling.Testing</c> fakes, for every flow test. It records each game
    /// event in <see cref="Events"/> (and each state in <see cref="States"/>, each outcome in <see cref="Outcomes"/>),
    /// marking an entry " stale" when the handler saw a <c>Current</c> that wasn't the new snapshot yet (R21, R22).
    /// Dispose it in <c>[TearDown]</c>: it owns two ScriptableObjects.
    /// </summary>
    internal sealed class GameplayRig : IDisposable
    {
        /// <summary>The wall-clock time the service and the default leaderboard see.</summary>
        public static readonly DateTime UtcNow = new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

        /// <summary>The profile <see cref="CompleteCalibration"/> hands the service. Not the default profile, so a test can tell them apart.</summary>
        public static readonly PlayerProfile Profile = new PlayerProfile(Handedness.Left, 0.70f, 0.42f, 0.45f);

        /// <summary>The throw <see cref="Bowl"/> emits. Its <c>Timestamp</c> is on the camera clock, not <see cref="Clock"/>.</summary>
        public static readonly ThrowEvent Throw = new ThrowEvent(1.0, Handedness.Left, 7.0f, 0.5f, 0.1f, 0.2f, 0.9f);

        public readonly FakeClock Clock = new FakeClock();
        public readonly FakePlayerInput Input = new FakePlayerInput();
        public readonly FakeCalibrator Calibrator = new FakeCalibrator();
        public readonly FakeThrowSource Throws = new FakeThrowSource();
        public readonly FakeRollSimulator Sim = new FakeRollSimulator();
        public readonly InMemorySettingsStore Settings = new InMemorySettingsStore();
        public readonly RecordingGameLog Log = new RecordingGameLog();
        public readonly ILeaderboardStore Leaderboard;
        public readonly GameplayTuning Tuning;
        public readonly DifficultyCatalog Difficulties;
        public readonly GameplayService Service;

        /// <summary>Every StateChanged argument, in order.</summary>
        public readonly List<GameState> States = new List<GameState>();

        /// <summary>"State:&lt;s&gt;", "Rolled:&lt;player&gt;:&lt;mark&gt;" and "Snapshot:&lt;s&gt;", in raise order.</summary>
        public readonly List<string> Events = new List<string>();

        /// <summary>Every RollScored argument, in order.</summary>
        public readonly List<RollOutcome> Outcomes = new List<RollOutcome>();

        /// <summary>
        /// <c>Sim.LastRack</c> at construction (if a rack was set by then) and at every StateChanged that follows a new
        /// <c>SetRack</c> call: the rack each state was entered with.
        /// </summary>
        public readonly List<PinMask> Racks = new List<PinMask>();

        private int _racksSeen;     // Sim.SetRackCount at the last Racks entry

        /// <summary>
        /// Builds the service. <paramref name="leaderboard"/> defaults to an in-memory store on <see cref="UtcNow"/>.
        /// With <paramref name="rollingAtStart"/>, a roll is already in progress when the service is built.
        /// </summary>
        public GameplayRig(ILeaderboardStore leaderboard = null, bool rollingAtStart = false)
        {
            Leaderboard = leaderboard ?? new InMemoryLeaderboardStore(() => UtcNow, TimeZoneInfo.Utc);
            Tuning = ScriptableObject.CreateInstance<GameplayTuning>();
            Difficulties = ScriptableObject.CreateInstance<DifficultyCatalog>();
            if (rollingAtStart)
            {
                Sim.Roll(default);
            }

            Service = new GameplayService(
                Input, Calibrator, Throws, Sim, Leaderboard, Settings, Clock, Log, () => UtcNow, Tuning, Difficulties);

            Service.StateChanged += OnStateChanged;
            Service.RollScored += OnRollScored;
            Service.SnapshotChanged += OnSnapshotChanged;
            RecordRackIfSet();
        }

        /// <summary>Moves time forward by <paramref name="seconds"/>, then ticks once.</summary>
        public void Wait(double seconds)
        {
            Clock.Advance(seconds);
            Service.Tick();
        }

        /// <summary>From Attract: the player steps in and the greeting runs out, which leaves the game in ModeSelect.</summary>
        public void Join()
        {
            Input.SetPresent(true);
            Service.Tick();
            Wait(Tuning.JoinGreeting);
        }

        /// <summary>From Attract: <see cref="Join"/>, then picks the mode, which leaves the game in Calibrate.</summary>
        public void StartGame(int players = 1, int frames = 3, Difficulty difficulty = Difficulty.Casual)
        {
            Join();
            Service.SelectMode(players, frames, difficulty);
        }

        /// <summary>In Calibrate: the calibrator finishes with <paramref name="profile"/> (default <see cref="Profile"/>).</summary>
        public void CompleteCalibration(PlayerProfile? profile = null)
        {
            Calibrator.Complete(profile ?? Profile);
        }

        /// <summary>From Attract: <see cref="StartGame"/>, then <see cref="CompleteCalibration"/>, which leaves the game in AwaitReady.</summary>
        public void PlayToFirstBall(int players = 1, int frames = 3, Difficulty difficulty = Difficulty.Casual)
        {
            StartGame(players, frames, difficulty);
            CompleteCalibration();
        }

        /// <summary>In AwaitReady: the swing starts, <see cref="Throw"/> is released, and the roll ends with <paramref name="standingAfter"/> up.</summary>
        public void Bowl(PinMask standingAfter)
        {
            Throws.SetPhase(SwingPhase.Ready);
            Throws.EmitThrow(Throw);
            Sim.CompleteWith(standingAfter);
        }

        /// <summary><see cref="Bowl"/>, knocking down the <paramref name="pins"/> lowest-numbered pins of <c>Sim.Standing</c>.</summary>
        public void Knock(int pins)
        {
            PinMask standing = Sim.Standing;
            for (int bit = 0; bit < 10 && pins > 0; bit++)
            {
                var pin = (PinMask)(1 << bit);
                if ((standing & pin) != 0)
                {
                    standing &= ~pin;
                    pins--;
                }
            }

            Bowl(standing);
        }

        /// <summary>In Scoring: waits out the hold for the last outcome's mark (the replay hold for a Strike or Spare).</summary>
        public void EndScoring()
        {
            MarkKind mark = Outcomes[Outcomes.Count - 1].Mark;
            Wait(mark == MarkKind.Strike || mark == MarkKind.Spare ? Tuning.ScoringHoldReplay : Tuning.ScoringHold);
        }

        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(Tuning);
            UnityEngine.Object.DestroyImmediate(Difficulties);
        }

        private void OnStateChanged(GameState state)
        {
            States.Add(state);
            Events.Add("State:" + state + Stale(Service.Current.State != state));
            RecordRackIfSet();
        }

        private void OnRollScored(RollOutcome outcome)
        {
            Outcomes.Add(outcome);
            Events.Add("Rolled:" + outcome.PlayerIndex + ":" + outcome.Mark + Stale(Service.Current.State != GameState.Scoring));
        }

        private void OnSnapshotChanged(GameSnapshot snapshot)
        {
            Events.Add("Snapshot:" + snapshot.State + Stale(!ReferenceEquals(snapshot, Service.Current)));
        }

        private void RecordRackIfSet()
        {
            if (Sim.SetRackCount > _racksSeen)
            {
                Racks.Add(Sim.LastRack);
                _racksSeen = Sim.SetRackCount;
            }
        }

        private static string Stale(bool stale)
        {
            return stale ? " stale" : string.Empty;
        }
    }
}
