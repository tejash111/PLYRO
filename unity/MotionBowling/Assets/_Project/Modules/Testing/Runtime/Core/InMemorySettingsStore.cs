using System;
using Bowling.Contracts;

namespace Bowling.Testing
{
    // An ISettingsStore that keeps one OperatorSettings in memory. Save stores a copy and Load returns a
    // new copy each call, so editing an object never changes what is stored.
    // Load returns defaults (a new OperatorSettings) until something is saved.
    public sealed class InMemorySettingsStore : ISettingsStore
    {
        private OperatorSettings _saved;

        public OperatorSettings Load()
        {
            return _saved == null ? new OperatorSettings() : Copy(_saved);
        }

        public void Save(OperatorSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _saved = Copy(settings);
        }

        private static OperatorSettings Copy(OperatorSettings settings)
        {
            return new OperatorSettings
            {
                SchemaVersion = settings.SchemaVersion,
                CameraUniqueId = settings.CameraUniqueId,
                DefaultDifficulty = settings.DefaultDifficulty,
                MasterVolume = settings.MasterVolume,
                PoseRecordingEnabled = settings.PoseRecordingEnabled,
            };
        }
    }
}
