using Bowling.Contracts;

namespace Bowling.Gameplay
{
    /// <summary>
    /// The bookkeeping of one pause (P-5, P-6): the state to resume, whether the player has been absent since the pause
    /// began, and an operator <c>Pause()</c> waiting for Rolling or Scoring to end. It makes no transition:
    /// <see cref="PauseFlow"/> reads it and decides.
    /// </summary>
    internal sealed class PauseTracker
    {
        private bool _absenceSeen;      // the player was absent at some point since the last Begin

        /// <summary>The state Paused resumes into. Meaningful only after <see cref="Begin"/>.</summary>
        public GameState ResumeState { get; private set; }

        /// <summary>An operator <c>Pause()</c> waiting for Rolling or Scoring to end (P-6).</summary>
        public bool Deferred { get; set; }

        /// <summary>
        /// A pause begins from <paramref name="from"/>. AwaitThrow resumes as AwaitReady, which re-arms; every other
        /// state resumes as itself. An absent player counts as an absence already. A pause that begins also satisfies
        /// a deferred one, so a direct <c>Pause()</c> in the state after Scoring doesn't pause twice.
        /// </summary>
        public void Begin(GameState from, bool present)
        {
            ResumeState = from == GameState.AwaitThrow ? GameState.AwaitReady : from;
            _absenceSeen = !present;
            Deferred = false;
        }

        /// <summary>Records an absence; true once the player is present after an absence since <see cref="Begin"/>.</summary>
        public bool PlayerReturned(bool present)
        {
            if (!present)
            {
                _absenceSeen = true;
                return false;
            }

            return _absenceSeen;
        }
    }
}
