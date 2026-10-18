using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Bowling.Tooling.Editor
{
    /// <summary>
    /// M-1: every module folder under <c>Modules/</c> is listed in <c>module-rules.json</c>, and every listed
    /// module has its README (the four template headings) and exactly the asmdefs of foundation spec section 4,
    /// with the <c>name</c>, <c>rootNamespace</c>, <c>includePlatforms</c> and required fields of their kind.
    /// Which asmdefs a module has comes from its flags in <c>module-rules.json</c>. A module folder that is
    /// missing is one violation for the folder, not one per file.
    /// </summary>
    public sealed class ModuleStructureRule : IValidationRule
    {
        const string IncludeTests = "UNITY_INCLUDE_TESTS";
        const string NUnit = "nunit.framework.dll";

        static readonly string[] ReadmeSections = { "Purpose", "Public API", "Dependencies", "Folder map" };
        static readonly Regex HeadingLine = new Regex(@"^ {0,3}(#{1,6})[ \t]+(.*?)(?:[ \t]+#+)?[ \t]*$");
        static readonly Regex FenceLine = new Regex(@"^ {0,3}(```|~~~)");

        public string Id => "M-1";

        public IEnumerable<Violation> Run(ValidationContext context)
        {
            var violations = new List<Violation>();
            string modulesDir = context.ResolvePath(context.ModulesRoot);
            if (!Directory.Exists(modulesDir))
            {
                violations.Add(Fail(context.ModulesRoot.Replace('\\', '/'), 0, "modules root folder not found"));
                return violations;
            }

            foreach (KeyValuePair<string, ModuleRules.ModuleInfo> module in context.Rules.Modules.OrderBy(m => m.Key, StringComparer.Ordinal))
                CheckModule(context, modulesDir, module.Key, module.Value, violations);

            foreach (string folder in Directory.GetDirectories(modulesDir).Select(d => Path.GetFileName(d)).OrderBy(n => n, StringComparer.Ordinal))
            {
                if (IsHidden(folder) || context.Rules.Modules.ContainsKey(folder)) continue;
                violations.Add(Fail(context.ModulePath(folder), 0, "module folder is not listed under modules in tools/checks/module-rules.json"));
            }
            return violations;
        }

        void CheckModule(ValidationContext context, string modulesDir, string module, ModuleRules.ModuleInfo info, List<Violation> violations)
        {
            string moduleDir = Path.Combine(modulesDir, module);
            if (!Directory.Exists(moduleDir))
            {
                violations.Add(Fail(context.ModulePath(module), 0, "module folder is missing (module-rules.json lists the module)"));
                return;
            }

            CheckReadme(context, moduleDir, module, violations);
            IReadOnlyList<ExpectedAsmdef> expected = ExpectedAsmdef.For(module, info);
            foreach (ExpectedAsmdef asmdef in expected)
                CheckAsmdef(context, moduleDir, asmdef, info, violations);

            foreach (string file in AsmdefFiles(moduleDir).OrderBy(f => f, StringComparer.Ordinal))
            {
                string relative = Path.GetRelativePath(moduleDir, file).Replace('\\', '/');
                if (expected.Any(e => e.RelativePath == relative)) continue;
                violations.Add(Fail(context.ModulePath(module, relative), 0, $"unexpected asmdef: module-rules.json gives {module} no asmdef at this path"));
            }
        }

        // --- README ---

        void CheckReadme(ValidationContext context, string moduleDir, string module, List<Violation> violations)
        {
            string path = context.ModulePath(module, "README.md");
            string file = Path.Combine(moduleDir, "README.md");
            if (!File.Exists(file))
            {
                violations.Add(Fail(path, 0, "missing README.md"));
                return;
            }

            var expected = new List<string> { "# " + module };
            expected.AddRange(ReadmeSections.Select(section => "## " + section));
            List<string> found = Headings(File.ReadAllText(file));
            if (found.OrderBy(h => h, StringComparer.Ordinal).SequenceEqual(expected.OrderBy(h => h, StringComparer.Ordinal))) return;
            violations.Add(Fail(path, 0, $"README headings differ from the template: expected {Format(expected)}, found {Format(found)}"));
        }

        /// <summary>The ATX headings of a Markdown text as <c>"## Text"</c>, ignoring fenced code blocks.</summary>
        static List<string> Headings(string markdown)
        {
            var headings = new List<string>();
            string openFence = null;
            foreach (string raw in markdown.TrimStart('\uFEFF').Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                Match fence = FenceLine.Match(line);
                if (fence.Success)
                {
                    if (openFence == null) openFence = fence.Groups[1].Value;
                    else if (openFence == fence.Groups[1].Value) openFence = null;
                    continue;
                }
                if (openFence != null) continue;
                Match heading = HeadingLine.Match(line);
                if (heading.Success) headings.Add(heading.Groups[1].Value + " " + heading.Groups[2].Value.Trim());
            }
            return headings;
        }

        // --- asmdefs ---

        void CheckAsmdef(ValidationContext context, string moduleDir, ExpectedAsmdef expected, ModuleRules.ModuleInfo info, List<Violation> violations)
        {
            string path = context.ModulePath(expected.Module, expected.RelativePath);
            string file = Path.Combine(moduleDir, expected.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(file))
            {
                violations.Add(Fail(path, 0, $"missing asmdef {expected.AssemblyName}"));
                return;
            }

            AsmdefFile asmdef;
            try
            {
                asmdef = AsmdefFile.Read(file);
            }
            catch (Exception e) when (e is IOException || e is FormatException || e is UnauthorizedAccessException)
            {
                violations.Add(Fail(path, 0, $"cannot read asmdef: {e.Message}"));
                return;
            }

            if (asmdef.Name != expected.AssemblyName)
                violations.Add(Fail(path, asmdef.LineOf("name"), $"name is '{asmdef.Name}', expected '{expected.AssemblyName}'"));
            if (asmdef.RootNamespace != expected.RootNamespace)
                violations.Add(Fail(path, asmdef.LineOf("rootNamespace"), $"rootNamespace is '{asmdef.RootNamespace}', expected '{expected.RootNamespace}'"));
            if (!SameSet(asmdef.IncludePlatforms, expected.IncludePlatforms))
                violations.Add(Fail(path, asmdef.LineOf("includePlatforms"), $"includePlatforms is {Format(asmdef.IncludePlatforms)}, expected {Format(expected.IncludePlatforms)}"));
            if (asmdef.AutoReferenced)
                violations.Add(Fail(path, asmdef.LineOf("autoReferenced"), "autoReferenced must be false"));
            if (!asmdef.OverrideReferences)
                violations.Add(Fail(path, asmdef.LineOf("overrideReferences"), "overrideReferences must be true, so no precompiled DLL such as Newtonsoft.Json.dll reaches the assembly by default"));

            if (expected.IsTest)
            {
                if (!asmdef.PrecompiledReferences.Contains(NUnit))
                    violations.Add(Fail(path, asmdef.LineOf("precompiledReferences"), $"precompiledReferences must include {NUnit}"));
                if (!asmdef.DefineConstraints.Contains(IncludeTests))
                    violations.Add(Fail(path, asmdef.LineOf("defineConstraints"), $"defineConstraints must include {IncludeTests}"));
            }
            else if (expected.Kind == ExpectedAsmdef.AsmdefKind.Runtime && info.IsTestOnly && !asmdef.DefineConstraints.Contains(IncludeTests))
            {
                violations.Add(Fail(path, asmdef.LineOf("defineConstraints"), $"defineConstraints must include {IncludeTests}, so the test-only assembly never ships"));
            }
        }

        /// <summary>Every asmdef under a module folder, skipping hidden folders (a leading '.' or a trailing '~'), which Unity ignores.</summary>
        static IEnumerable<string> AsmdefFiles(string directory)
        {
            foreach (string file in Directory.GetFiles(directory, "*.asmdef"))
                yield return file;
            foreach (string sub in Directory.GetDirectories(directory))
            {
                if (IsHidden(Path.GetFileName(sub))) continue;
                foreach (string file in AsmdefFiles(sub))
                    yield return file;
            }
        }

        static bool IsHidden(string folderName) => folderName.StartsWith(".", StringComparison.Ordinal) || folderName.EndsWith("~", StringComparison.Ordinal);

        static bool SameSet(IEnumerable<string> actual, IEnumerable<string> expected)
        {
            return actual.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(expected.OrderBy(x => x, StringComparer.Ordinal));
        }

        static string Format(IEnumerable<string> items) => "[" + string.Join(", ", items) + "]";

        Violation Fail(string path, int line, string message) => new Violation(Id, path, line, message, false);
    }
}
