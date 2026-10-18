namespace Bowling.Tooling.Editor
{
    /// <summary>
    /// One finding of a validation rule. Printed as <c>&lt;rule-id&gt; &lt;path&gt;[:&lt;line&gt;] &lt;message&gt;</c>,
    /// the same line format the text checks in <c>tools/checks/check_sources_of_truth.sh</c> use.
    /// A warning is reported but does not fail a batch run.
    /// </summary>
    public readonly struct Violation
    {
        public readonly string RuleId;

        /// <summary>The file or folder at fault, with forward slashes.</summary>
        public readonly string Path;

        /// <summary>1-based line in <see cref="Path"/>, or 0 when the finding has no line.</summary>
        public readonly int Line;

        public readonly string Message;

        public readonly bool IsWarning;

        public Violation(string ruleId, string path, int line, string message, bool isWarning)
        {
            RuleId = ruleId;
            Path = path;
            Line = line;
            Message = message;
            IsWarning = isWarning;
        }

        /// <summary><c>&lt;id&gt; &lt;path&gt;[:&lt;line&gt;] &lt;message&gt;</c>, with the line left out when it is 0.</summary>
        public override string ToString()
        {
            return Line > 0 ? $"{RuleId} {Path}:{Line} {Message}" : $"{RuleId} {Path} {Message}";
        }
    }
}
