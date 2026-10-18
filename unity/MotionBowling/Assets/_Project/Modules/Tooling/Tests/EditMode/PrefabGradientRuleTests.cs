using System.Collections.Generic;
using Bowling.Tooling.Editor;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Bowling.Tooling.Tests
{
    /// <summary>
    /// G-5 on prefabs (foundation spec, section 8.1): a prefab under the search root has no camera that clears to the
    /// skybox and no line or trail renderer whose gradient is not one flat color. Each fail case builds one temporary
    /// prefab with one fault and expects exactly one violation naming the rule and that prefab.
    /// </summary>
    public class PrefabGradientRuleTests : G5RuleTestBase
    {
        [Test]
        public void APrefabWhoseCameraClearsToTheSkyboxFails()
        {
            string path = TempAssets.SavedPrefab(Folder, "SkyCam", root =>
                root.AddComponent<Camera>().clearFlags = CameraClearFlags.Skybox);

            AssertOneViolation(Run(ContextWithScenes()), path, "clearFlags");
        }

        [Test]
        public void ASkyboxCameraOnAnInactiveChildIsFound()
        {
            string path = TempAssets.SavedPrefab(Folder, "DeepCam", root =>
            {
                var child = new GameObject("Child");
                child.transform.SetParent(root.transform);
                child.AddComponent<Camera>().clearFlags = CameraClearFlags.Skybox;
                child.SetActive(false);
            });

            List<Violation> violations = Run(ContextWithScenes());

            AssertOneViolation(violations, path, "DeepCam/Child");
        }

        [Test]
        public void APrefabLineRendererWithTwoDifferentColorsFails()
        {
            string path = TempAssets.SavedPrefab(Folder, "TwoColors", root =>
                root.AddComponent<LineRenderer>().colorGradient = GradientOf(2, 1));

            AssertOneViolation(Run(ContextWithScenes()), path, "LineRenderer");
        }

        [Test]
        public void APrefabLineRendererWithTwoDifferentAlphasFails()
        {
            string path = TempAssets.SavedPrefab(Folder, "TwoAlphas", root =>
                root.AddComponent<LineRenderer>().colorGradient = GradientOf(1, 2));

            AssertOneViolation(Run(ContextWithScenes()), path, "different alphas");
        }

        [Test]
        public void APrefabTrailRendererWithTwoDifferentColorsFails()
        {
            string path = TempAssets.SavedPrefab(Folder, "Trail", root =>
                root.AddComponent<TrailRenderer>().colorGradient = GradientOf(2, 1));

            AssertOneViolation(Run(ContextWithScenes()), path, "TrailRenderer");
        }

        [Test]
        public void ARendererWithManyColorsAndAlphasIsOneViolation()
        {
            string path = TempAssets.SavedPrefab(Folder, "Both", root =>
                root.AddComponent<LineRenderer>().colorGradient = GradientOf(3, 2));

            AssertOneViolation(Run(ContextWithScenes()), path, "3 different colors");
        }

        [Test]
        public void APrefabLineRendererWithOneColorKeyAndOneAlphaKeyPasses()
        {
            TempAssets.SavedPrefab(Folder, "OneKey", root =>
                root.AddComponent<LineRenderer>().colorGradient = GradientOf(1, 1));

            List<Violation> violations = Run(ContextWithScenes());

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        [Test]
        public void ATwoKeyGradientOfOneColorAndOneAlphaPasses()
        {
            // Two keys, but the same color and alpha at both: flat along the line, so not a gradient.
            TempAssets.SavedPrefab(Folder, "TwoEqualKeys", root =>
            {
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(Color.red, 0f), new GradientColorKey(Color.red, 1f) },
                    new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0.5f, 1f) });
                root.AddComponent<LineRenderer>().colorGradient = gradient;
            });

            List<Violation> violations = Run(ContextWithScenes());

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        [Test]
        public void ADefaultLineRendererPasses()
        {
            TempAssets.SavedPrefab(Folder, "DefaultLine", root => root.AddComponent<LineRenderer>());

            List<Violation> violations = Run(ContextWithScenes());

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        [Test]
        public void APrefabCameraThatClearsToASolidColorPasses()
        {
            TempAssets.SavedPrefab(Folder, "SolidCam", root =>
                root.AddComponent<Camera>().clearFlags = CameraClearFlags.SolidColor);

            List<Violation> violations = Run(ContextWithScenes());

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        [Test]
        public void ARenderSettingsFaultDoesNotApplyToAPrefab()
        {
            // A prefab has no RenderSettings of its own: the ambient mode of whichever scene is active must not leak in.
            TempAssets.SavedPrefab(Folder, "Plain", _ => { });
            string trilight = TempAssets.SavedScene(Folder, "Active", _ => RenderSettings.ambientMode = AmbientMode.Trilight);
            Scene opened = EditorSceneManager.OpenScene(trilight, OpenSceneMode.Additive);
            Scene previous = SceneManager.GetActiveScene();
            try
            {
                SceneManager.SetActiveScene(opened);

                List<Violation> violations = Run(ContextWithScenes());

                Assert.That(violations, Is.Empty, string.Join("\n", violations));
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(opened, true);
            }
        }
    }
}
