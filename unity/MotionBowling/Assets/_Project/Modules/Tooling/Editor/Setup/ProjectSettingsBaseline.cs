using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bowling.Tooling.Editor
{
    /// <summary>
    /// Applies the project's player, editor and time settings (foundation spec, sections 3.1 to 3.3), so the
    /// settings live in code and not in somebody's Inspector clicks. Every method is idempotent: a second run
    /// finds the values already in place and leaves the settings files unchanged. Run it headless with
    /// <c>tools/ci/unity.sh exec Bowling.Tooling.Editor.ProjectSettingsBaseline.ApplyAllBatch</c>.
    /// </summary>
    public static class ProjectSettingsBaseline
    {
        public const string CameraUsageDescription =
            "Motion Bowling uses the camera to see your bowling swing. Video stays on this Mac and is never saved.";

        public const string CompanyName = "Shava Studios";
        public const string ProductName = "Motion Bowling";
        public const string BundleIdentifier = "ai.shavastudios.motionbowling";
        public const string MinimumMacOsVersion = "14.0";
        public const string VisibleMetaFilesMode = "Visible Meta Files";

        /// <summary>Fixed Timestep, 1/240 s (GC-6).</summary>
        public const double FixedTimestepSeconds = 1.0 / 240.0;

        /// <summary>Maximum Allowed Timestep, 1/30 s.</summary>
        public const float MaximumTimestepSeconds = 1f / 30f;

        /// <summary>The value of <c>activeInputHandler</c> for "Input System Package (New)" only.</summary>
        public const int InputSystemOnly = 1;

        const int DefaultWidth = 1920;
        const int DefaultHeight = 1080;

        // Serialized keys that have no public PlayerSettings API (docs/dev-setup.md, "Serialized PlayerSettings keys").
        const string CameraUsageKey = "cameraUsageDescription";
        const string MacMinimumVersionKey = "macOSTargetOSVersion";
        const string InputHandlerKey = "activeInputHandler";

        const string ProjectSettingsAssetPath = "ProjectSettings/ProjectSettings.asset";
        const string TimeManagerAssetPath = "ProjectSettings/TimeManager.asset";

        // The property names in TimeManager.asset.
        const string FixedTimestepName = "Fixed Timestep";
        const string MaximumTimestepName = "Maximum Allowed Timestep";

        /// <summary>Applies sections 3.1 (player), 3.2 (editor) and 3.3 (time) of the foundation spec.</summary>
        public static void ApplyPlayerEditorAndTime()
        {
            ApplyPlayer();
            ApplyEditor();
            ApplyTime();
        }

        /// <summary>
        /// Batch entry point: applies every baseline, saves the assets, then ends the editor with exit code 0.
        /// On any exception it logs it and ends the editor with exit code 1.
        /// </summary>
        public static void ApplyAllBatch()
        {
            int exitCode = 0;
            try
            {
                ApplyPlayerEditorAndTime();
                AssetDatabase.SaveAssets();
                Debug.Log("[ProjectSettingsBaseline] applied the player, editor and time settings");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[ProjectSettingsBaseline] failed: {exception}");
                exitCode = 1;
            }
            EditorApplication.Exit(exitCode);
        }

        static void ApplyPlayer()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, BundleIdentifier);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneOSX, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneOSX, new[] { GraphicsDeviceType.Metal });
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultScreenWidth = DefaultWidth;
            PlayerSettings.defaultScreenHeight = DefaultHeight;
            PlayerSettings.resizableWindow = false;

            var playerSettings = new SerializedObject(LoadSettingsAsset(ProjectSettingsAssetPath));
            Property(playerSettings, CameraUsageKey).stringValue = CameraUsageDescription;
            Property(playerSettings, MacMinimumVersionKey).stringValue = MinimumMacOsVersion;
            Property(playerSettings, InputHandlerKey).intValue = InputSystemOnly;
            playerSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ApplyEditor()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            VersionControlSettings.mode = VisibleMetaFilesMode;
            // Domain reload off, scene reload on. SOT-2 bans static state, so this is safe, and code that
            // leaks static state shows it at once.
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        }

        static void ApplyTime()
        {
            var timeManager = new SerializedObject(LoadSettingsAsset(TimeManagerAssetPath));
            bool foundFixed = false;
            bool foundMaximum = false;
            for (SerializedProperty property = timeManager.GetIterator(); property.Next(true);)
            {
                if (property.name == FixedTimestepName)
                {
                    SetSeconds(property, FixedTimestepSeconds);
                    foundFixed = true;
                }
                else if (property.name == MaximumTimestepName)
                {
                    SetSeconds(property, MaximumTimestepSeconds);
                    foundMaximum = true;
                }
            }
            if (!foundFixed || !foundMaximum)
            {
                throw new InvalidOperationException(
                    $"{TimeManagerAssetPath} has no '{FixedTimestepName}' or no '{MaximumTimestepName}' property");
            }
            timeManager.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Sets a time property to <paramref name="seconds"/>. Unity 6 stores the fixed timestep as a rational
        /// time (a tick count over a ticks-per-second rate) and the maximum timestep as a float.
        /// </summary>
        static void SetSeconds(SerializedProperty property, double seconds)
        {
            if (property.propertyType == SerializedPropertyType.Float)
            {
                property.floatValue = (float)seconds;
                return;
            }
            SerializedProperty count = property.FindPropertyRelative("m_Count");
            SerializedProperty numerator = property.FindPropertyRelative("m_Rate.m_Numerator");
            SerializedProperty denominator = property.FindPropertyRelative("m_Rate.m_Denominator");
            if (count == null || numerator == null || denominator == null)
            {
                throw new InvalidOperationException(
                    $"Time property '{property.propertyPath}' is {property.propertyType}, not a float or a rational time");
            }
            double ticksPerSecond = (double)numerator.longValue / denominator.longValue;
            count.longValue = (long)Math.Round(seconds * ticksPerSecond);
        }

        static UnityEngine.Object LoadSettingsAsset(string path)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (assets.Length == 0) throw new InvalidOperationException($"Could not load {path}");
            return assets[0];
        }

        static SerializedProperty Property(SerializedObject settings, string key)
        {
            SerializedProperty property = settings.FindProperty(key);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"{settings.targetObject.GetType().Name} has no serialized key '{key}'. " +
                    "Look it up again as described in docs/dev-setup.md.");
            }
            return property;
        }
    }
}
