using System;
using Bowling.Contracts;
using Bowling.Testing;
using NUnit.Framework;

namespace Bowling.Gameplay.Tests
{
    /// <summary>
    /// The lobby states (Attract, Join, ModeSelect) and the way into Calibrate (spec section 4): construction enters
    /// Attract quietly behind the Attract guard (P-11), presence and timers move the game on (P-3), and SelectMode
    /// validates in any state but only acts in ModeSelect (P-6).
    /// </summary>
    public sealed class LobbyFlowTests
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

        [Test]
        public void ConstructionEntersAttractQuietly()
        {
            Assert.AreEqual(GameState.Attract, Current.State);
            Assert.IsEmpty(_rig.Events);
            Assert.AreEqual(1, _rig.Throws.DisarmCount);
            Assert.AreEqual(1, _rig.Sim.SetBumpersCount);
            Assert.IsFalse(_rig.Sim.LastBumpers);
            Assert.AreEqual(PinMask.All, _rig.Sim.LastRack);
            Assert.AreEqual(0, Current.Players.Count);
        }

        [Test]
        public void ConstructionWhileRollingLeavesTheSimulatorAlone()
        {
            GameplayRig rolling = null;
            try
            {
                Assert.DoesNotThrow(() => rolling = new GameplayRig(rollingAtStart: true));
                Assert.AreEqual(GameState.Attract, rolling.Service.Current.State);
                Assert.AreEqual(0, rolling.Sim.SetBumpersCount);
                Assert.AreEqual(0, rolling.Sim.SetRackCount);
                Assert.AreEqual(1, rolling.Throws.DisarmCount);
            }
            finally
            {
                rolling?.Dispose();
            }
        }

        [TestCase("input")]
        [TestCase("calibrator")]
        [TestCase("throwSource")]
        [TestCase("rollSimulator")]
        [TestCase("leaderboard")]
        [TestCase("settings")]
        [TestCase("clock")]
        [TestCase("log")]
        [TestCase("utcNow")]
        [TestCase("tuning")]
        [TestCase("difficulties")]
        public void NullDependenciesThrow(string parameter)
        {
            T Arg<T>(string name, T value) where T : class => name == parameter ? null : value;
            Func<DateTime> utcNow = () => GameplayRig.UtcNow;

            var thrown = Assert.Throws<ArgumentNullException>(() => new GameplayService(
                Arg<IPlayerInput>("input", _rig.Input),
                Arg<IPlayerCalibrator>("calibrator", _rig.Calibrator),
                Arg<IThrowSource>("throwSource", _rig.Throws),
                Arg<IRollSimulator>("rollSimulator", _rig.Sim),
                Arg("leaderboard", _rig.Leaderboard),
                Arg<ISettingsStore>("settings", _rig.Settings),
                Arg<IClock>("clock", _rig.Clock),
                Arg<IGameLog>("log", _rig.Log),
                Arg("utcNow", utcNow),
                Arg("tuning", _rig.Tuning),
                Arg("difficulties", _rig.Difficulties)));

            Assert.AreEqual(parameter, thrown.ParamName);
        }

        [Test]
        public void PresenceStartsJoin()
        {
            _rig.Input.SetPresent(true);
            _rig.Service.Tick();

            Assert.That(_rig.Events, Is.EqualTo(new[] { "State:Join", "Snapshot:Join" }));
        }

        [Test]
        public void JoinGreetsForOneSecond()
        {
            _rig.Input.SetPresent(true);
            _rig.Service.Tick();
            Assert.AreEqual(GameState.Join, Current.State);

            _rig.Wait(0.75);
            Assert.AreEqual(GameState.Join, Current.State);

            _rig.Wait(0.25);
            Assert.AreEqual(GameState.ModeSelect, Current.State);
        }

        [Test]
        public void JoinWithoutPresenceReturnsToAttract()
        {
            _rig.Input.SetPresent(true);
            _rig.Service.Tick();
            Assert.AreEqual(GameState.Join, Current.State);

            _rig.Input.SetPresent(false);
            _rig.Service.Tick();

            Assert.AreEqual(GameState.Attract, Current.State);
        }

        [Test]
        public void ModeSelectPresetsFromSettings()
        {
            _rig.Settings.Save(new OperatorSettings { DefaultDifficulty = Difficulty.Pro });

            _rig.Join();

            Assert.AreEqual(GameState.ModeSelect, Current.State);
            Assert.AreEqual(3, Current.FrameCount);
            Assert.AreEqual(Difficulty.Pro, Current.Difficulty.Level);
            Assert.AreEqual(0f, Current.Difficulty.Assist);
            Assert.IsFalse(Current.Difficulty.Bumpers);
            Assert.AreEqual(0, Current.Players.Count);
        }

        [Test]
        public void SelectModeStartsCalibration()
        {
            _rig.StartGame(2, 5, Difficulty.Standard);

            Assert.AreEqual(GameState.Calibrate, Current.State);
            Assert.AreEqual(2, Current.Players.Count);
            Assert.AreEqual("Player 1", Current.Players[0].DisplayName);
            Assert.AreEqual("Player 2", Current.Players[1].DisplayName);
            Assert.AreEqual(5, Current.FrameCount);
            Assert.AreEqual(Difficulty.Standard, Current.Difficulty.Level);
            Assert.AreEqual(1, _rig.Calibrator.BeginCount);
        }

        [Test]
        public void ModeSelectAutoStartsAfterTwentySeconds()
        {
            _rig.Join();

            _rig.Wait(19.75);
            Assert.AreEqual(GameState.ModeSelect, Current.State);

            _rig.Wait(0.25);
            Assert.AreEqual(GameState.Calibrate, Current.State);
            Assert.AreEqual(1, Current.Players.Count);
            Assert.AreEqual(3, Current.FrameCount);
            Assert.AreEqual(Difficulty.Casual, Current.Difficulty.Level);
        }

        [Test]
        public void AutoStartWaitsForThePlayer()
        {
            _rig.Join();
            _rig.Wait(16);
            _rig.Input.SetPresent(false);
            _rig.Wait(4);
            Assert.AreEqual(GameState.ModeSelect, Current.State, "20 s have passed, but the player is absent");

            _rig.Input.SetPresent(true);
            _rig.Service.Tick();

            Assert.AreEqual(GameState.Calibrate, Current.State);
        }

        [Test]
        public void ModeSelectAbsentFiveSecondsReturnsToAttract()
        {
            _rig.Join();
            _rig.Input.SetPresent(false);

            _rig.Wait(4.75);
            Assert.AreEqual(GameState.ModeSelect, Current.State);

            _rig.Wait(0.25);
            Assert.AreEqual(GameState.Attract, Current.State);
        }

        [TestCase(0, 3, Difficulty.Casual)]
        [TestCase(5, 3, Difficulty.Casual)]
        [TestCase(1, 4, Difficulty.Casual)]
        [TestCase(1, 3, (Difficulty)7)]
        public void SelectModeRejectsBadArguments(int players, int frames, Difficulty difficulty)
        {
            Assert.AreEqual(GameState.Attract, Current.State);

            Assert.Throws<ArgumentOutOfRangeException>(() => _rig.Service.SelectMode(players, frames, difficulty));
        }

        [Test]
        public void SelectModeOutsideModeSelectIsIgnored()
        {
            _rig.Input.SetPresent(true);
            _rig.Service.Tick();
            int eventsBefore = _rig.Events.Count;

            _rig.Service.SelectMode(1, 3, Difficulty.Casual);

            Assert.AreEqual(GameState.Join, Current.State);
            Assert.AreEqual(eventsBefore, _rig.Events.Count);
            Assert.That(_rig.Log.Entries, Has.Exactly(1).Matches<GameLogEntry>(e =>
                e.Level == GameLogLevel.Info && e.Category == "Gameplay" && e.Message == "SelectMode ignored in Join"));
        }

        [Test]
        public void EveryHandlerSeesTheNewCurrent()
        {
            _rig.StartGame();

            Assert.IsNotEmpty(_rig.Events);
            Assert.IsFalse(_rig.Events.Exists(e => e.Contains("stale")), string.Join(", ", _rig.Events));
        }
    }
}
