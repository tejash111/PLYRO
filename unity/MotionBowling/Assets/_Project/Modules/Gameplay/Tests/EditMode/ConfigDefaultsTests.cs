using System;
using Bowling.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace Bowling.Gameplay.Tests
{
    /// <summary>The shipped defaults of GameplayTuning (spec section 8), DifficultyCatalog (D9) and DefaultProfiles (P-13).</summary>
    public sealed class ConfigDefaultsTests
    {
        private GameplayTuning _tuning;
        private DifficultyCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _tuning = ScriptableObject.CreateInstance<GameplayTuning>();
            _catalog = ScriptableObject.CreateInstance<DifficultyCatalog>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_tuning);
            UnityEngine.Object.DestroyImmediate(_catalog);
        }

        [Test]
        public void TuningDefaultsMatchTheSpec()
        {
            Assert.AreEqual(1.0f, _tuning.JoinGreeting);
            Assert.AreEqual(20f, _tuning.ModeSelectTimeout);
            Assert.AreEqual(5f, _tuning.ModeSelectAbsent);
            Assert.AreEqual(2f, _tuning.PauseAfterAbsent);
            Assert.AreEqual(30f, _tuning.TurnChangeWait);
            Assert.AreEqual(1.5f, _tuning.ScoringHold);
            Assert.AreEqual(4.5f, _tuning.ScoringHoldReplay);
            Assert.AreEqual(15f, _tuning.AbandonAfterPause);
            Assert.AreEqual(2f, _tuning.GameOverHold);
            Assert.AreEqual(10f, _tuning.ResultsHold);
            Assert.AreEqual(3f, _tuning.ResultsAbsent);
            Assert.AreEqual(1, _tuning.PresetPlayerCount);
            Assert.AreEqual(3, _tuning.PresetFrameCount);
            Assert.AreEqual(Handedness.Right, _tuning.DefaultBowlingHand);
            Assert.AreEqual(0.60f, _tuning.DefaultArmLengthM);
            Assert.AreEqual(0.38f, _tuning.DefaultShoulderWidthM);
            Assert.AreEqual(0.5f, _tuning.DefaultNeutralImageX);
        }

        [TestCase(Difficulty.Casual, 0.50f, true)]
        [TestCase(Difficulty.Standard, 0.25f, false)]
        [TestCase(Difficulty.Pro, 0.00f, false)]
        public void CatalogMatchesD9(Difficulty level, float assist, bool bumpers)
        {
            DifficultyProfile profile = _catalog.For(level);

            Assert.AreEqual(level, profile.Level);
            Assert.AreEqual(assist, profile.Assist);
            Assert.AreEqual(bumpers, profile.Bumpers);
        }

        [Test]
        public void CatalogRejectsAnUndefinedLevel()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _catalog.For((Difficulty)7));
        }

        [Test]
        public void DefaultProfileComesFromTuning()
        {
            _tuning.DefaultBowlingHand = Handedness.Left;
            _tuning.DefaultArmLengthM = 0.7f;
            _tuning.DefaultShoulderWidthM = 0.4f;
            _tuning.DefaultNeutralImageX = 0.45f;

            PlayerProfile profile = DefaultProfiles.For(_tuning);

            Assert.AreEqual(Handedness.Left, profile.BowlingHand);
            Assert.AreEqual(0.7f, profile.ArmLengthM);
            Assert.AreEqual(0.4f, profile.ShoulderWidthM);
            Assert.AreEqual(0.45f, profile.NeutralImageX);
            Assert.Throws<ArgumentNullException>(() => DefaultProfiles.For(null));
        }
    }
}
