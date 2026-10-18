using System.Collections.Generic;
using System.Linq;
using Bowling.Tooling.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Bowling.Tooling.Tests
{
    /// <summary>
    /// What the G-5 fixtures share (foundation spec, section 8.1): a temp folder per test, a context that scans only
    /// that folder and the scenes a test names, and the "exactly one violation naming the rule and the asset" check.
    /// </summary>
    public abstract class G5RuleTestBase
    {
        protected const string MainScene = "Assets/_Project/Scenes/Main.unity";

        /// <summary>The temp asset folder of the running test.</summary>
        protected string Folder { get; private set; }

        [SetUp]
        public void SetUp() => Folder = TempAssets.Folder("G5");

        [TearDown]
        public void TearDown() => TempAssets.DeleteAll();

        protected ValidationContext ContextWithScenes(params string[] scenes)
        {
            ValidationContext context = TempAssets.Context(Folder);
            context.ScenePaths = scenes;
            return context;
        }

        protected static List<Violation> Run(ValidationContext context) => new SceneAndPrefabRule().Run(context).ToList();

        protected static void AssertOneViolation(List<Violation> violations, string assetPath, string messagePart)
        {
            Assert.That(violations, Has.Count.EqualTo(1), string.Join("\n", violations));
            Assert.That(violations[0].RuleId, Is.EqualTo("G-5"));
            Assert.That(violations[0].Path, Is.EqualTo(assetPath));
            Assert.That(violations[0].IsWarning, Is.False);
            Assert.That(violations[0].Message, Does.Contain(messagePart));
        }

        /// <summary>
        /// A gradient of <paramref name="colors"/> color keys of different colors and <paramref name="alphas"/> alpha keys
        /// of different alphas. One key of a kind is a flat gradient, which Unity stores as two equal keys.
        /// </summary>
        protected static Gradient GradientOf(int colors, int alphas)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                Enumerable.Range(0, colors)
                    .Select(i => new GradientColorKey(i == 0 ? Color.white : Color.HSVToRGB(i / (float)colors, 1f, 1f), Position(i, colors))).ToArray(),
                Enumerable.Range(0, alphas)
                    .Select(i => new GradientAlphaKey(1f - 0.25f * i, Position(i, alphas))).ToArray());
            return gradient;
        }

        static float Position(int index, int count) => count == 1 ? 0f : index / (float)(count - 1);
    }
}
