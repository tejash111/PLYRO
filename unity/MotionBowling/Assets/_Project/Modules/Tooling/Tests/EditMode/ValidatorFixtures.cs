using System.IO;
using Bowling.Tooling.Editor;
using UnityEngine;

namespace Bowling.Tooling.Tests
{
    /// <summary>
    /// Where the validator fixtures live, and how a rule test points a <see cref="ValidationContext"/> at one.
    /// The fixtures are plain files under <c>Fixtures~</c> (the <c>~</c> keeps Unity from importing or compiling
    /// them), so the rules read them with ordinary file IO. A case folder holds a <c>Modules/</c> folder. Every
    /// M-1 case also holds its own <c>module-rules.json</c>; all M-2 cases share <c>M-2/module-rules.json</c>.
    /// </summary>
    internal static class ValidatorFixtures
    {
        public static string Root => Path.GetFullPath(Path.Combine(
            Application.dataPath, "_Project", "Modules", "Tooling", "Tests", "EditMode", "Fixtures~"));

        /// <summary>An M-1 case: <c>pass</c> or <c>fail/&lt;name&gt;</c>.</summary>
        public static ValidationContext M1(string caseName)
        {
            string caseDir = Path.Combine(Root, "M-1", caseName);
            return Context(Path.Combine(caseDir, "module-rules.json"), caseDir);
        }

        /// <summary>An M-2 case: <c>pass</c> or <c>fail/&lt;name&gt;</c>.</summary>
        public static ValidationContext M2(string caseName)
        {
            return Context(Path.Combine(Root, "M-2", "module-rules.json"), Path.Combine(Root, "M-2", caseName));
        }

        public static ValidationContext Context(string rulesFile, string caseDir)
        {
            return new ValidationContext
            {
                RepoRoot = caseDir,
                ProjectRoot = caseDir,
                ModulesRoot = Path.Combine(caseDir, "Modules"),
                AssetsRoot = caseDir,
                Rules = ModuleRules.Load(rulesFile),
            };
        }
    }
}
