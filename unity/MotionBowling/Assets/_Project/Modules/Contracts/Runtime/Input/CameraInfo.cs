namespace Bowling.Contracts
{
    public readonly struct CameraInfo
    {
        public readonly string UniqueId;
        public readonly string Name;
        public readonly bool IsContinuityCamera;
        public readonly int MaxFps;

        public CameraInfo(string uniqueId, string name, bool isContinuityCamera, int maxFps)
        {
            UniqueId = uniqueId;
            Name = name;
            IsContinuityCamera = isContinuityCamera;
            MaxFps = maxFps;
        }
    }
}
