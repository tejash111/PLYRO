using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bowling.Tooling.Editor;
using NUnit.Framework;

namespace Bowling.Tooling.Tests
{
    /// <summary>
    /// <c>tools/checks/module-rules.json</c>. The first tests hold the real file to foundation spec section 4
    /// (the module table and the allowed-reference table). The rest feed <see cref="ModuleRules.Load"/> files
    /// that are wrong in one way, which must be rejected rather than read as "allow nothing".
    /// </summary>
    public class ModuleRulesTests
    {
        static readonly string[] SpecModules =
        {
            "Contracts", "PoseInput", "ThrowDetection", "BowlingPhysics", "Gameplay", "Presentation",
            "UI", "Platform", "App", "Tooling", "Testing",
        };

        string _tempFile;

        [TearDown]
        public void TearDown()
        {
            if (_tempFile != null && File.Exists(_tempFile)) File.Delete(_tempFile);
        }

        static ModuleRules RealRules() => ValidationContext.ForProject().Rules;

        // --- The real file against spec section 4 ---

        [Test]
        public void RealFileListsTheElevenModules()
        {
            Assert.That(RealRules().Modules.Keys, Is.EquivalentTo(SpecModules));
        }

        [Test]
        public void RealFileHasTheModuleTableOfSpecSection4()
        {
            ModuleRules rules = RealRules();
            string[] noRuntime = { "Tooling" };
            string[] editor = { "App", "Tooling" };
            string[] playMode = { "BowlingPhysics", "Presentation", "UI", "App" };

            foreach (string module in SpecModules)
            {
                ModuleRules.ModuleInfo info = rules.Modules[module];
                Assert.That(info.HasRuntime, Is.EqualTo(!noRuntime.Contains(module)), module + " runtime");
                Assert.That(info.HasEditor, Is.EqualTo(editor.Contains(module)), module + " editor");
                Assert.That(info.HasEditMode, Is.True, module + " editMode");
                Assert.That(info.HasPlayMode, Is.EqualTo(playMode.Contains(module)), module + " playMode");
                Assert.That(info.IsTestOnly, Is.EqualTo(module == "Testing"), module + " testOnly");
            }
        }

        [Test]
        public void OnlyPoseInputMayUseUnsafeCode()
        {
            IEnumerable<string> allowed = RealRules().Assemblies.Where(a => a.Value.AllowUnsafeCode).Select(a => a.Key);
            Assert.That(allowed, Is.EqualTo(new[] { "Bowling.PoseInput" }));
        }

        [Test]
        public void RealFileHasTheAllowedReferencesOfSpecSection4()
        {
            ModuleRules rules = RealRules();

            Assert.That(rules.FindAssembly("Bowling.Contracts").References, Is.Empty);
            foreach (string module in new[] { "ThrowDetection", "BowlingPhysics", "Gameplay", "UI" })
            {
                Assert.That(rules.FindAssembly("Bowling." + module).References, Is.EqualTo(new[] { "Bowling.Contracts" }), module);
                Assert.That(rules.FindAssembly("Bowling." + module).Precompiled, Is.Empty, module);
            }
            Assert.That(rules.FindAssembly("Bowling.PoseInput").Precompiled, Is.EqualTo(new[] { "Newtonsoft.Json.dll" }));
            Assert.That(rules.FindAssembly("Bowling.Platform").Precompiled, Is.EqualTo(new[] { "Newtonsoft.Json.dll" }));
            Assert.That(rules.FindAssembly("Bowling.Platform").References, Is.EquivalentTo(new[] { "Bowling.Contracts", "Unity.InputSystem" }));
            Assert.That(rules.FindAssembly("Bowling.Presentation").References, Is.EquivalentTo(new[]
            {
                "Bowling.Contracts", "Unity.Cinemachine", "FMODUnity",
                "Unity.RenderPipelines.Universal.Runtime", "Unity.RenderPipelines.Core.Runtime",
            }));
            Assert.That(rules.FindAssembly("Bowling.Tooling.Editor").References, Is.EquivalentTo(new[]
            {
                "Unity.RenderPipelines.Universal.Runtime", "Unity.RenderPipelines.Core.Runtime",
            }));
            Assert.That(rules.FindAssembly("Bowling.Tooling.Editor").Precompiled, Is.EqualTo(new[] { "Newtonsoft.Json.dll" }));
        }

        [Test]
        public void AllRuntimeIsEveryRuntimeAssemblyExceptTheTestOnlyOne()
        {
            ModuleRules rules = RealRules();
            IReadOnlyList<string> allRuntime = rules.ExpandReferences(new[] { ModuleRules.AllRuntimeToken }, null);

            Assert.That(allRuntime, Is.EquivalentTo(SpecModules
                .Where(m => m != "Tooling" && m != "Testing")
                .Select(m => "Bowling." + m)));

            IReadOnlyList<string> app = rules.ExpandReferences(rules.FindAssembly("Bowling.App").References, null);
            Assert.That(app, Is.SupersetOf(allRuntime));
            Assert.That(app, Contains.Item("Unity.InputSystem"));
            Assert.That(app, Does.Not.Contain("Bowling.Testing"));
        }

        [Test]
        public void AppTestsGetAllRuntimeAssembliesAndOtherTestsDoNot()
        {
            ModuleRules rules = RealRules();

            Assert.That(rules.FindAssembly("Bowling.App.Tests.EditMode").References, Contains.Item(ModuleRules.AllRuntimeToken));
            Assert.That(rules.FindAssembly("Bowling.App.Tests.PlayMode").References, Contains.Item(ModuleRules.AllRuntimeToken));
            Assert.That(rules.FindAssembly("Bowling.Gameplay.Tests.EditMode"), Is.Null);
        }

        [Test]
        public void PrimaryAssemblyIsTheRuntimeOneOrTheEditorOneWithoutRuntime()
        {
            ModuleRules rules = RealRules();

            Assert.That(rules.PrimaryAssembly("Gameplay"), Is.EqualTo("Bowling.Gameplay"));
            Assert.That(rules.PrimaryAssembly("Tooling"), Is.EqualTo("Bowling.Tooling.Editor"));
        }

        [Test]
        public void ToolingModuleOfTheRealProjectPassesM1AndM2()
        {
            ValidationContext context = ValidationContext.ForProject();
            var rules = new IValidationRule[] { new ModuleStructureRule(), new AsmdefReferenceRule() };

            List<Violation> violations = rules
                .SelectMany(rule => rule.Run(context))
                .Where(v => v.Path.Contains("/Modules/Tooling"))
                .ToList();

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        // --- Files that must be rejected ---

        const string Tail = @", ""assemblies"": {}, ""testAssemblies"": { ""references"": [], ""precompiled"": [] } }";

        [Test]
        public void ValidFileLoads()
        {
            const string json = @"{ ""modules"": { ""A"": { ""runtime"": true, ""editor"": false, ""editMode"": true, ""playMode"": false } }" + Tail;
            Assert.That(Load(json).Modules.Keys, Is.EqualTo(new[] { "A" }));
        }

        [TestCase(@"{ ""modules"": {}, ""assemblies"": {} }", "testAssemblies", TestName = "MissingSection")]
        [TestCase(@"{ ""modules"": { ""../A"": { ""runtime"": true, ""editor"": false, ""editMode"": true, ""playMode"": false } }" + Tail, "module name", TestName = "BadModuleName")]
        [TestCase(@"{ ""modules"": { ""A"": { ""runtime"": true, ""editor"": false, ""editMode"": true } }" + Tail, "playMode", TestName = "MissingFlag")]
        [TestCase(@"{ ""modules"": { ""A"": { ""runtime"": false, ""editor"": true, ""editMode"": true, ""playMode"": false, ""testOnly"": true } }" + Tail, "testOnly", TestName = "TestOnlyWithoutRuntime")]
        [TestCase(@"{ ""modules"": {}, ""assemblies"": { ""Bowling.A"": { ""references"": [""$bogus""] } }, ""testAssemblies"": { ""references"": [], ""precompiled"": [] } }", "$bogus", TestName = "UnknownToken")]
        [TestCase(@"{ ""modules"": {}, ""assemblies"": { ""Bowling.A"": { ""references"": [""$module""] } }, ""testAssemblies"": { ""references"": [], ""precompiled"": [] } }", "$module", TestName = "ModuleTokenOutsideTestAssemblies")]
        [TestCase(@"{ ""modules"": {}, ""assemblies"": { ""Bowling.A"": { ""references"": [], ""precompilled"": [] } }, ""testAssemblies"": { ""references"": [], ""precompiled"": [] } }", "precompilled", TestName = "MisspelledKey")]
        [TestCase("{ not json", "not valid JSON", TestName = "NotJson")]
        public void InvalidFileIsRejectedWithAMessageThatNamesTheProblem(string json, string messagePart)
        {
            var exception = Assert.Throws<InvalidDataException>(() => Load(json));
            Assert.That(exception.Message, Does.Contain(messagePart));
        }

        [Test]
        public void MissingFileThrows()
        {
            Assert.Throws<FileNotFoundException>(() => ModuleRules.Load(Path.Combine(Path.GetTempPath(), "no-such-module-rules.json")));
        }

        ModuleRules Load(string json)
        {
            _tempFile = Path.Combine(Path.GetTempPath(), "module-rules-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(_tempFile, json);
            return ModuleRules.Load(_tempFile);
        }
    }
}
