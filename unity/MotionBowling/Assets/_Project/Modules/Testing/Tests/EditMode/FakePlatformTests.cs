using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Bowling.Contracts;
using NUnit.Framework;

namespace Bowling.Testing.Tests
{
    // Pinned semantics of the platform fakes: InMemorySettingsStore, RecordingGameLog, FakeOperatorActions
    // and TestDataPaths. The leaderboard store has its own test file.
    public class FakePlatformTests
    {
        private static OperatorSettings CustomSettings()
        {
            return new OperatorSettings
            {
                SchemaVersion = 7,
                CameraUniqueId = "cam-42",
                DefaultDifficulty = Difficulty.Pro,
                MasterVolume = 0.25f,
                PoseRecordingEnabled = true,
            };
        }

        private static void AssertSameValues(OperatorSettings expected, OperatorSettings actual)
        {
            Assert.AreEqual(expected.SchemaVersion, actual.SchemaVersion);
            Assert.AreEqual(expected.CameraUniqueId, actual.CameraUniqueId);
            Assert.AreEqual(expected.DefaultDifficulty, actual.DefaultDifficulty);
            Assert.AreEqual(expected.MasterVolume, actual.MasterVolume);
            Assert.AreEqual(expected.PoseRecordingEnabled, actual.PoseRecordingEnabled);
        }

        // ---- InMemorySettingsStore ----

        [Test]
        public void SettingsLoadWithNothingSavedReturnsDefaults()
        {
            var store = new InMemorySettingsStore();

            AssertSameValues(new OperatorSettings(), store.Load());
        }

        [Test]
        public void SettingsLoadWithNothingSavedReturnsAFreshObjectEachTime()
        {
            var store = new InMemorySettingsStore();

            OperatorSettings first = store.Load();
            first.MasterVolume = 0.1f;

            Assert.AreNotSame(first, store.Load());
            Assert.AreEqual(0.8f, store.Load().MasterVolume);
        }

        [Test]
        public void SettingsLoadReturnsACopyOfWhatWasSaved()
        {
            var store = new InMemorySettingsStore();
            OperatorSettings saved = CustomSettings();
            store.Save(saved);

            OperatorSettings loaded = store.Load();

            Assert.AreNotSame(saved, loaded);
            AssertSameValues(saved, loaded);
        }

        [Test]
        public void SettingsSaveStoresACopySoLaterEditsDoNotLeakIn()
        {
            var store = new InMemorySettingsStore();
            OperatorSettings saved = CustomSettings();
            store.Save(saved);

            saved.MasterVolume = 0.99f;
            saved.CameraUniqueId = "edited";

            OperatorSettings loaded = store.Load();
            Assert.AreEqual(0.25f, loaded.MasterVolume);
            Assert.AreEqual("cam-42", loaded.CameraUniqueId);
        }

        [Test]
        public void SettingsLoadedCopiesAreIndependentOfTheStoreAndEachOther()
        {
            var store = new InMemorySettingsStore();
            store.Save(CustomSettings());

            OperatorSettings first = store.Load();
            first.DefaultDifficulty = Difficulty.Casual;
            OperatorSettings second = store.Load();

            Assert.AreNotSame(first, second);
            Assert.AreEqual(Difficulty.Pro, second.DefaultDifficulty);
        }

        [Test]
        public void SettingsSaveOverwritesThePreviousSave()
        {
            var store = new InMemorySettingsStore();
            store.Save(CustomSettings());
            store.Save(new OperatorSettings { MasterVolume = 0.5f });

            Assert.AreEqual(0.5f, store.Load().MasterVolume);
            Assert.IsNull(store.Load().CameraUniqueId);
        }

        [Test]
        public void SettingsSaveRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new InMemorySettingsStore().Save(null));
        }

        [Test]
        public void OperatorSettingsStillHasTheFiveFieldsTheStoreCopies()
        {
            // InMemorySettingsStore copies OperatorSettings field by field. If this fails, a field was
            // added: update the copy in the store and the assertions above.
            FieldInfo[] fields = typeof(OperatorSettings).GetFields(BindingFlags.Public | BindingFlags.Instance);

            Assert.AreEqual(5, fields.Length);
        }

        // ---- RecordingGameLog ----

        [Test]
        public void LogStartsEmpty()
        {
            Assert.IsEmpty(new RecordingGameLog().Entries);
        }

        [Test]
        public void LogRecordsInfoWarnAndErrorInOrder()
        {
            var log = new RecordingGameLog();
            var failure = new InvalidOperationException("boom");

            log.Info("Tracking", "started");
            log.Warn("Camera", "slow");
            log.Error("Tracking", "lost", failure);
            log.Error("App", "no exception");

            Assert.AreEqual(4, log.Entries.Count);
            AssertEntry(log.Entries[0], GameLogLevel.Info, "Tracking", "started", null);
            AssertEntry(log.Entries[1], GameLogLevel.Warn, "Camera", "slow", null);
            AssertEntry(log.Entries[2], GameLogLevel.Error, "Tracking", "lost", failure);
            AssertEntry(log.Entries[3], GameLogLevel.Error, "App", "no exception", null);
        }

        private static void AssertEntry(GameLogEntry entry, GameLogLevel level, string category, string message, Exception exception)
        {
            Assert.AreEqual(level, entry.Level);
            Assert.AreEqual(category, entry.Category);
            Assert.AreEqual(message, entry.Message);
            Assert.AreSame(exception, entry.Exception);
        }

        // ---- FakeOperatorActions ----

        [Test]
        public void OperatorActionsStartWithDefaultSettingsAndNoCalls()
        {
            var actions = new FakeOperatorActions();

            AssertSameValues(new OperatorSettings(), actions.Settings);
            Assert.IsEmpty(actions.Calls);
        }

        [Test]
        public void OperatorActionsApplySettingsStoresThemThenRaisesSettingsApplied()
        {
            var actions = new FakeOperatorActions();
            var events = new List<string>();
            actions.SettingsApplied += s =>
                events.Add("SettingsApplied:" + s.MasterVolume + ":" + ReferenceEquals(s, actions.Settings));
            OperatorSettings settings = CustomSettings();

            actions.ApplySettings(settings);

            Assert.AreSame(settings, actions.Settings);
            Assert.That(events, Is.EqualTo(new[] { "SettingsApplied:0.25:True" }),
                "Settings already holds the applied object inside the handler");
        }

        [Test]
        public void OperatorActionsRecordEveryCallInOrder()
        {
            var actions = new FakeOperatorActions();

            actions.ResetLeaderboard();
            actions.ApplySettings(new OperatorSettings());
            actions.Quit();
            actions.ResetLeaderboard();

            Assert.That(actions.Calls, Is.EqualTo(new[] { "ResetLeaderboard", "ApplySettings", "Quit", "ResetLeaderboard" }));
        }

        [Test]
        public void OperatorActionsRaiseNothingForResetAndQuit()
        {
            var actions = new FakeOperatorActions();
            int raised = 0;
            actions.SettingsApplied += s => raised++;

            actions.ResetLeaderboard();
            actions.Quit();

            Assert.AreEqual(0, raised);
        }

        [Test]
        public void OperatorActionsApplySettingsRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new FakeOperatorActions().ApplySettings(null));
        }

        // ---- TestDataPaths ----

        [Test]
        public void PoseRecordingsPathEndsWithTheTestDataFolder()
        {
            string path = TestDataPaths.PoseRecordings.Replace('\\', '/');

            StringAssert.EndsWith("test-data/pose-recordings", path);
            Assert.IsTrue(Path.IsPathRooted(TestDataPaths.PoseRecordings));
        }

        [Test]
        public void PoseRecordingsPathResolvesToTheRepositoryRoot()
        {
            string repoRoot = Path.GetDirectoryName(Path.GetDirectoryName(TestDataPaths.PoseRecordings));

            Assert.IsTrue(File.Exists(Path.Combine(repoRoot, "CLAUDE.md")), "expected CLAUDE.md in " + repoRoot);
        }

        [Test]
        public void PoseRecordingsFolderExistsInTheRepository()
        {
            Assert.IsTrue(Directory.Exists(TestDataPaths.PoseRecordings), TestDataPaths.PoseRecordings);
        }
    }
}
