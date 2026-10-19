using System.Collections.Generic;
using System.Linq;
using Bowling.Tooling.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Bowling.Tooling.Tests
{
    /// <summary>
    /// G-6 (foundation spec, section 8.1) with no search folder named: the rule reads all of <c>Assets/_Project</c>
    /// except Tooling and the module test folders, not just Art and the UI module. The cases put a ramp PNG into a temporary folder of the real
    /// project, which teardown deletes, and run the rule with <see cref="ValidationContext.TextureSearchRoots"/> unset.
    /// </summary>
    public class TextureGradientDefaultRootTests
    {
        const string TempName = "_TestTempG6";
        const string ProjectFolder = "Assets/_Project";
        const string ToolingFolder = "Assets/_Project/Modules/Tooling";
        const string UiTestsFolder = "Assets/_Project/Modules/UI/Tests";

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset($"{ProjectFolder}/{TempName}");
            AssetDatabase.DeleteAsset($"{ProjectFolder}/Modules/UI/{TempName}");
            AssetDatabase.DeleteAsset($"{ToolingFolder}/{TempName}");
            AssetDatabase.DeleteAsset($"{UiTestsFolder}/{TempName}");
            AssetDatabase.DeleteAsset($"{UiTestsFolder}/EditMode/{TempName}");
        }

        static string RampIn(string parent)
        {
            AssetDatabase.CreateFolder(parent, TempName);
            return TextureFixtures.Png($"{parent}/{TempName}", "Ramp", 256, 4, TextureFixtures.Ramp);
        }

        static List<Violation> RunWithDefaultRoots()
        {
            ValidationContext context = TempAssets.Context("Assets");
            context.TextureSearchRoots = null;
            return new TextureGradientRule().Run(context).ToList();
        }

        [Test]
        public void TheDefaultRootIsAllOfProject()
        {
            Assert.That(new ValidationContext().DefaultTextureSearchRoots, Is.EqualTo(new[] { "Assets/_Project" }));
        }

        [TestCase("Assets/_Project")]
        [TestCase("Assets/_Project/Modules/UI")]
        public void ARampUnderTheProjectIsJudgedByDefault(string parent)
        {
            string path = RampIn(parent);

            List<Violation> violations = RunWithDefaultRoots();

            Assert.That(violations, Has.Count.EqualTo(1), string.Join("\n", violations));
            Assert.That(violations[0].RuleId, Is.EqualTo("G-6"));
            Assert.That(violations[0].Path, Is.EqualTo(path));
            Assert.That(violations[0].Message, Does.Contain("smooth-ramp pixels"));
        }

        // Ruling R30: an image fixture a test reads is test data, so a module's Tests/ folder is not judged.
        [TestCase(UiTestsFolder)]
        [TestCase(UiTestsFolder + "/EditMode")]
        public void ARampUnderAModuleTestFolderIsNotJudged(string parent)
        {
            string path = RampIn(parent);
            Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(path), Is.Not.Null, "the ramp was not imported");

            List<Violation> violations = RunWithDefaultRoots();

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        // ...unless it can ship: an asset outside the module test folders uses it.
        [Test]
        public void ARampInAModuleTestFolderThatAnAssetOutsideTestsUsesIsJudged()
        {
            string path = RampIn(UiTestsFolder + "/EditMode");
            AssetDatabase.CreateFolder(ProjectFolder, TempName);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            Assert.That(shader, Is.Not.Null, "the URP Unlit shader was not found");
            var material = new Material(shader);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            AssetDatabase.CreateAsset(material, $"{ProjectFolder}/{TempName}/Uses.mat");
            Assert.That(AssetDatabase.GetDependencies($"{ProjectFolder}/{TempName}/Uses.mat"), Does.Contain(path));

            List<Violation> violations = RunWithDefaultRoots();

            Assert.That(violations, Has.Count.EqualTo(1), string.Join("\n", violations));
            Assert.That(violations[0].Path, Is.EqualTo(path));
        }

        // ...or it sits in a Resources folder, which goes into the build whatever uses it.
        [Test]
        public void ARampInAResourcesFolderOfAModuleTestFolderIsJudged()
        {
            AssetDatabase.CreateFolder(UiTestsFolder, TempName);
            AssetDatabase.CreateFolder($"{UiTestsFolder}/{TempName}", "Resources");
            string path = TextureFixtures.Png($"{UiTestsFolder}/{TempName}/Resources", "Ramp", 256, 4, TextureFixtures.Ramp);

            List<Violation> violations = RunWithDefaultRoots();

            Assert.That(violations, Has.Count.EqualTo(1), string.Join("\n", violations));
            Assert.That(violations[0].Path, Is.EqualTo(path));
        }

        [Test]
        public void ARampInAFolderNamedTestsOutsideAModuleIsJudged()
        {
            AssetDatabase.CreateFolder(ProjectFolder, TempName);
            AssetDatabase.CreateFolder($"{ProjectFolder}/{TempName}", "Tests");
            string path = TextureFixtures.Png($"{ProjectFolder}/{TempName}/Tests", "Ramp", 256, 4, TextureFixtures.Ramp);

            List<Violation> violations = RunWithDefaultRoots();

            Assert.That(violations, Has.Count.EqualTo(1), string.Join("\n", violations));
            Assert.That(violations[0].Path, Is.EqualTo(path));
        }

        [Test]
        public void ARampUnderToolingIsNotJudged()
        {
            RampIn(ToolingFolder);

            List<Violation> violations = RunWithDefaultRoots();

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }
    }
}
