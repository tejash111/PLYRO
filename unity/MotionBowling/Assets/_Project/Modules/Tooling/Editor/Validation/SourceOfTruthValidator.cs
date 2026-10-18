using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Bowling.Tooling.Editor
{
    /// <summary>
    /// Runs the editor-layer source-of-truth rules (foundation spec, section 8). The fast text rules run from
    /// <c>tools/checks/check_sources_of_truth.sh</c>; these need the editor. Run them from the menu
    /// <b>Bowling &gt; Validate Sources of Truth</b>, or headless with
    /// <c>tools/ci/unity.sh exec Bowling.Tooling.Editor.SourceOfTruthValidator.RunBatch</c>.
    /// </summary>
    public static class SourceOfTruthValidator
    {
        /// <summary>The rules, as new instances. Later tasks append their rules here.</summary>
        public static IReadOnlyList<IValidationRule> CreateRules()
        {
            return new IValidationRule[]
            {
                new ModuleStructureRule(),
                new AsmdefReferenceRule(),
                new VolumeProfileRule(),
                new SceneAndPrefabRule(),
                new MainSceneRootRule(),
            };
        }

        /// <summary>Runs every rule of <see cref="CreateRules"/>.</summary>
        public static IReadOnlyList<Violation> RunAll(ValidationContext context) => Run(CreateRules(), context);

        /// <summary>
        /// Runs the given rules in order and collects their violations. A rule that throws doesn't stop the
        /// run: it becomes a violation of that rule, so the failure can't pass for a clean result.
        /// </summary>
        public static IReadOnlyList<Violation> Run(IEnumerable<IValidationRule> rules, ValidationContext context)
        {
            var violations = new List<Violation>();
            foreach (IValidationRule rule in rules)
            {
                try
                {
                    violations.AddRange(rule.Run(context).ToList());
                }
                catch (Exception e)
                {
                    violations.Add(new Violation(rule.Id, "(validator)", 0, $"the rule threw {e.GetType().Name}: {e.Message}", false));
                }
            }
            return violations;
        }

        /// <summary>1 if any violation is not a warning, else 0.</summary>
        public static int ExitCodeFor(IEnumerable<Violation> violations) => violations.Any(v => !v.IsWarning) ? 1 : 0;

        [MenuItem("Bowling/Validate Sources of Truth")]
        public static void RunFromMenu()
        {
            Report(RunAll(ValidationContext.ForProject()));
        }

        /// <summary>
        /// Batch entry point. Prints each violation, then ends the editor with exit code 1 if any is not a
        /// warning, else 0. Whatever goes wrong, the editor still exits: an exception is logged and exits 1.
        /// </summary>
        public static void RunBatch()
        {
            int exitCode;
            try
            {
                IReadOnlyList<Violation> violations = RunAll(ValidationContext.ForProject());
                Report(violations);
                exitCode = ExitCodeFor(violations);
            }
            catch (Exception e)
            {
                Debug.LogError($"SourceOfTruthValidator failed to run: {e}");
                exitCode = 1;
            }
            EditorApplication.Exit(exitCode);
        }

        /// <summary>
        /// One log line per violation (warnings start with <c>WARN </c>, as in the text checks), then a summary.
        /// Spec section 8 says one line per violation, so none of them carries a stack trace.
        /// </summary>
        static void Report(IReadOnlyList<Violation> violations)
        {
            foreach (Violation violation in violations)
                LogLine(violation.IsWarning ? $"WARN {violation}" : violation.ToString());
            int errors = violations.Count(v => !v.IsWarning);
            LogLine($"Source-of-truth validation: {errors} violation(s), {violations.Count - errors} warning(s).");
        }

        static void LogLine(string text) => Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}", text);
    }
}
