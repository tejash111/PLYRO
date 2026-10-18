namespace Bowling.Contracts
{
    public interface IRecordingIndicator               // implemented by PoseInput's recorder; the UI shows a "Recording" badge
    {
        bool IsRecording { get; }
    }
}
