using Bowling.Contracts;

namespace Bowling.Testing
{
    // An IRecordingIndicator whose state a test sets. Starts off.
    public sealed class FakeRecordingIndicator : IRecordingIndicator
    {
        public bool IsRecording { get; private set; }

        public void Set(bool isRecording)
        {
            IsRecording = isRecording;
        }
    }
}
