using System;
using Bowling.Contracts;

namespace Bowling.Testing
{
    // An IUiCues a test fires by hand.
    public sealed class FakeUiCues : IUiCues
    {
        public event Action<UiCue> Cue;

        public void Raise(UiCue cue)
        {
            Cue?.Invoke(cue);
        }
    }
}
