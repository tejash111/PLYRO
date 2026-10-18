using System.IO;
using UnityEngine;

namespace Bowling.Testing
{
    // Where the repository keeps test data. The path is computed on every read, so nothing is stored.
    public static class TestDataPaths
    {
        // The absolute <repo>/test-data/pose-recordings folder. Application.dataPath is
        // <repo>/unity/MotionBowling/Assets, so the repository root is three levels up.
        // Read it on the main thread, as Application.dataPath requires.
        public static string PoseRecordings =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "test-data", "pose-recordings"));
    }
}
