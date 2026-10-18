using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Bowling.Tooling.Editor
{
    /// <summary>
    /// The scene half of the graphics baseline (foundation spec, sections 3.4 and 3.5): the global volume profile
    /// (Tonemapping Neutral and nothing else), the Rendering prefab (Main Camera and Global Volume) and Main.unity
    /// (flat ambient light, no skybox, no fog, custom reflections without a cubemap, one Rendering instance).
    /// Each step first checks what it owns and rewrites only that when it is wrong, so a second run changes nothing
    /// and a later task's additions (the Bootstrap root, more children in the prefab) survive a re-run.
    /// </summary>
    public static class MainSceneBaseline
    {
        public const string GlobalVolumeProfilePath = RenderingBaseline.SettingsFolder + "/GlobalVolumeProfile.asset";
        public const string PrefabFolder = "Assets/_Project/Prefabs/Rendering";
        public const string PrefabPath = PrefabFolder + "/Rendering.prefab";
        public const string SceneFolder = "Assets/_Project/Scenes";
        public const string ScenePath = SceneFolder + "/Main.unity";

        /// <summary>Palette.Background from the presentation spec; the spec says to use this until it exists.</summary>
        public static readonly Color32 BackgroundColor = new Color32(0x14, 0x16, 0x1F, 0xFF);

        /// <summary>The flat ambient light: <see cref="BackgroundColor"/> brightened 2x.</summary>
        public static readonly Color32 AmbientColor = new Color32(0x28, 0x2C, 0x3E, 0xFF);

        const string RootName = "Rendering";
        const string CameraName = "Main Camera";
        const string VolumeName = "Global Volume";
        const string MainCameraTag = "MainCamera";

        /// <summary>Applies the volume profile, the Rendering prefab, Main.unity and the build settings.</summary>
        public static void Apply()
        {
            VolumeProfile profile = EnsureGlobalVolumeProfile();
            RenderingBaseline.EnsureFolder(PrefabFolder);
            GameObject prefab = EnsureRenderingPrefab(profile);
            RenderingBaseline.EnsureFolder(SceneFolder);
            EnsureMainScene(prefab);
            EnsureBuildSettings();
        }

        // --- The global volume profile ---

        static VolumeProfile EnsureGlobalVolumeProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(GlobalVolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, GlobalVolumeProfilePath);
            }
            if (HoldsOnlyNeutralTonemapping(profile)) return profile;

            foreach (VolumeComponent stale in profile.components.Where(component => component != null).ToList())
            {
                AssetDatabase.RemoveObjectFromAsset(stale);
                UnityEngine.Object.DestroyImmediate(stale, true);
            }
            profile.components.Clear();

            // One override: Tonemapping, with only its mode overridden. Add(false) leaves every parameter
            // un-overridden, so Unity's defaults stand for everything else.
            var tonemapping = profile.Add<Tonemapping>(false);
            tonemapping.mode.Override(TonemappingMode.Neutral);
            AssetDatabase.AddObjectToAsset(tonemapping, profile);
            EditorUtility.SetDirty(tonemapping);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        static bool HoldsOnlyNeutralTonemapping(VolumeProfile profile)
        {
            return profile.components.Count == 1
                && profile.components[0] is Tonemapping tonemapping
                && tonemapping.active
                && tonemapping.mode.overrideState
                && tonemapping.mode.value == TonemappingMode.Neutral
                && tonemapping.parameters.Count(parameter => parameter.overrideState) == 1;
        }

        // --- The Rendering prefab ---

        static GameObject EnsureRenderingPrefab(VolumeProfile profile)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null && PrefabMatches(prefab, profile)) return prefab;

            bool exists = prefab != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject(RootName);
            try
            {
                ConfigureCamera(GetOrCreateChild(root.transform, CameraName));
                ConfigureVolume(GetOrCreateChild(root.transform, VolumeName), profile);
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static bool PrefabMatches(GameObject root, VolumeProfile profile)
        {
            Transform cameraTransform = root.transform.Find(CameraName);
            Transform volumeTransform = root.transform.Find(VolumeName);
            if (cameraTransform == null || volumeTransform == null) return false;

            var camera = cameraTransform.GetComponent<Camera>();
            var cameraData = cameraTransform.GetComponent<UniversalAdditionalCameraData>();
            var volume = volumeTransform.GetComponent<Volume>();
            return camera != null && cameraData != null && volume != null
                && cameraTransform.CompareTag(MainCameraTag)
                && camera.clearFlags == CameraClearFlags.SolidColor
                && Same(camera.backgroundColor, BackgroundColor)
                && cameraData.renderPostProcessing
                && volume.isGlobal
                && volume.sharedProfile == profile;
        }

        static Transform GetOrCreateChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null) return child;
            child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        static void ConfigureCamera(Transform cameraTransform)
        {
            cameraTransform.gameObject.tag = MainCameraTag;
            if (!cameraTransform.TryGetComponent(out Camera camera)) camera = cameraTransform.gameObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }

        static void ConfigureVolume(Transform volumeTransform, VolumeProfile profile)
        {
            if (!volumeTransform.TryGetComponent(out Volume volume)) volume = volumeTransform.gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        // --- Main.unity ---

        static void EnsureMainScene(GameObject prefab)
        {
            bool exists = RenderingBaseline.AssetExists(ScenePath);
            Scene scene = exists
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            bool changed = !exists;
            if (!LightingMatches())
            {
                ApplyLighting();
                changed = true;
            }
            if (!HasRenderingInstance(scene, prefab))
            {
                PrefabUtility.InstantiatePrefab(prefab, scene);
                changed = true;
            }
            if (changed && !EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException($"Could not save {ScenePath}");
        }

        static bool LightingMatches()
        {
            return RenderSettings.ambientMode == AmbientMode.Flat
                && Same(RenderSettings.ambientLight, AmbientColor)
                && RenderSettings.skybox == null
                && !RenderSettings.fog
                && RenderSettings.defaultReflectionMode == DefaultReflectionMode.Custom
                && RenderSettings.customReflectionTexture == null;
        }

        static void ApplyLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = AmbientColor;
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = null;
        }

        static bool HasRenderingInstance(Scene scene, GameObject prefab)
        {
            return scene.GetRootGameObjects().Any(root =>
                PrefabUtility.IsAnyPrefabInstanceRoot(root)
                && PrefabUtility.GetCorrespondingObjectFromSource(root) == prefab);
        }

        // --- Build settings ---

        static void EnsureBuildSettings()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 1 && scenes[0].enabled && scenes[0].path == ScenePath) return;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        // Colors compare by their 8-bit channels, which is how they are authored.
        static bool Same(Color color, Color32 expected)
        {
            Color32 actual = color;
            return actual.r == expected.r && actual.g == expected.g && actual.b == expected.b && actual.a == expected.a;
        }
    }
}
