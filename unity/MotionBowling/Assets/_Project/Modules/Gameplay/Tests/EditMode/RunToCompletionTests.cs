using Bowling.Contracts;
using Bowling.Testing;
using NUnit.Framework;

namespace Bowling.Gameplay.Tests
{
    /// <summary>
    /// The run-to-completion gate (P-7): a command that a subscriber calls from inside a transition is queued and runs
    /// after that transition has raised all of its events, and queued calls run in call order.
    /// </summary>
    public sealed class RunToCompletionTests
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

        [Test]
        public void ACommandFromAHandlerRunsAfterTheTransition()
        {
            _rig.Service.StateChanged += s =>
            {
                if (s == GameState.ModeSelect)
                {
                    _rig.Service.SelectMode(1, 3, Difficulty.Casual);
                }
            };

            _rig.Join();

            Assert.That(_rig.Events, Is.EqualTo(new[]
            {
                "State:Join", "Snapshot:Join",
                "State:ModeSelect", "Snapshot:ModeSelect",
                "State:Calibrate", "Snapshot:Calibrate",
            }));
        }

        [Test]
        public void QueuedCallsRunInCallOrder()
        {
            _rig.Service.StateChanged += s =>
            {
                if (s == GameState.ModeSelect)
                {
                    _rig.Service.SelectMode(1, 3, Difficulty.Casual);
                    _rig.Service.SelectMode(1, 3, Difficulty.Casual);
                }
            };

            _rig.Join();

            Assert.AreEqual(GameState.Calibrate, _rig.Service.Current.State);
            Assert.That(_rig.Log.Entries, Has.Exactly(1).Matches<GameLogEntry>(e =>
                e.Level == GameLogLevel.Info && e.Category == "Gameplay" && e.Message == "SelectMode ignored in Calibrate"));
        }
    }
}
