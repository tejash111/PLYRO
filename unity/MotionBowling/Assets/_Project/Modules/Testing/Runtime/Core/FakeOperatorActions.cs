using System;
using System.Collections.Generic;
using Bowling.Contracts;

namespace Bowling.Testing
{
    // An IOperatorActions that records its calls in Calls ("ApplySettings", "ResetLeaderboard", "Quit"),
    // in call order. Settings starts as defaults; ApplySettings keeps the object it is given and raises
    // SettingsApplied after Settings holds it. ResetLeaderboard and Quit raise nothing.
    public sealed class FakeOperatorActions : IOperatorActions
    {
        private readonly List<string> _calls = new List<string>();

        public OperatorSettings Settings { get; private set; } = new OperatorSettings();

        public IReadOnlyList<string> Calls => _calls;

        public event Action<OperatorSettings> SettingsApplied;

        public void ApplySettings(OperatorSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _calls.Add("ApplySettings");
            Settings = settings;
            SettingsApplied?.Invoke(settings);
        }

        public void ResetLeaderboard()
        {
            _calls.Add("ResetLeaderboard");
        }

        public void Quit()
        {
            _calls.Add("Quit");
        }
    }
}
