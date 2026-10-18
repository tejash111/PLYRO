using System;

namespace Bowling.Testing
{
    // One call to an IGameLog method, as RecordingGameLog captured it.
    public readonly struct GameLogEntry
    {
        public readonly GameLogLevel Level;
        public readonly string Category;
        public readonly string Message;
        public readonly Exception Exception;    // null unless an Error call passed one

        public GameLogEntry(GameLogLevel level, string category, string message, Exception exception)
        {
            Level = level;
            Category = category;
            Message = message;
            Exception = exception;
        }
    }
}
