using System;
using System.Collections.Generic;
using Bowling.Contracts;
using Bowling.Testing;
using UnityEngine;

namespace Bowling.Gameplay.Tests
{
    /// <summary>
    /// A <see cref="GameplayService"/> on the <c>Bowling.Testing</c> fakes, for every flow test. It records each game
    /// event in <see cref="Events"/> (and each state in <see cref="States"/>), marking an entry " stale" when the
    /// handler saw a <c>Current</c> that wasn't the new snapshot yet (R21, R22). Dispose it in <c>[TearDown]</c>: it
    /// owns two ScriptableObjects.
    /// </summary>
    internal sealed class GameplayRig : IDisposable
    {
        /// <summary>The wall-clock time the service and the default leaderboard see.</summary>
        public static readonly DateTime UtcNow = new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

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

        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(Tuning);
            UnityEngine.Object.DestroyImmediate(Difficulties);
        }

        private void OnStateChanged(GameState state)
        {
            States.Add(state);
            Events.Add("State:" + state + Stale(Service.Current.State != state));
        }

        private void OnRollScored(RollOutcome outcome)
        {
            Events.Add("Rolled:" + outcome.PlayerIndex + ":" + outcome.Mark + Stale(Service.Current.State != GameState.Scoring));
        }

        private void OnSnapshotChanged(GameSnapshot snapshot)
        {
            Events.Add("Snapshot:" + snapshot.State + Stale(!ReferenceEquals(snapshot, Service.Current)));
        }

        private static string Stale(bool stale)
        {
            return stale ? " stale" : string.Empty;
        }
    }
}
