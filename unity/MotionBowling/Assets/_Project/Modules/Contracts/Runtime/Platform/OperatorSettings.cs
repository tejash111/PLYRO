namespace Bowling.Contracts
{
    public sealed class OperatorSettings
    {
        public int SchemaVersion = 1;
        public string CameraUniqueId;                    // null = first available camera
        public Difficulty DefaultDifficulty = Difficulty.Casual;
        public float MasterVolume = 0.8f;                // 0..1
        public bool PoseRecordingEnabled;                // default false
    }
}
