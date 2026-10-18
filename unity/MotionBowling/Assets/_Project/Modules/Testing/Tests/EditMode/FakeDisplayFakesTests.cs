using System;
using Bowling.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace Bowling.Testing.Tests
{
    // The display-side input fakes only expose what a test sets: FakeHandCursor, FakeCameraPreview,
    // FakeTrackingDiagnostics, FakeRecordingIndicator.
    public class FakeDisplayFakesTests
    {
        // ---- FakeHandCursor ----

        [Test]
        public void HandCursorStartsInactiveAtTheOrigin()
        {
            IHandCursor cursor = new FakeHandCursor();

            Assert.IsFalse(cursor.IsActive);
            Assert.AreEqual(Vector2.zero, cursor.Position);
        }

        [Test]
        public void HandCursorSetStoresActivityAndPosition()
        {
            var cursor = new FakeHandCursor();

            cursor.Set(true, new Vector2(0.25f, 0.75f));
            Assert.IsTrue(cursor.IsActive);
            Assert.AreEqual(new Vector2(0.25f, 0.75f), cursor.Position);

            cursor.Set(false, new Vector2(0.5f, 0.5f));
            Assert.IsFalse(cursor.IsActive);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), cursor.Position);
        }

        // ---- FakeCameraPreview ----

        [Test]
        public void CameraPreviewStartsWithoutATexture()
        {
            ICameraPreview preview = new FakeCameraPreview();

            Assert.IsNull(preview.Texture);
        }

        [Test]
        public void CameraPreviewSetStoresTheTextureAndNullClearsIt()
        {
            var preview = new FakeCameraPreview();
            var texture = new Texture2D(2, 2);
            try
            {
                preview.Set(texture);
                Assert.AreSame(texture, preview.Texture);

                preview.Set(null);
                Assert.IsNull(preview.Texture);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        // ---- FakeTrackingDiagnostics ----

        [Test]
        public void DiagnosticsStartWithZeroStatsAndNoCameras()
        {
            ITrackingDiagnostics diagnostics = new FakeTrackingDiagnostics();

            Assert.AreEqual(0f, diagnostics.Stats.CaptureFps);
            Assert.AreEqual(0L, diagnostics.Stats.FramesDroppedStale);
            Assert.IsNotNull(diagnostics.Cameras);
            Assert.AreEqual(0, diagnostics.Cameras.Count);
        }

        [Test]
        public void DiagnosticsSetStoresTheStats()
        {
            var diagnostics = new FakeTrackingDiagnostics();

            diagnostics.Set(new TrackingStats(30f, 28f, 11f, 19f, 4L, 2L));

            Assert.AreEqual(30f, diagnostics.Stats.CaptureFps);
            Assert.AreEqual(28f, diagnostics.Stats.InferenceFps);
            Assert.AreEqual(11f, diagnostics.Stats.InferenceMsP50);
            Assert.AreEqual(19f, diagnostics.Stats.InferenceMsP95);
            Assert.AreEqual(4L, diagnostics.Stats.FramesDroppedStale);
            Assert.AreEqual(2L, diagnostics.Stats.SamplesOverwritten);
        }

        [Test]
        public void DiagnosticsSetCamerasKeepsTheOrderAndCopiesTheArray()
        {
            var diagnostics = new FakeTrackingDiagnostics();
            var cameras = new[]
            {
                new CameraInfo("id-a", "FaceTime HD", false, 30),
                new CameraInfo("id-b", "iPhone Camera", true, 60),
            };

            diagnostics.SetCameras(cameras);
            cameras[0] = new CameraInfo("changed", "changed", false, 1);

            Assert.AreEqual(2, diagnostics.Cameras.Count);
            Assert.AreEqual("id-a", diagnostics.Cameras[0].UniqueId);
            Assert.AreEqual("FaceTime HD", diagnostics.Cameras[0].Name);
            Assert.IsFalse(diagnostics.Cameras[0].IsContinuityCamera);
            Assert.AreEqual(30, diagnostics.Cameras[0].MaxFps);
            Assert.AreEqual("id-b", diagnostics.Cameras[1].UniqueId);
            Assert.IsTrue(diagnostics.Cameras[1].IsContinuityCamera);
        }

        [Test]
        public void DiagnosticsSetCamerasWithNoArgumentsClearsTheList()
        {
            var diagnostics = new FakeTrackingDiagnostics();
            diagnostics.SetCameras(new CameraInfo("id-a", "Cam", false, 30));

            diagnostics.SetCameras();

            Assert.AreEqual(0, diagnostics.Cameras.Count);
            Assert.Throws<ArgumentNullException>(() => diagnostics.SetCameras(null));
        }

        // ---- FakeRecordingIndicator ----

        [Test]
        public void RecordingIndicatorStartsOffAndFollowsSet()
        {
            var indicator = new FakeRecordingIndicator();
            Assert.IsFalse(((IRecordingIndicator)indicator).IsRecording);

            indicator.Set(true);
            Assert.IsTrue(indicator.IsRecording);

            indicator.Set(false);
            Assert.IsFalse(indicator.IsRecording);
        }
    }
}
