using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Bowling.Tooling.Editor
{
    /// <summary>
    /// The render pipeline half of the graphics baseline (foundation spec, section 3.4): the URP asset and its
    /// renderer, where Unity's generated URP settings assets live, and the single "Booth" quality level.
    /// <see cref="MainSceneBaseline"/> does the rest (the volume profile, the Rendering prefab and Main.unity).
    /// Every step checks the current state first and writes only when it differs, so a second run changes nothing.
    /// </summary>
    public static class RenderingBaseline
    {
        public const string SettingsFolder = "Assets/_Project/Settings";
        public const string UrpAssetPath = SettingsFolder + "/URP-Booth.asset";
        public const string RendererPath = SettingsFolder + "/URP-Booth_Renderer.asset";
        public const string GlobalSettingsPath = SettingsFolder + "/UniversalRenderPipelineGlobalSettings.asset";
        public const string DefaultVolumeProfilePath = SettingsFolder + "/DefaultVolumeProfile.asset";
        public const string QualityLevelName = "Booth";

        // Where Unity writes the URP global settings and its default volume profile (URPDefaultVolumeProfileSettings).
        const string GeneratedGlobalSettingsPath = "Assets/UniversalRenderPipelineGlobalSettings.asset";
        const string GeneratedDefaultVolumeProfilePath = "Assets/DefaultVolumeProfile.asset";

        // The post-process data URP's own "Create > URP Asset" menu gives a new renderer. Without it the
        // renderer skips post-processing altogether.
        const string PostProcessDataPath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

        const string QualitySettingsAssetPath = "ProjectSettings/QualitySettings.asset";

        // The level whose other values Booth inherits, when the project still has Unity's six default levels.
        const string InheritedQualityLevelName = "High";

        const int VSyncEveryVBlank = 1;

        /// <summary>Applies the URP asset, the settings asset locations and the quality level.</summary>
        public static void Apply()
        {
            EnsureFolder(SettingsFolder);
            UniversalRenderPipelineAsset urp = EnsureUrpAsset();
            if (GraphicsSettings.defaultRenderPipeline != urp) GraphicsSettings.defaultRenderPipeline = urp;
            // Assigning the URP asset is what makes Unity generate its global settings (if they are missing),
            // so the move comes after it. MoveAsset keeps the GUIDs, so GraphicsSettings still finds them.
            MoveIntoSettings(GeneratedGlobalSettingsPath, GlobalSettingsPath);
            MoveIntoSettings(GeneratedDefaultVolumeProfilePath, DefaultVolumeProfilePath);
            ApplyQuality(urp);
        }

        /// <summary>Creates <paramref name="folder"/> and any missing parents, so each gets a .meta file.</summary>
        internal static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) throw new InvalidOperationException($"No parent folder for {folder}");
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        internal static bool AssetExists(string path)
        {
            return AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets).Length > 0;
        }

        static UniversalRenderPipelineAsset EnsureUrpAsset()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }
            if (renderer.postProcessData == null)
            {
                renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(PostProcessDataPath);
                if (renderer.postProcessData == null)
                    throw new InvalidOperationException($"URP's default post-process data is missing at {PostProcessDataPath}");
                EditorUtility.SetDirty(renderer);
            }

            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
            if (urp == null)
            {
                urp = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(urp, UrpAssetPath);
            }
            return urp;
        }

        static void MoveIntoSettings(string generatedPath, string settingsPath)
        {
            if (AssetExists(settingsPath)) return;
            if (!AssetExists(generatedPath))
            {
                throw new InvalidOperationException(
                    $"Neither {generatedPath} nor {settingsPath} exists, so there is nothing to move into the settings folder");
            }
            string error = AssetDatabase.MoveAsset(generatedPath, settingsPath);
            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException($"Could not move {generatedPath} to {settingsPath}: {error}");
        }

        /// <summary>
        /// Leaves one quality level, "Booth", on the URP asset with vsync every V blank. Booth keeps the values of
        /// Unity's "High" level (URP takes its shadow and anti-aliasing settings from the URP asset anyway).
        /// </summary>
        static void ApplyQuality(UniversalRenderPipelineAsset urp)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(QualitySettingsAssetPath);
            if (assets.Length == 0) throw new InvalidOperationException($"Could not load {QualitySettingsAssetPath}");
            var quality = new SerializedObject(assets[0]);

            SerializedProperty levels = quality.FindProperty("m_QualitySettings");
            if (levels == null || levels.arraySize == 0)
                throw new InvalidOperationException($"{QualitySettingsAssetPath} has no quality levels");
            int keep = IndexOfLevel(levels, QualityLevelName);
            if (keep < 0) keep = IndexOfLevel(levels, InheritedQualityLevelName);
            if (keep < 0) keep = 0;
            for (int index = levels.arraySize - 1; index >= 0; index--)
            {
                if (index != keep) levels.DeleteArrayElementAtIndex(index);
            }

            SerializedProperty booth = levels.GetArrayElementAtIndex(0);
            Relative(booth, "name").stringValue = QualityLevelName;
            Relative(booth, "vSyncCount").intValue = VSyncEveryVBlank;
            Relative(booth, "customRenderPipeline").objectReferenceValue = urp;

            quality.FindProperty("m_CurrentQuality").intValue = 0;
            SerializedProperty perPlatform = quality.FindProperty("m_PerPlatformDefaultQuality");
            for (int index = 0; index < perPlatform.arraySize; index++)
                Relative(perPlatform.GetArrayElementAtIndex(index), "second").intValue = 0;

            quality.ApplyModifiedPropertiesWithoutUndo();
        }

        static int IndexOfLevel(SerializedProperty levels, string name)
        {
            for (int index = 0; index < levels.arraySize; index++)
            {
                if (Relative(levels.GetArrayElementAtIndex(index), "name").stringValue == name) return index;
            }
            return -1;
        }

        static SerializedProperty Relative(SerializedProperty parent, string name)
        {
            SerializedProperty property = parent.FindPropertyRelative(name);
            if (property == null) throw new InvalidOperationException($"{parent.propertyPath} has no '{name}' property");
            return property;
        }
    }
}
