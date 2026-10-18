using System.Collections.Generic;
using System.IO;
using Bowling.Tooling.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Bowling.Tooling.Tests
{
    /// <summary>
    /// The pinned package set (foundation spec, section 3). The first four tests judge the real
    /// Packages/manifest.json. Each of the next four feeds its check an inline manifest that breaks
    /// exactly that check, so a rule that stopped reporting would fail here, not stay quietly green.
    /// </summary>
    public class PackageManifestTests
    {
        static string ManifestPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Packages", "manifest.json"));

        static IReadOnlyDictionary<string, string> RealDependencies() => Dependencies(File.ReadAllText(ManifestPath));

        static IReadOnlyDictionary<string, string> Dependencies(string manifestJson) =>
            JObject.Parse(manifestJson)["dependencies"].ToObject<Dictionary<string, string>>();

        // --- The real manifest ---

        [Test]
        public void RequiredPackagesPresent()
        {
            Assert.That(PackageManifestRules.MissingRequired(RealDependencies()), Is.Empty);
        }

        [Test]
        public void NoUnapprovedPackages()
        {
            Assert.That(PackageManifestRules.Unapproved(RealDependencies()), Is.Empty);
        }

        [Test]
        public void VersionsAreExact()
        {
            Assert.That(PackageManifestRules.InexactVersions(RealDependencies()), Is.Empty);
        }

        [Test]
        public void CinemachineIsMajor3()
        {
            Assert.That(PackageManifestRules.CinemachineNotMajor3(RealDependencies()), Is.Empty);
        }

        // --- Inline manifests that break one check ---

        [Test]
        public void MissingRequiredPackageIsReported()
        {
            const string json = @"{ ""dependencies"": {
                ""com.unity.inputsystem"": ""1.20.1"",
                ""com.unity.cinemachine"": ""3.1.7"",
                ""com.unity.test-framework"": ""1.6.0"",
                ""com.unity.test-framework.performance"": ""3.6.0"",
                ""com.unity.nuget.newtonsoft-json"": ""3.2.2"",
                ""com.unity.modules.physics"": ""1.0.0"" } }";

            Assert.That(PackageManifestRules.MissingRequired(Dependencies(json)),
                Is.EqualTo(new[] { "com.unity.render-pipelines.universal" }));
        }

        [Test]
        public void TimelineIsReportedAsUnapproved()
        {
            const string json = @"{ ""dependencies"": {
                ""com.unity.timeline"": ""1.8.7"",
                ""com.unity.inputsystem"": ""1.20.1"",
                ""com.unity.ide.rider"": ""3.0.31"",
                ""com.unity.modules.physics"": ""1.0.0"" } }";

            Assert.That(PackageManifestRules.Unapproved(Dependencies(json)),
                Is.EqualTo(new[] { "com.unity.timeline" }));
        }

        [Test]
        public void VersionRangeIsReportedAsInexact()
        {
            const string json = @"{ ""dependencies"": {
                ""com.unity.inputsystem"": ""^1.2.0"",
                ""com.unity.cinemachine"": ""3.1.7"",
                ""com.unity.nuget.newtonsoft-json"": ""3.2.2-pre.1"" } }";

            Assert.That(PackageManifestRules.InexactVersions(Dependencies(json)),
                Is.EqualTo(new[] { "com.unity.inputsystem" }));
        }

        [Test]
        public void CinemachineVersion2IsReported()
        {
            const string json = @"{ ""dependencies"": { ""com.unity.cinemachine"": ""2.10.1"" } }";

            Assert.That(PackageManifestRules.CinemachineNotMajor3(Dependencies(json)),
                Is.EqualTo(new[] { "com.unity.cinemachine" }));
        }
    }
}
