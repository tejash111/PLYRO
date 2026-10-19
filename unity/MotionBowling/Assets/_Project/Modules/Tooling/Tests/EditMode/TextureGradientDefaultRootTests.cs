using System.Collections.Generic;
using System.Linq;
using Bowling.Tooling.Editor;
using NUnit.Framework;
using UnityEditor;

namespace Bowling.Tooling.Tests
{
    /// <summary>
    /// G-6 (foundation spec, section 8.1) with no search folder named: the rule reads all of <c>Assets/_Project</c>
    /// except Tooling, not just Art and the UI module. The cases put a ramp PNG into a temporary folder of the real
    /// project, which teardown deletes, and run the rule with <see cref="ValidationContext.TextureSearchRoots"/> unset.
    /// </summary>
    public class TextureGradientDefaultRootTests
    {
        const string TempName = "_TestTempG6";
        const string ProjectFolder = "Assets/_Project";
        const string ToolingFolder = "Assets/_Project/Modules/Tooling";

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset($"{ProjectFolder}/{TempName}");
            AssetDatabase.DeleteAsset($"{ProjectFolder}/Modules/UI/{TempName}");
            AssetDatabase.DeleteAsset($"{ToolingFolder}/{TempName}");
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

        [Test]
        public void ARampUnderToolingIsNotJudged()
        {
            RampIn(ToolingFolder);

            List<Violation> violations = RunWithDefaultRoots();

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }
    }
}
