namespace Bowling.App
{
    /// <summary>
    /// The update order of the module drivers (foundation spec, section 7). Modules cannot reference App, so each
    /// driver writes the literal number in its own <c>[DefaultExecutionOrder]</c>; ExecutionOrderTests checks that
    /// the literal equals the constant named after the module. Constants only: no state.
    /// </summary>
    public static class ExecutionOrder
    {
        public const int Bootstrap = -1000;
        public const int PoseInput = -300;
        public const int ThrowDetection = -200;
        public const int Gameplay = -100;
        public const int Presentation = 0;
        public const int UI = 100;
    }
}
