// public class Loud { }
namespace Bowling.Gameplay
{
    /* public class AlsoLoud { */
    [System.Serializable]
    public sealed class Quiet
    {
        private const string Open = "{";            // a brace in a string, and one in a comment {
        private const char Close = '}';
        private const string Text = @"public class InAString { ";
        private const string Plain = "public class Trap";
    }
}
