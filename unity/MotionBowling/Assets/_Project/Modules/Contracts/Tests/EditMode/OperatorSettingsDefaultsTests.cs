using NUnit.Framework;

namespace Bowling.Contracts.Tests
{
    public class OperatorSettingsDefaultsTests
    {
        [Test]
        public void DefaultsMatchSpec()
        {
            var settings = new OperatorSettings();

            Assert.AreEqual(1, settings.SchemaVersion);
            Assert.IsNull(settings.CameraUniqueId);
            Assert.AreEqual(Difficulty.Casual, settings.DefaultDifficulty);
            Assert.AreEqual(0.8f, settings.MasterVolume);
            Assert.IsFalse(settings.PoseRecordingEnabled);
        }
    }
}
