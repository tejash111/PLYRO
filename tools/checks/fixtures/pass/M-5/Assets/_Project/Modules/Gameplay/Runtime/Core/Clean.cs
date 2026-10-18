namespace Bowling.Gameplay
{
    public class Clean
    {
        // Never call FindObjectOfType here: the wiring is passed in by Bowling.App.
        public int InstanceCount { get; private set; }
        public Clean Instances { get; }
    }
}
