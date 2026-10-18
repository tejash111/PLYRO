using System;
using Bowling.Tooling.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bowling.Tooling.Tests
{
    /// <summary>
    /// The player, editor and time settings (foundation spec, sections 3.1 to 3.3), one test per row.
    /// They read the live settings, so they pass only after
    /// <c>tools/ci/unity.sh exec Bowling.Tooling.Editor.ProjectSettingsBaseline.ApplyAllBatch</c> has run.
    /// </summary>
    public class PlayerSettingsBaselineTests
    {
        const string ProjectSettingsAssetPath = "ProjectSettings/ProjectSettings.asset";

        // The keys come from docs/dev-setup.md, "Serialized PlayerSettings keys".
        const string CameraUsageKey = "cameraUsageDescription";
        const string MacMinimumVersionKey = "macOSTargetOSVersion";
        const string InputHandlerKey = "activeInputHandler";

        static SerializedProperty PlayerSettingsProperty(string key)
        {
            UnityEngine.Object playerSettings = AssetDatabase.LoadAllAssetsAtPath(ProjectSettingsAssetPath)[0];
            SerializedProperty property = new SerializedObject(playerSettings).FindProperty(key);
            Assert.That(property, Is.Not.Null, $"PlayerSettings has no serialized key '{key}'");
            return property;
        }

        // --- 3.1 Player settings ---

        [Test]
        public void CompanyNameIsShavaStudios()
        {
            Assert.That(PlayerSettings.companyName, Is.EqualTo("Shava Studios"));
        }

        [Test]
        public void ProductNameIsMotionBowling()
        {
            Assert.That(PlayerSettings.productName, Is.EqualTo("Motion Bowling"));
        }

        [Test]
        public void BundleIdentifierIsSet()
        {
            Assert.That(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Standalone),
                Is.EqualTo("ai.shavastudios.motionbowling"));
        }

        [Test]
        public void ScriptingBackendIsIl2Cpp()
        {
            Assert.That(PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone),
                Is.EqualTo(ScriptingImplementation.IL2CPP));
        }

        [Test]
        public void ApiCompatibilityIsNetStandard()
        {
            Assert.That(PlayerSettings.GetApiCompatibilityLevel(NamedBuildTarget.Standalone),
                Is.EqualTo(ApiCompatibilityLevel.NET_Standard));
        }

        [Test]
        public void GraphicsApiIsMetalOnly()
        {
            Assert.That(PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneOSX), Is.False);
            Assert.That(PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneOSX),
                Is.EqualTo(new[] { GraphicsDeviceType.Metal }));
        }

        [Test]
        public void CameraUsageDescriptionIsSet()
        {
            Assert.That(PlayerSettingsProperty(CameraUsageKey).stringValue,
                Is.EqualTo(ProjectSettingsBaseline.CameraUsageDescription));
        }

        [Test]
        public void MinimumMacOsVersionIs14()
        {
            Assert.That(PlayerSettingsProperty(MacMinimumVersionKey).stringValue, Is.EqualTo("14.0"));
        }

        [Test]
        public void ActiveInputHandlerIsInputSystemOnly()
        {
            Assert.That(PlayerSettingsProperty(InputHandlerKey).intValue, Is.EqualTo(1));
        }

        [Test]
        public void RunsInBackground()
        {
            Assert.That(PlayerSettings.runInBackground, Is.True);
        }

        [Test]
        public void IsVisibleInBackground()
        {
            Assert.That(PlayerSettings.visibleInBackground, Is.True);
        }

        [Test]
        public void FullscreenModeIsFullscreenWindow()
        {
            Assert.That(PlayerSettings.fullScreenMode, Is.EqualTo(FullScreenMode.FullScreenWindow));
        }

        [Test]
        public void DefaultResolutionIs1920By1080()
        {
            Assert.That(PlayerSettings.defaultScreenWidth, Is.EqualTo(1920));
            Assert.That(PlayerSettings.defaultScreenHeight, Is.EqualTo(1080));
        }

        [Test]
        public void WindowIsNotResizable()
        {
            Assert.That(PlayerSettings.resizableWindow, Is.False);
        }

        // --- 3.2 Editor settings ---

        [Test]
        public void SerializationIsForceText()
        {
            Assert.That(EditorSettings.serializationMode, Is.EqualTo(SerializationMode.ForceText));
        }

        [Test]
        public void MetaFilesAreVisible()
        {
            Assert.That(VersionControlSettings.mode, Is.EqualTo("Visible Meta Files"));
        }

        [Test]
        public void EnterPlayModeReloadsSceneButNotDomain()
        {
            Assert.That(EditorSettings.enterPlayModeOptionsEnabled, Is.True);
            Assert.That(EditorSettings.enterPlayModeOptions, Is.EqualTo(EnterPlayModeOptions.DisableDomainReload));
        }

        // --- 3.3 Time settings ---

        [Test]
        public void FixedTimestepIsOneOver240()
        {
            Assert.That(Math.Abs(Time.fixedDeltaTime - 1f / 240f), Is.LessThan(1e-7));
        }

        [Test]
        public void MaximumAllowedTimestepIsOneOver30()
        {
            Assert.That(Math.Abs(Time.maximumDeltaTime - 1f / 30f), Is.LessThan(1e-6));
        }
    }
}
