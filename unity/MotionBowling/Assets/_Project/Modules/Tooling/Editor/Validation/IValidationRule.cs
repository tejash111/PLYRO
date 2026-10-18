using System.Collections.Generic;

namespace Bowling.Tooling.Editor
{
    /// <summary>
    /// One source-of-truth rule that needs the editor (foundation spec, section 8). A rule keeps no state
    /// between runs and reads everything it needs from the <see cref="ValidationContext"/>.
    /// </summary>
    public interface IValidationRule
    {
        /// <summary>The rule id used in the output, for example <c>M-1</c>.</summary>
        string Id { get; }

        /// <summary>Judges the project. An empty result means the rule passes.</summary>
        IEnumerable<Violation> Run(ValidationContext context);
    }
}
