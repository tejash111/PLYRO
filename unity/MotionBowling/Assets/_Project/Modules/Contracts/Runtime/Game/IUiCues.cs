using System;

namespace Bowling.Contracts
{
    public interface IUiCues                             // implemented by UI; consumed by Presentation audio
    {
        event Action<UiCue> Cue;
    }
}
