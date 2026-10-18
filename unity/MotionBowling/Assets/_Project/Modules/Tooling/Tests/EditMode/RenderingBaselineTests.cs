using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Bowling.Tooling.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Bowling.Tooling.Tests
{
    /// <summary>
    /// The graphics, quality and lighting baseline (foundation spec, sections 3.4 and 3.5), one test per row.
    /// They read the live project, so they pass only after
    /// <c>tools/ci/unity.sh exec Bowling.Tooling.Editor.ProjectSettingsBaseline.ApplyAllBatch</c> has run.
    /// The paths and colors are written out here on purpose: the tests pin the spec, not the code under test.
    /// </summary>
    public class RenderingBaselineTests
    {
        const string SettingsFolder = "Assets/_Project/Settings/";
        const string UrpAssetPath = SettingsFolder + "URP-Booth.asset";
        const string RendererPath = SettingsFolder + "URP-Booth_Renderer.asset";
        const string GlobalSettingsPath = SettingsFolder + "UniversalRenderPipelineGlobalSettings.asset";
        const string DefaultVolumeProfilePath = SettingsFolder + "DefaultVolumeProfile.asset";
        const string GlobalVolumeProfilePath = SettingsFolder + "GlobalVolumeProfile.asset";
        const string PrefabPath = "Assets/_Project/Prefabs/Rendering/Rendering.prefab";
        const string MainScenePath = "Assets/_Project/Scenes/Main.unity";

        bool _openedScene;

        [TearDown]
        public void LeaveNoSceneBehind()
        {
            if (!_openedScene) return;
            _openedScene = false;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, $"No {typeof(T).Name} at {path}");
            return asset;
        }

        Scene OpenMainScene()
        {
            _openedScene = true;
            return EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        }

        // --- 3.4 The URP asset and the quality level ---

        [Test]
        public void DefaultRenderPipelineIsTheBoothUrpAsset()
        {
            RenderPipelineAsset asset = GraphicsSettings.defaultRenderPipeline;
            Assert.That(asset, Is.InstanceOf<UniversalRenderPipelineAsset>());
            Assert.That(AssetDatabase.GetAssetPath(asset), Is.EqualTo(UrpAssetPath));
        }

        [Test]
        public void UrpAssetUsesItsOwnRendererWithPostProcessing()
        {
            var urp = new SerializedObject(Load<UniversalRenderPipelineAsset>(UrpAssetPath));
            SerializedProperty renderers = urp.FindProperty("m_RendererDataList");
            Assert.That(renderers.arraySize, Is.EqualTo(1));
            Object renderer = renderers.GetArrayElementAtIndex(0).objectReferenceValue;
            Assert.That(AssetDatabase.GetAssetPath(renderer), Is.EqualTo(RendererPath));
            // Without post-process data the renderer skips post-processing, and Tonemapping would do nothing.
            Assert.That(new SerializedObject(renderer).FindProperty("postProcessData").objectReferenceValue,
                Is.Not.Null);
        }

        [Test]
        public void QualityHasOneLevelNamedBooth()
        {
            Assert.That(QualitySettings.names, Is.EqualTo(new[] { "Booth" }));
        }

        [Test]
        public void VSyncIsEveryVBlank()
        {
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(1));
        }

        [Test]
        public void QualityUsesTheDefaultUrpAsset()
        {
            Assert.That(QualitySettings.renderPipeline, Is.Not.Null);
            Assert.That(QualitySettings.renderPipeline, Is.SameAs(GraphicsSettings.defaultRenderPipeline));
        }

        // --- 3.4 Where the settings assets live ---

        [Test]
        public void UrpGlobalSettingsLiveInProjectSettings()
        {
            RenderPipelineGlobalSettings settings = GraphicsSettings.GetSettingsForRenderPipeline<UniversalRenderPipeline>();
            Assert.That(settings, Is.Not.Null);
            string path = AssetDatabase.GetAssetPath(settings);
            Assert.That(path, Does.StartWith(SettingsFolder));
            Assert.That(path, Is.EqualTo(GlobalSettingsPath));
        }

        [Test]
        public void DefaultVolumeProfileLivesInProjectSettings()
        {
            var settings = GraphicsSettings.GetRenderPipelineSettings<URPDefaultVolumeProfileSettings>();
            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.volumeProfile, Is.Not.Null);
            string path = AssetDatabase.GetAssetPath(settings.volumeProfile);
            Assert.That(path, Does.StartWith(SettingsFolder));
            Assert.That(path, Is.EqualTo(DefaultVolumeProfilePath));
        }

        [Test]
        public void NoUnityGeneratedSettingsAssetIsLeftAtTheAssetsRoot()
        {
            Assert.That(AssetDatabase.AssetPathToGUID("Assets/UniversalRenderPipelineGlobalSettings.asset"), Is.Empty);
            Assert.That(AssetDatabase.AssetPathToGUID("Assets/DefaultVolumeProfile.asset"), Is.Empty);
        }

        // --- 3.4 The global volume profile ---

        [Test]
        public void GlobalVolumeProfileHoldsOnlyAnActiveNeutralTonemapping()
        {
            var profile = Load<VolumeProfile>(GlobalVolumeProfilePath);
            Assert.That(profile.components, Has.Count.EqualTo(1));
            Assert.That(profile.components[0], Is.InstanceOf<Tonemapping>());
            var tonemapping = (Tonemapping)profile.components[0];
            Assert.That(tonemapping.active, Is.True);
            Assert.That(tonemapping.mode.overrideState, Is.True);
            Assert.That(tonemapping.mode.value, Is.EqualTo(TonemappingMode.Neutral));
        }

        [Test]
        public void GlobalVolumeProfileOverridesNothingButTheTonemappingMode()
        {
            var tonemapping = (Tonemapping)Load<VolumeProfile>(GlobalVolumeProfilePath).components.Single();
            int overridden = tonemapping.parameters.Count(parameter => parameter.overrideState);
            Assert.That(overridden, Is.EqualTo(1));
        }

        // --- 3.4 The camera and the volume, as the Rendering prefab ---

        [Test]
        public void RenderingPrefabHoldsTheMainCameraAndTheGlobalVolume()
        {
            GameObject root = Load<GameObject>(PrefabPath);
            Assert.That(root.name, Is.EqualTo("Rendering"));
            string[] children = root.transform.Cast<Transform>().Select(child => child.name).OrderBy(name => name).ToArray();
            Assert.That(children, Is.EqualTo(new[] { "Global Volume", "Main Camera" }));
        }

        [Test]
        public void MainCameraClearsToTheSolidBackgroundColor()
        {
            Transform cameraTransform = Load<GameObject>(PrefabPath).transform.Find("Main Camera");
            Assert.That(cameraTransform, Is.Not.Null);
            Assert.That(cameraTransform.gameObject.CompareTag("MainCamera"), Is.True);
            var camera = cameraTransform.GetComponent<Camera>();
            Assert.That(camera, Is.Not.Null);
            Assert.That(camera.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            Assert.That((Color32)camera.backgroundColor, Is.EqualTo(new Color32(0x14, 0x16, 0x1F, 0xFF)));
        }

        [Test]
        public void MainCameraRendersPostProcessing()
        {
            Transform cameraTransform = Load<GameObject>(PrefabPath).transform.Find("Main Camera");
            Assert.That(cameraTransform, Is.Not.Null);
            var additionalData = cameraTransform.GetComponent<UniversalAdditionalCameraData>();
            Assert.That(additionalData, Is.Not.Null);
            Assert.That(additionalData.renderPostProcessing, Is.True);
        }

        [Test]
        public void GlobalVolumeIsGlobalAndUsesTheGlobalProfile()
        {
            Transform volumeTransform = Load<GameObject>(PrefabPath).transform.Find("Global Volume");
            Assert.That(volumeTransform, Is.Not.Null);
            var volume = volumeTransform.GetComponent<Volume>();
            Assert.That(volume, Is.Not.Null);
            Assert.That(volume.isGlobal, Is.True);
            Assert.That(AssetDatabase.GetAssetPath(volume.sharedProfile), Is.EqualTo(GlobalVolumeProfilePath));
        }

        // --- 3.4 and 3.5 Main.unity: lighting and roots ---

        [Test]
        public void MainSceneAmbientLightIsFlat()
        {
            OpenMainScene();
            Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Flat));
        }

        [Test]
        public void MainSceneAmbientColorIsTheBackgroundBrightenedTwice()
        {
            OpenMainScene();
            Assert.That((Color32)RenderSettings.ambientLight, Is.EqualTo(new Color32(0x28, 0x2C, 0x3E, 0xFF)));
        }

        [Test]
        public void MainSceneHasNoSkyboxAndNoFog()
        {
            OpenMainScene();
            Assert.That(RenderSettings.skybox, Is.Null);
            Assert.That(RenderSettings.fog, Is.False);
        }

        [Test]
        public void MainSceneReflectionsAreCustomWithoutACubemap()
        {
            OpenMainScene();
            Assert.That(RenderSettings.defaultReflectionMode, Is.EqualTo(DefaultReflectionMode.Custom));
            Assert.That(RenderSettings.customReflectionTexture, Is.Null);
        }

        [Test]
        public void MainSceneRootsAreTheBootstrapObjectAndTheRenderingPrefabInstance()
        {
            Scene scene = OpenMainScene();
            GameObject[] roots = scene.GetRootGameObjects();
            Assert.That(roots.Select(root => root.name), Is.EquivalentTo(new[] { "Bootstrap", "Rendering" }));

            // The Bootstrap root is the one loose object spec 3.5 allows; everything else is a prefab instance.
            GameObject bootstrap = roots.Single(root => root.name == "Bootstrap");
            Assert.That(PrefabUtility.IsAnyPrefabInstanceRoot(bootstrap), Is.False);

            GameObject rendering = roots.Single(root => root.name == "Rendering");
            Assert.That(PrefabUtility.IsAnyPrefabInstanceRoot(rendering), Is.True);
            Object source = PrefabUtility.GetCorrespondingObjectFromSource(rendering);
            Assert.That(AssetDatabase.GetAssetPath(source), Is.EqualTo(PrefabPath));
        }

        // --- 3.5 Build settings ---

        [Test]
        public void MainSceneIsTheOnlyEnabledSceneInTheBuild()
        {
            string[] enabled = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            Assert.That(enabled, Is.EqualTo(new[] { MainScenePath }));
        }

        // --- Idempotence: the baseline is code, so applying it again must change nothing ---

        [Test]
        public void ApplyingTheRenderingBaselineAgainChangesNoFile()
        {
            _openedScene = true; // ApplyRendering opens Main.unity; the TearDown leaves an empty scene behind
            Dictionary<string, string> before = HashBaselineFiles();
            Assert.That(before.Keys, Does.Contain(MainScenePath + ".meta"), "the baseline has not been applied yet");

            ProjectSettingsBaseline.ApplyRendering();

            Assert.That(HashBaselineFiles(), Is.EqualTo(before));
        }

        /// <summary>The SHA-256 of every file the rendering baseline writes, by path relative to the project.</summary>
        static Dictionary<string, string> HashBaselineFiles()
        {
            string project = Path.GetDirectoryName(Application.dataPath);
            var paths = new List<string>
            {
                "ProjectSettings/QualitySettings.asset",
                "ProjectSettings/GraphicsSettings.asset",
                "ProjectSettings/EditorBuildSettings.asset",
            };
            foreach (string folder in new[] { "Assets/_Project/Settings", "Assets/_Project/Prefabs", "Assets/_Project/Scenes" })
            {
                string absolute = Path.Combine(project, folder);
                if (!Directory.Exists(absolute)) continue;
                paths.AddRange(Directory.GetFiles(absolute, "*", SearchOption.AllDirectories)
                    .Select(file => file.Substring(project.Length + 1).Replace('\\', '/')));
            }

            using SHA256 sha = SHA256.Create();
            return paths.ToDictionary(path => path, path =>
                System.BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(project, path)))));
        }
    }
}
