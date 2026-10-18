using Bowling.Contracts;
using UnityEngine;

namespace Bowling.App
{
    /// <summary>
    /// The only composition root. <c>Awake</c> constructs the plain C# services; later sub-projects add the module
    /// wiring here as their final task. The literal order is checked against <see cref="ExecutionOrder.Bootstrap"/>.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        public IClock Clock { get; private set; }
        public IGameLog Log { get; private set; }

        void Awake()
        {
            Clock = new UnityClock();
            Log = new UnityConsoleGameLog();
            Log.Info("App", "Boot");
        }
    }
}
