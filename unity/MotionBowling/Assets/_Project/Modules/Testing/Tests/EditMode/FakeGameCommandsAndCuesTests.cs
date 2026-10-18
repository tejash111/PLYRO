using System.Collections.Generic;
using Bowling.Contracts;
using NUnit.Framework;

namespace Bowling.Testing.Tests
{
    // Pinned semantics of FakeGameCommands and FakeUiCues.
    public class FakeGameCommandsAndCuesTests
    {
        // ---- FakeGameCommands ----

        [Test]
        public void CommandsStartWithNoCalls()
        {
            var commands = new FakeGameCommands();

            Assert.IsEmpty(commands.Calls);
            Assert.IsEmpty(commands.SelectModeCalls);
        }

        [Test]
        public void CommandsRecordEveryCallWithItsArgumentsInOrder()
        {
            var commands = new FakeGameCommands();

            commands.SelectMode(2, 5, Difficulty.Pro);
            commands.Pause();
            commands.Resume();
            commands.Abandon();
            commands.SelectMode(1, 3, Difficulty.Casual);

            Assert.That(commands.Calls, Is.EqualTo(new[]
            {
                "SelectMode(2, 5, Pro)",
                "Pause()",
                "Resume()",
                "Abandon()",
                "SelectMode(1, 3, Casual)",
            }));
        }

        [Test]
        public void CommandsKeepTypedSelectModeArguments()
        {
            var commands = new FakeGameCommands();

            commands.SelectMode(4, 10, Difficulty.Standard);
            commands.Pause();
            commands.SelectMode(1, 3, Difficulty.Casual);

            Assert.AreEqual(2, commands.SelectModeCalls.Count);
            Assert.AreEqual(4, commands.SelectModeCalls[0].PlayerCount);
            Assert.AreEqual(10, commands.SelectModeCalls[0].FrameCount);
            Assert.AreEqual(Difficulty.Standard, commands.SelectModeCalls[0].Difficulty);
            Assert.AreEqual(1, commands.SelectModeCalls[1].PlayerCount);
            Assert.AreEqual(3, commands.SelectModeCalls[1].FrameCount);
            Assert.AreEqual(Difficulty.Casual, commands.SelectModeCalls[1].Difficulty);
        }

        // ---- FakeUiCues ----

        [Test]
        public void CuesRaiseDeliversEachCueInOrder()
        {
            var cues = new FakeUiCues();
            var received = new List<UiCue>();
            cues.Cue += received.Add;

            cues.Raise(UiCue.DwellTick);
            cues.Raise(UiCue.Confirm);
            cues.Raise(UiCue.DwellTick);
            cues.Raise(UiCue.Toast);

            Assert.That(received, Is.EqualTo(new[] { UiCue.DwellTick, UiCue.Confirm, UiCue.DwellTick, UiCue.Toast }));
        }

        [Test]
        public void CuesRaiseWithoutSubscribersDoesNothing()
        {
            var cues = new FakeUiCues();

            Assert.DoesNotThrow(() => cues.Raise(UiCue.Back));
        }
    }
}
