using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bowling.Tooling.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Bowling.Tooling.Tests
{
    /// <summary>
    /// M-1 (foundation spec, sections 4 and 8.2). The pass fixture is a miniature project with every kind of
    /// module. Each fail fixture breaks one thing in an otherwise valid module, so each case must produce
    /// exactly one violation, and it must name the offending file or folder.
    /// </summary>
    public class ModuleStructureRuleTests
    {
        static List<Violation> Run(ValidationContext context) => new ModuleStructureRule().Run(context).ToList();

        [Test]
        public void RuleIdIsM1()
        {
            Assert.That(new ModuleStructureRule().Id, Is.EqualTo("M-1"));
        }

        [Test]
        public void CompleteProjectPasses()
        {
            List<Violation> violations = Run(ValidatorFixtures.M1("pass"));
            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        // The brief's nine cases.
        [TestCase("missing-readme", "README.md", "missing")]
        [TestCase("readme-headings", "README.md", "headings")]
        [TestCase("missing-asmdef", "Bowling.Contracts.Tests.EditMode.asmdef", "missing")]
        [TestCase("wrong-root-namespace", "Bowling.Contracts.asmdef", "rootNamespace")]
        [TestCase("auto-referenced", "Bowling.Contracts.asmdef", "autoReferenced")]
        [TestCase("override-references-false", "Bowling.Contracts.asmdef", "overrideReferences")]
        [TestCase("editmode-platforms", "Bowling.Contracts.Tests.EditMode.asmdef", "includePlatforms")]
        [TestCase("testing-define-constraint", "Bowling.Testing.asmdef", "UNITY_INCLUDE_TESTS")]
        [TestCase("unlisted-module", "Rogue", "not listed")]
        // The rest of the kind table and the folder checks.
        [TestCase("wrong-name", "Bowling.Contracts.asmdef", "name")]
        [TestCase("runtime-platforms", "Bowling.Contracts.asmdef", "includePlatforms")]
        [TestCase("no-nunit-reference", "Bowling.Contracts.Tests.EditMode.asmdef", "nunit.framework.dll")]
        [TestCase("no-test-define-constraint", "Bowling.Contracts.Tests.EditMode.asmdef", "UNITY_INCLUDE_TESTS")]
        [TestCase("unexpected-asmdef", "Bowling.Contracts.Extra.asmdef", "unexpected")]
        [TestCase("malformed-asmdef", "Bowling.Contracts.asmdef", "cannot read")]
        [TestCase("missing-module-folder", "Gameplay", "missing")]
        // A runtime asmdef other than Contracts references Bowling.Contracts (Testing too).
        [TestCase("no-contracts-reference", "Bowling.Gameplay.asmdef", "Bowling.Contracts")]
        [TestCase("testing-no-contracts-reference", "Bowling.Testing.asmdef", "Bowling.Contracts")]
        // Ruling R25: an .asmref under Modules/ and an asmdef outside a module folder.
        [TestCase("asmref-in-module", "Extra.asmref", ".asmref")]
        [TestCase("asmdef-outside-modules", "Bowling.Rogue.asmdef", "outside a module folder")]
        [TestCase("asmdef-loose-in-modules", "Bowling.Loose.asmdef", "outside a module folder")]
        public void FailureReportsExactlyOneViolationNamingTheFile(string fixture, string name, string messagePart)
        {
            List<Violation> violations = Run(ValidatorFixtures.M1("fail/" + fixture));

            Assert.That(violations, Has.Count.EqualTo(1), string.Join("\n", violations));
            Violation violation = violations[0];
            Assert.That(violation.RuleId, Is.EqualTo("M-1"));
            Assert.That(violation.IsWarning, Is.False);
            Assert.That(violation.Path, Does.EndWith("/" + name));
            Assert.That(violation.Message, Does.Contain(messagePart));
        }

        [Test]
        public void MissingModulesRootIsOneViolation()
        {
            ValidationContext context = ValidatorFixtures.M1("pass");
            context.ModulesRoot = Path.Combine(ValidatorFixtures.Root, "M-1", "does-not-exist");

            List<Violation> violations = Run(context);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0].Message, Does.Contain("not found"));
        }

        // --- The Contracts reference by GUID ---
        // No Bowling.Contracts asmdef exists yet, and a real one would recompile the project mid-test. So the
        // GUID points at a text asset under Assets/_TestTemp that holds asmdef JSON: the resolver only reads its name.

        const string ToolingEditorAsmdef = "Assets/_Project/Modules/Tooling/Editor/Bowling.Tooling.Editor.asmdef";
        const string TempAssets = "Assets/_TestTemp";

        string _tempDir;
        bool _importedTempAsset;

        [TearDown]
        public void TearDown()
        {
            if (_tempDir != null && Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
            if (_importedTempAsset) AssetDatabase.DeleteAsset(TempAssets);
        }

        [Test]
        public void ContractsReferencedByAGuidThatResolvesToItPasses()
        {
            string guid = ImportAsmdefText("Bowling.Contracts");

            List<Violation> violations = Run(TempGameplayModule("GUID:" + guid));

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        [Test]
        public void AGuidThatResolvesToAnotherAssemblyIsNotTheContractsReference()
        {
            string guid = AssetDatabase.AssetPathToGUID(ToolingEditorAsmdef);
            Assume.That(guid, Is.Not.Empty);

            List<Violation> violations = Run(TempGameplayModule("GUID:" + guid));

            Assert.That(violations, Has.Count.EqualTo(1), string.Join("\n", violations));
            Assert.That(violations[0].Path, Does.EndWith("/Bowling.Gameplay.asmdef"));
            Assert.That(violations[0].Message, Does.Contain("Bowling.Contracts"));
        }

        [Test]
        public void AGuidThatCannotBeResolvedIsNotTheContractsReference()
        {
            List<Violation> violations = Run(TempGameplayModule("GUID:00000000000000000000000000000001"));

            Assert.That(violations, Has.Count.EqualTo(1), string.Join("\n", violations));
            Assert.That(violations[0].Message, Does.Contain("Bowling.Contracts"));
        }

        string ImportAsmdefText(string assemblyName)
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "_TestTemp"));
            string assetPath = $"{TempAssets}/{assemblyName}.asmdef.txt";
            File.WriteAllText(Path.Combine(Application.dataPath, "_TestTemp", assemblyName + ".asmdef.txt"),
                "{ \"name\": \"" + assemblyName + "\" }");
            _importedTempAsset = true;
            AssetDatabase.ImportAsset(assetPath);
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            Assume.That(guid, Is.Not.Empty);
            return guid;
        }

        /// <summary>A complete runtime-only module <c>Gameplay</c> in a temp folder, whose one reference is the given entry.</summary>
        ValidationContext TempGameplayModule(string reference)
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "bowling-m1-" + System.Guid.NewGuid().ToString("N"));
            string module = Path.Combine(_tempDir, "Modules", "Gameplay");
            Directory.CreateDirectory(Path.Combine(module, "Runtime"));
            File.WriteAllText(Path.Combine(module, "README.md"),
                "# Gameplay\n\n## Purpose\n\n## Public API\n\n## Dependencies\n\n## Folder map\n");
            File.WriteAllText(Path.Combine(module, "Runtime", "Bowling.Gameplay.asmdef"),
                "{ \"name\": \"Bowling.Gameplay\", \"rootNamespace\": \"Bowling.Gameplay\", \"references\": [ \"" + reference + "\" ],"
                + " \"includePlatforms\": [], \"overrideReferences\": true, \"autoReferenced\": false }");
            string rules = Path.Combine(_tempDir, "module-rules.json");
            File.WriteAllText(rules,
                "{ \"modules\": { \"Gameplay\": { \"runtime\": true, \"editor\": false, \"editMode\": false, \"playMode\": false } },"
                + " \"assemblies\": {}, \"testAssemblies\": { \"references\": [], \"precompiled\": [] } }");
            return ValidatorFixtures.Context(rules, _tempDir);
        }
    }
}
