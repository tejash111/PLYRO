using Bowling.Contracts;

namespace Bowling.Gameplay
{
    /// <summary>
    /// The on-enter effects of the states on the throw source and the simulator (spec section 4). It makes no
    /// transition and holds no game state: the state machine calls it while entering a state.
    /// </summary>
    internal sealed class StateEntryActions
    {
        private readonly IThrowSource _throwSource;
        private readonly IRollSimulator _sim;
        private readonly GameplayTuning _tuning;

        public StateEntryActions(IThrowSource throwSource, IRollSimulator rollSimulator, GameplayTuning tuning)
        {
            _throwSource = throwSource;
            _sim = rollSimulator;
            _tuning = tuning;
        }

        /// <summary>
        /// Disarms, then the Attract guard: <c>SetBumpers</c> and <c>SetRack</c> both throw while a roll is in
        /// progress, so they run only when the simulator is idle.
        /// </summary>
        public void EnterAttract()
        {
            _throwSource.Disarm();
            if (!_sim.IsRolling)
            {
                _sim.SetBumpers(false);
                _sim.SetRack(PinMask.All);
            }
        }

        /// <summary>
        /// The bumpers and rack for this ball, then the throw source armed with the current player's profile (or the
        /// default profile).
        /// </summary>
        public void EnterAwaitReady(GameSession session)
        {
            _sim.SetBumpers(session.Difficulty.Bumpers);
            _sim.SetRack(session.Rack);
            _throwSource.Arm(new ThrowContext(session.Current.Profile ?? DefaultProfiles.For(_tuning), session.Difficulty));
        }
    }
}
