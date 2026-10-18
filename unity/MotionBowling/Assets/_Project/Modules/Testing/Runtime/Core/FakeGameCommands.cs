using System.Collections.Generic;
using Bowling.Contracts;

namespace Bowling.Testing
{
    // An IGameCommands that does nothing but record. Every call appears in Calls, in call order, as
    // text with its arguments. SelectMode arguments are also kept typed in SelectModeCalls.
    public sealed class FakeGameCommands : IGameCommands
    {
        private readonly List<string> _calls = new List<string>();
        private readonly List<(int PlayerCount, int FrameCount, Difficulty Difficulty)> _selectModeCalls =
            new List<(int PlayerCount, int FrameCount, Difficulty Difficulty)>();

        // For example "SelectMode(2, 5, Pro)", "Pause()", "Resume()", "Abandon()".
        public IReadOnlyList<string> Calls => _calls;

        public IReadOnlyList<(int PlayerCount, int FrameCount, Difficulty Difficulty)> SelectModeCalls => _selectModeCalls;

        public void SelectMode(int playerCount, int frameCount, Difficulty difficulty)
        {
            _calls.Add($"SelectMode({playerCount}, {frameCount}, {difficulty})");
            _selectModeCalls.Add((playerCount, frameCount, difficulty));
        }

        public void Pause()
        {
            _calls.Add("Pause()");
        }

        public void Resume()
        {
            _calls.Add("Resume()");
        }

        public void Abandon()
        {
            _calls.Add("Abandon()");
        }
    }
}
