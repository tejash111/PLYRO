using System;
using System.Collections.Generic;
using Bowling.Contracts;

namespace Bowling.Testing
{
    // An IGameLog that keeps every call in Entries, in call order, and writes nowhere else.
    public sealed class RecordingGameLog : IGameLog
    {
        private readonly List<GameLogEntry> _entries = new List<GameLogEntry>();

        public IReadOnlyList<GameLogEntry> Entries => _entries;

        public void Info(string category, string message)
        {
            _entries.Add(new GameLogEntry(GameLogLevel.Info, category, message, null));
        }

        public void Warn(string category, string message)
        {
            _entries.Add(new GameLogEntry(GameLogLevel.Warn, category, message, null));
        }

        public void Error(string category, string message, Exception exception = null)
        {
            _entries.Add(new GameLogEntry(GameLogLevel.Error, category, message, exception));
        }
    }
}
