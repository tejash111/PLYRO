using System.Collections.Generic;
using System.Linq;
using Bowling.Tooling.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Bowling.Tooling.Tests
{
    /// <summary>
    /// G-5 on scenes (foundation spec, section 8.1): a scene in the build needs flat ambient light, no fog and no
    /// skybox, and the camera and renderer checks hold inside it. Each fail case builds one temporary scene with one
    /// fault and expects exactly one violation naming the rule and that scene. The editor's open scenes, the active
    /// scene and the dirty flags must be as the rule found them.
    /// </summary>
    public class SceneAndPrefabRuleTests : G5RuleTestBase
    {
        [Test]
        public void RuleIdIsG5()
        {
            Assert.That(new SceneAndPrefabRule().Id, Is.EqualTo("G-5"));
        }

        // --- Scenes: fail ---

        [Test]
        public void ASceneWithTrilightAmbientFails()
        {
            string path = TempAssets.SavedScene(Folder, "Trilight", _ => RenderSettings.ambientMode = AmbientMode.Trilight);

            AssertOneViolation(Run(ContextWithScenes(path)), path, "ambientMode");
        }

        [Test]
        public void ASceneWithSkyboxAmbientFails()
        {
            string path = TempAssets.SavedScene(Folder, "SkyAmbient", _ => RenderSettings.ambientMode = AmbientMode.Skybox);

            AssertOneViolation(Run(ContextWithScenes(path)), path, "ambientMode");
        }

        [Test]
        public void ASceneWithFogOnFails()
        {
            string path = TempAssets.SavedScene(Folder, "Fog", _ => RenderSettings.fog = true);

            AssertOneViolation(Run(ContextWithScenes(path)), path, "fog");
        }

        [Test]
        public void ASceneWithASkyboxMaterialFails()
        {
            Material sky = TempAssets.SkyboxMaterial(Folder);
            string path = TempAssets.SavedScene(Folder, "Skybox", _ => RenderSettings.skybox = sky);

            AssertOneViolation(Run(ContextWithScenes(path)), path, "skybox");
        }

        [Test]
        public void ACameraThatClearsToTheSkyboxInASceneFails()
        {
            string path = TempAssets.SavedScene(Folder, "SkyCamera", _ =>
                new GameObject("Cam").AddComponent<Camera>().clearFlags = CameraClearFlags.Skybox);

            AssertOneViolation(Run(ContextWithScenes(path)), path, "clearFlags");
        }

        [Test]
        public void ALineRendererWithTwoDifferentColorsInASceneFails()
        {
            string path = TempAssets.SavedScene(Folder, "SceneLine", _ =>
                new GameObject("Line").AddComponent<LineRenderer>().colorGradient = GradientOf(2, 1));

            AssertOneViolation(Run(ContextWithScenes(path)), path, "LineRenderer");
        }

        [Test]
        public void EachFaultOfASceneIsItsOwnViolation()
        {
            string path = TempAssets.SavedScene(Folder, "Many", _ =>
            {
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.fog = true;
            });

            List<Violation> violations = Run(ContextWithScenes(path));

            Assert.That(violations, Has.Count.EqualTo(2), string.Join("\n", violations));
            Assert.That(violations.Select(v => v.Path), Is.All.EqualTo(path));
        }

        [Test]
        public void ASceneWithAMissingFileIsOneViolation()
        {
            string path = Folder + "/Nope.unity";

            AssertOneViolation(Run(ContextWithScenes(path)), path, "not found");
        }

        // --- Scenes: pass ---

        [Test]
        public void ACleanSceneWithAOneKeyLineRendererAndASolidCameraPasses()
        {
            string path = TempAssets.SavedScene(Folder, "Clean", _ =>
            {
                new GameObject("Line").AddComponent<LineRenderer>().colorGradient = GradientOf(1, 1);
                new GameObject("Cam").AddComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            });

            List<Violation> violations = Run(ContextWithScenes(path));

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        [Test]
        public void TheRealMainSceneAlonePasses()
        {
            List<Violation> violations = Run(ContextWithScenes(MainScene));

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        [Test]
        public void TheRealProjectPassesWithItsBuildScenesAndPrefabs()
        {
            ValidationContext context = TempAssets.Context(ValidationContext.DefaultAssetSearchRoot);
            context.ScenePaths = null; // the enabled scenes of the build settings

            List<Violation> violations = Run(context);

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        [Test]
        public void APrefabInstanceInASceneIsJudgedThroughTheScene()
        {
            string prefab = TempAssets.SavedPrefab(Folder, "SkyCam", root =>
                root.AddComponent<Camera>().clearFlags = CameraClearFlags.Skybox);
            string scene = TempAssets.SavedScene(Folder, "WithInstance", s =>
                PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefab), s));
            ValidationContext context = ContextWithScenes(scene);
            context.AssetSearchRoot = TempAssets.Folder("G5-empty"); // judge the scene alone

            List<Violation> violations = Run(context);

            AssertOneViolation(violations, scene, "clearFlags");
        }

        // --- The editor's scenes stay as they were ---

        [Test]
        public void TheRuleLeavesTheOpenScenesTheActiveSceneAndTheDirtyFlagsAsItFoundThem()
        {
            string path = TempAssets.SavedScene(Folder, "Probe", _ => { });
            ValidationContext context = ContextWithScenes(path, MainScene);
            Scene active = SceneManager.GetActiveScene();
            int count = SceneManager.sceneCount;
            bool[] dirty = Enumerable.Range(0, count).Select(i => SceneManager.GetSceneAt(i).isDirty).ToArray();

            Run(context);

            Assert.That(SceneManager.sceneCount, Is.EqualTo(count));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(active));
            Assert.That(Enumerable.Range(0, count).Select(i => SceneManager.GetSceneAt(i).isDirty), Is.EqualTo(dirty));
            Assert.That(SceneManager.GetSceneByPath(path).isLoaded, Is.False);
            Assert.That(SceneManager.GetSceneByPath(MainScene).isLoaded, Is.False);
        }

        [Test]
        public void ASceneThatIsAlreadyOpenStaysOpenAndTheActiveSceneIsKept()
        {
            string path = TempAssets.SavedScene(Folder, "Open", _ => { });
            Scene active = SceneManager.GetActiveScene();
            Scene opened = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                Run(ContextWithScenes(path));

                Assert.That(opened.isLoaded, Is.True);
                Assert.That(opened.isDirty, Is.False);
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(active));
            }
            finally
            {
                EditorSceneManager.CloseScene(opened, true);
            }
        }
    }
}
