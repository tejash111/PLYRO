using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bowling.Tooling.Editor;
using NUnit.Framework;
using UnityEditor;

namespace Bowling.Tooling.Tests
{
    /// <summary>
    /// M-2 (foundation spec, sections 4 and 8.2). All cases share <c>Fixtures~/M-2/module-rules.json</c>, a copy
    /// of the real allowed-reference table plus one module with no entry. The pass fixture holds one asmdef of
    /// every kind. Each fail fixture holds a single asmdef that breaks one rule, so each produces exactly one
    /// violation that names the file.
    /// </summary>
    public class AsmdefReferenceRuleTests
    {
        const string TestingGuid = "00000000000000000000000000000001";
        const string ToolingEditorAsmdef = "Assets/_Project/Modules/Tooling/Editor/Bowling.Tooling.Editor.asmdef";
        const string ToolingTestsAsmdef = "Assets/_Project/Modules/Tooling/Tests/EditMode/Bowling.Tooling.Tests.EditMode.asmdef";

        string _tempDir;

        [TearDown]
        public void TearDown()
        {
            if (_tempDir != null && Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }

        static List<Violation> Run(ValidationContext context) => new AsmdefReferenceRule().Run(context).ToList();

        [Test]
        public void RuleIdIsM2()
        {
            Assert.That(new AsmdefReferenceRule().Id, Is.EqualTo("M-2"));
        }

        [Test]
        public void CompleteProjectPasses()
        {
            List<Violation> violations = Run(ValidatorFixtures.M2("pass"));
            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        [TestCase("gameplay-references-physics", "Bowling.Gameplay.asmdef", "Bowling.BowlingPhysics")]
        [TestCase("ui-newtonsoft", "Bowling.UI.asmdef", "Newtonsoft.Json.dll")]
        [TestCase("unsafe-outside-poseinput", "Bowling.Gameplay.asmdef", "allowUnsafeCode")]
        [TestCase("unresolvable-guid", "Bowling.Gameplay.asmdef", "unresolvable GUID reference")]
        [TestCase("test-references-other-module", "Bowling.Gameplay.Tests.EditMode.asmdef", "Bowling.PoseInput")]
        [TestCase("app-references-testing", "Bowling.App.asmdef", "Bowling.Testing")]
        [TestCase("unlisted-assembly", "Bowling.Mystery.asmdef", "not listed")]
        [TestCase("malformed-asmdef", "Bowling.Gameplay.asmdef", "cannot read")]
        public void FailureReportsExactlyOneViolationNamingTheFile(string fixture, string file, string messagePart)
        {
            List<Violation> violations = Run(ValidatorFixtures.M2("fail/" + fixture));

            Assert.That(violations, Has.Count.EqualTo(1), string.Join("\n", violations));
            Violation violation = violations[0];
            Assert.That(violation.RuleId, Is.EqualTo("M-2"));
            Assert.That(violation.IsWarning, Is.False);
            Assert.That(violation.Path, Does.EndWith("/" + file));
            Assert.That(violation.Message, Does.Contain(messagePart));
        }

        [Test]
        public void ReferenceViolationPointsAtTheLineOfTheReference()
        {
            // The generated asmdef lists "Bowling.Contracts" on line 5 and "Bowling.BowlingPhysics" on line 6.
            List<Violation> violations = Run(ValidatorFixtures.M2("fail/gameplay-references-physics"));

            Assert.That(violations[0].Line, Is.EqualTo(6));
            Assert.That(violations[0].ToString(), Does.Contain("Bowling.Gameplay.asmdef:6 "));
        }

        [Test]
        public void GuidOfAnAllowedAssemblyPasses()
        {
            // A package asmdef, to show that resolution does not depend on the file being under Assets.
            string urp = FindAsmdefGuid("Unity.RenderPipelines.Universal.Runtime");
            ValidationContext context = TempContext("Tooling", "Editor", "Bowling.Tooling.Editor", "GUID:" + urp);

            Assert.That(Run(context), Is.Empty);
        }

        [Test]
        public void GuidOfTheOwnModuleAssemblyPassesForATestAssembly()
        {
            string toolingEditor = AssetDatabase.AssetPathToGUID(ToolingEditorAsmdef);
            Assert.That(toolingEditor, Is.Not.Empty);
            ValidationContext context = TempContext(
                "Tooling", "Tests/EditMode", "Bowling.Tooling.Tests.EditMode", "GUID:" + toolingEditor);

            Assert.That(Run(context), Is.Empty);
        }

        [Test]
        public void GuidOfADisallowedAssemblyIsReportedByItsResolvedName()
        {
            string testsGuid = AssetDatabase.AssetPathToGUID(ToolingTestsAsmdef);
            Assert.That(testsGuid, Is.Not.Empty);
            ValidationContext context = TempContext("Tooling", "Editor", "Bowling.Tooling.Editor", "GUID:" + testsGuid);

            List<Violation> violations = Run(context);

            Assert.That(violations, Has.Count.EqualTo(1), string.Join("\n", violations));
            Assert.That(violations[0].Message, Does.Contain("Bowling.Tooling.Tests.EditMode"));
        }

        [Test]
        public void GuidThatDoesNotExistIsUnresolvable()
        {
            ValidationContext context = TempContext("Tooling", "Editor", "Bowling.Tooling.Editor", "GUID:" + TestingGuid);

            List<Violation> violations = Run(context);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0].Message, Does.Contain("unresolvable GUID reference"));
        }

        [Test]
        public void MalformedGuidIsUnresolvable()
        {
            ValidationContext context = TempContext("Tooling", "Editor", "Bowling.Tooling.Editor", "GUID:not-a-guid");

            List<Violation> violations = Run(context);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0].Message, Does.Contain("unresolvable GUID reference"));
        }

        static string FindAsmdefGuid(string assemblyName)
        {
            string guid = AssetDatabase.FindAssets(assemblyName + " t:AssemblyDefinitionAsset")
                .FirstOrDefault(g => AssetDatabase.GUIDToAssetPath(g).EndsWith("/" + assemblyName + ".asmdef", StringComparison.Ordinal));
            Assert.That(guid, Is.Not.Null, "no asmdef named " + assemblyName + " in the project");
            return guid;
        }

        // A made-up module whose test assemblies may reach "Extra.Assembly" only through a wildcard entry.
        const string ZedRules = @"{
            ""modules"": { ""Zed"": { ""runtime"": true, ""editor"": false, ""editMode"": true, ""playMode"": false } },
            ""assemblies"": { ""Bowling.Zed"": { ""references"": [] }%WILDCARD% },
            ""testAssemblies"": { ""references"": [""$module"", ""$moduleAllowed""], ""precompiled"": [] } }";
        const string ZedWildcard = @", ""Bowling.Zed.Tests.*"": { ""references"": [""Extra.Assembly""] }";

        [Test]
        public void WildcardEntryAddsToTheTestAssembliesItCovers()
        {
            ValidationContext context = TempContext(
                "Zed", "Tests/EditMode", "Bowling.Zed.Tests.EditMode", "Extra.Assembly", ZedRules.Replace("%WILDCARD%", ZedWildcard));

            Assert.That(Run(context), Is.Empty);
        }

        [Test]
        public void WithoutTheWildcardEntryTheSameReferenceIsReported()
        {
            ValidationContext context = TempContext(
                "Zed", "Tests/EditMode", "Bowling.Zed.Tests.EditMode", "Extra.Assembly", ZedRules.Replace("%WILDCARD%", ""));

            List<Violation> violations = Run(context);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0].Message, Does.Contain("Extra.Assembly"));
        }

        /// <summary>
        /// Writes one asmdef under a temp <c>Modules/&lt;module&gt;/&lt;folder&gt;</c> and points a context at it,
        /// with the shared M-2 fixture rules or the given rules JSON.
        /// </summary>
        ValidationContext TempContext(string module, string folder, string assemblyName, string reference, string rulesJson = null)
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "bowling-m2-" + Guid.NewGuid().ToString("N"));
            string dir = Path.Combine(_tempDir, "Modules", module, folder);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, assemblyName + ".asmdef"),
                "{ \"name\": \"" + assemblyName + "\", \"references\": [ \"" + reference + "\" ] }");
            ModuleRules rules = ValidatorFixtures.M2("pass").Rules;
            if (rulesJson != null)
            {
                string rulesFile = Path.Combine(_tempDir, "module-rules.json");
                File.WriteAllText(rulesFile, rulesJson);
                rules = ModuleRules.Load(rulesFile);
            }
            return new ValidationContext
            {
                RepoRoot = _tempDir,
                ProjectRoot = _tempDir,
                ModulesRoot = Path.Combine(_tempDir, "Modules"),
                Rules = rules,
            };
        }
    }
}
