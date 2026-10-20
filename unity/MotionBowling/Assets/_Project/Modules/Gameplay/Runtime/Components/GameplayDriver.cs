using System;
using UnityEngine;

namespace Bowling.Gameplay
{
    /// <summary>
    /// Ticks the plain C# <see cref="GameplayService"/> once per frame. It holds no logic of its own: App constructs the
    /// service, adds this component to the Bootstrap object and hands the service over through <see cref="Initialize"/>.
    /// The literal order is checked against <c>ExecutionOrder.Gameplay</c> by App's <c>ExecutionOrderTests</c>.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameplayDriver : MonoBehaviour
    {
        private GameplayService _service;

        /// <summary>
        /// Gives the driver the service to tick. Call it once, from App, before the first frame that should tick.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="service"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The driver was already initialized.</exception>
        public void Initialize(GameplayService service)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));
            if (_service != null)
                throw new InvalidOperationException("GameplayDriver is already initialized.");
            _service = service;
        }

        // Does nothing until Initialize has run, so the driver may be added to the scene before the wiring finishes.
        private void Update()
        {
            if (_service != null)
                _service.Tick();
        }
    }
}
