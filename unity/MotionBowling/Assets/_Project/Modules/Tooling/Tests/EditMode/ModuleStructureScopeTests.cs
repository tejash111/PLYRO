using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bowling.Tooling.Editor;
using NUnit.Framework;

namespace Bowling.Tooling.Tests
{
    /// <summary>
    /// M-1 (foundation spec section 8.2): an <c>.asmref</c> anywhere under <c>Modules/</c> and an <c>.asmdef</c>
    /// anywhere under <c>Assets/</c> outside <c>Assets/_Project/Modules/</c> (vendor folders excluded) are violations.
    /// These cases use the real layout of the project (<c>Assets/_Project/Modules</c> and the default
    /// <see cref="ValidationContext.AssetsRoot"/>) in a temp folder, so they pin the paths a violation prints.
    /// </summary>
    public class ModuleStructureScopeTests
    {
        string _project;

        [SetUp]
        public void SetUp()
        {
            _project = Path.Combine(Path.GetTempPath(), "bowling-m1-scope-" + Guid.NewGuid().ToString("N"));
            Write("Assets/_Project/Modules/Gameplay/README.md",
                "# Gameplay\n\n## Purpose\n\n## Public API\n\n## Dependencies\n\n## Folder map\n");
            Write("Assets/_Project/Modules/Gameplay/Runtime/Bowling.Gameplay.asmdef", Asmdef("Bowling.Gameplay", "Bowling.Contracts"));
            Write("module-rules.json",
                "{ \"modules\": { \"Gameplay\": { \"runtime\": true, \"editor\": false, \"editMode\": false, \"playMode\": false } },"
                + " \"assemblies\": {}, \"testAssemblies\": { \"references\": [], \"precompiled\": [] } }");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_project)) Directory.Delete(_project, true);
        }

        static string Asmdef(string name, string reference)
        {
            string references = reference == null ? "" : "\"" + reference + "\"";
            return "{ \"name\": \"" + name + "\", \"rootNamespace\": \"" + name + "\", \"references\": [ " + references + " ],"
                + " \"includePlatforms\": [], \"overrideReferences\": true, \"autoReferenced\": false }";
        }

        void Write(string relative, string content)
        {
            string path = Path.Combine(_project, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content);
        }

        List<Violation> Run()
        {
            var context = new ValidationContext
            {
                RepoRoot = _project,
                ProjectRoot = _project,
                Rules = ModuleRules.Load(Path.Combine(_project, "module-rules.json")),
            };
            return new ModuleStructureRule().Run(context).ToList();
        }

        [Test]
        public void TheValidLayoutPasses()
        {
            List<Violation> violations = Run();
            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        [TestCase("Assets/Scripts/Rogue.asmdef")]
        [TestCase("Assets/Scripts/Deep/Down/Rogue.asmdef")]
        [TestCase("Assets/Rogue.asmdef")]
        [TestCase("Assets/_Project/Rogue.asmdef")]
        [TestCase("Assets/_Project/Scripts/Rogue.asmdef")]
        [TestCase("Assets/_Project/Modules/Rogue.asmdef")]
        [TestCase("Assets/Plugins/FMODX/Rogue.asmdef")]
        [TestCase("Assets/Plugins/Other/Rogue.asmdef")]
        public void AnAsmdefOutsideAModuleFolderIsOneViolationWithItsAssetPath(string asmdef)
        {
            Write(asmdef, Asmdef("Rogue", null));

            List<Violation> violations = Run();

            Assert.That(violations, Has.Count.EqualTo(1), string.Join("\n", violations));
            Assert.That(violations[0].RuleId, Is.EqualTo("M-1"));
            Assert.That(violations[0].Path, Is.EqualTo(asmdef));
            Assert.That(violations[0].IsWarning, Is.False);
            Assert.That(violations[0].Message, Does.Contain("outside a module folder"));
        }

        [TestCase("Assets/Plugins/FMOD/FMODUnity.asmdef")]
        [TestCase("Assets/Plugins/FMOD/Deep/Down/FMODUnity.Editor.asmdef")]
        [TestCase("Assets/Scripts~/Hidden.asmdef")]
        [TestCase("Assets/.Hidden/Hidden.asmdef")]
        public void VendorAndHiddenFoldersAreNotSearched(string asmdef)
        {
            Write(asmdef, Asmdef("Vendor", null));

            List<Violation> violations = Run();

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        [TestCase("Assets/_Project/Modules/Gameplay/Runtime/Extra.asmref")]
        [TestCase("Assets/_Project/Modules/Gameplay/Runtime/Core/Deep/Extra.asmref")]
        [TestCase("Assets/_Project/Modules/Gameplay/Extra.asmref")]
        [TestCase("Assets/_Project/Modules/Extra.asmref")]
        [TestCase("Assets/_Project/Modules/Unlisted/Runtime/Extra.asmref")]
        public void AnAsmrefUnderModulesIsAViolationWithItsAssetPath(string asmref)
        {
            Write(asmref, "{ \"reference\": \"Bowling.Contracts\" }");

            List<Violation> violations = Run();

            Assert.That(violations.Where(v => v.Path == asmref).ToList(), Has.Count.EqualTo(1), string.Join("\n", violations));
            Violation violation = violations.Single(v => v.Path == asmref);
            Assert.That(violation.RuleId, Is.EqualTo("M-1"));
            Assert.That(violation.IsWarning, Is.False);
            Assert.That(violation.Message, Does.Contain(".asmref"));
        }
    }
}
