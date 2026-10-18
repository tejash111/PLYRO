using System;

namespace Bowling.Contracts
{
    public interface IThrowSource                      // implemented by ThrowDetection (and test fakes)
    {
        SwingPhase Phase { get; }
        void Arm(in ThrowContext context);
        void Disarm();
        event Action<SwingPhase> PhaseChanged;
        event Action<ThrowEvent> Thrown;               // fires once, then source returns to Disarmed
        event Action<ThrowRejection> Rejected;         // swing aborted; source returns to WaitingForReady
    }
}
