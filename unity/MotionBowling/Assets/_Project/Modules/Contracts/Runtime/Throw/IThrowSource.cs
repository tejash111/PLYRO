using System;

namespace Bowling.Contracts
{
    public interface IThrowSource                      // implemented by ThrowDetection (and test fakes)
    {
        SwingPhase Phase { get; }
        void Arm(in ThrowContext context);
        void Disarm();
        event Action<SwingPhase> PhaseChanged;
        event Action<ThrowEvent> Thrown;               // fires once; the source is already Disarmed when it fires
        event Action<ThrowRejection> Rejected;         // swing aborted; the source is already back in WaitingForReady when it fires
    }
}
