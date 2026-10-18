namespace Bowling.Contracts
{
    public readonly struct TrackingStats
    {
        public readonly float CaptureFps;
        public readonly float InferenceFps;
        public readonly float InferenceMsP50;
        public readonly float InferenceMsP95;
        public readonly long FramesDroppedStale;
        public readonly long SamplesOverwritten;

        public TrackingStats(
            float captureFps,
            float inferenceFps,
            float inferenceMsP50,
            float inferenceMsP95,
            long framesDroppedStale,
            long samplesOverwritten)
        {
            CaptureFps = captureFps;
            InferenceFps = inferenceFps;
            InferenceMsP50 = inferenceMsP50;
            InferenceMsP95 = inferenceMsP95;
            FramesDroppedStale = framesDroppedStale;
            SamplesOverwritten = samplesOverwritten;
        }
    }
}
