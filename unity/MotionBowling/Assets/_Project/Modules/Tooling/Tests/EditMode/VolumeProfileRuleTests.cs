using System.Collections.Generic;
using System.Linq;
using Bowling.Tooling.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Bowling.Tooling.Tests
{
    /// <summary>
    /// G-4 (foundation spec, section 8.1): a volume profile may not hold an active Vignette, ScreenSpaceLensFlare,
    /// FilmGrain or Bloom whose effective intensity is above 0. Each fail case builds one profile with one such
    /// override and expects exactly one violation, naming the rule and the profile asset.
    /// </summary>
    public class VolumeProfileRuleTests
    {
        string _folder;

        [SetUp]
        public void SetUp() => _folder = TempAssets.Folder("G4");

        [TearDown]
        public void TearDown() => TempAssets.DeleteAll();

        List<Violation> Run() => new VolumeProfileRule().Run(TempAssets.Context(_folder)).ToList();

        static void AssertOneViolation(List<Violation> violations, string profilePath, string messagePart)
        {
            Assert.That(violations, Has.Count.EqualTo(1), string.Join("\n", violations));
            Assert.That(violations[0].RuleId, Is.EqualTo("G-4"));
            Assert.That(violations[0].Path, Is.EqualTo(profilePath));
            Assert.That(violations[0].IsWarning, Is.False);
            Assert.That(violations[0].Message, Does.Contain(messagePart));
        }

        [Test]
        public void RuleIdIsG4()
        {
            Assert.That(new VolumeProfileRule().Id, Is.EqualTo("G-4"));
        }

        // --- Fail ---

        [Test]
        public void ActiveVignetteOverriddenToAQuarterFails()
        {
            string path = TempAssets.SavedProfile(_folder, "Vignette", profile =>
                profile.Add<Vignette>(false).intensity.Override(0.25f));

            AssertOneViolation(Run(), path, "Vignette");
        }

        [Test]
        public void ActiveBloomAtATenthFails()
        {
            string path = TempAssets.SavedProfile(_folder, "Bloom", profile =>
                profile.Add<Bloom>(false).intensity.Override(0.1f));

            AssertOneViolation(Run(), path, "Bloom");
        }

        [Test]
        public void ActiveFilmGrainAboveZeroFails()
        {
            string path = TempAssets.SavedProfile(_folder, "FilmGrain", profile =>
                profile.Add<FilmGrain>(false).intensity.Override(0.5f));

            AssertOneViolation(Run(), path, "FilmGrain");
        }

        [Test]
        public void ActiveScreenSpaceLensFlareAboveZeroFails()
        {
            string path = TempAssets.SavedProfile(_folder, "Flare", profile =>
                profile.Add<ScreenSpaceLensFlare>(false).intensity.Override(1f));

            AssertOneViolation(Run(), path, "ScreenSpaceLensFlare");
        }

        [Test]
        public void EachOffendingOverrideIsItsOwnViolation()
        {
            string path = TempAssets.SavedProfile(_folder, "Both", profile =>
            {
                profile.Add<Vignette>(false).intensity.Override(0.3f);
                profile.Add<Bloom>(false).intensity.Override(0.2f);
            });

            List<Violation> violations = Run();

            Assert.That(violations, Has.Count.EqualTo(2), string.Join("\n", violations));
            Assert.That(violations.Select(v => v.Path), Is.All.EqualTo(path));
            Assert.That(violations.Select(v => v.Message), Has.Some.Contain("Vignette"));
            Assert.That(violations.Select(v => v.Message), Has.Some.Contain("Bloom"));
        }

        [Test]
        public void OnlyTheOffendingProfileIsReported()
        {
            TempAssets.SavedProfile(_folder, "Fine", profile => profile.Add<Tonemapping>(false));
            string bad = TempAssets.SavedProfile(_folder, "Bad", profile =>
                profile.Add<Bloom>(false).intensity.Override(0.1f));

            AssertOneViolation(Run(), bad, "Bloom");
        }

        // --- Pass ---

        [Test]
        public void ActiveVignetteAtIntensityZeroPasses()
        {
            // Add(true) overrides every parameter at its default, so intensity is overridden to 0.
            TempAssets.SavedProfile(_folder, "Zero", profile => profile.Add<Vignette>(true));

            Assert.That(Run(), Is.Empty);
        }

        [Test]
        public void AProfileWithOnlyTonemappingPasses()
        {
            TempAssets.SavedProfile(_folder, "Tonemapping", profile =>
                profile.Add<Tonemapping>(false).mode.Override(TonemappingMode.Neutral));

            Assert.That(Run(), Is.Empty);
        }

        [Test]
        public void AnEmptyProfilePasses()
        {
            TempAssets.SavedProfile(_folder, "Empty", profile => { });

            Assert.That(Run(), Is.Empty);
        }

        [Test]
        public void AnInactiveOverrideAboveZeroPasses()
        {
            TempAssets.SavedProfile(_folder, "Inactive", profile =>
            {
                Bloom bloom = profile.Add<Bloom>(false);
                bloom.intensity.Override(0.8f);
                bloom.active = false;
            });

            Assert.That(Run(), Is.Empty);
        }

        [Test]
        public void AnIntensityThatIsNotOverriddenDoesNotCount()
        {
            // The stored value is ignored by the volume system when the parameter is not overridden.
            TempAssets.SavedProfile(_folder, "NotOverridden", profile =>
            {
                Vignette vignette = profile.Add<Vignette>(false);
                vignette.intensity.value = 0.6f;
            });

            Assert.That(Run(), Is.Empty);
        }

        // --- Scope ---

        [Test]
        public void ProfilesOutsideTheSearchRootAreNotScanned()
        {
            string other = TempAssets.Folder("G4-other");
            TempAssets.SavedProfile(other, "Bad", profile => profile.Add<Bloom>(false).intensity.Override(0.1f));

            Assert.That(Run(), Is.Empty);
        }

        [Test]
        public void TheFmodVendorFolderIsExcluded()
        {
            bool createdPlugins = !AssetDatabase.IsValidFolder("Assets/Plugins");
            try
            {
                if (createdPlugins) AssetDatabase.CreateFolder("Assets", "Plugins");
                if (!AssetDatabase.IsValidFolder("Assets/Plugins/FMOD")) AssetDatabase.CreateFolder("Assets/Plugins", "FMOD");
                TempAssets.SavedProfile("Assets/Plugins/FMOD", "VendorBloom", profile =>
                    profile.Add<Bloom>(false).intensity.Override(0.5f));
                ValidationContext context = TempAssets.Context("Assets/Plugins");

                Assert.That(new VolumeProfileRule().Run(context).ToList(), Is.Empty);
            }
            finally
            {
                if (createdPlugins) AssetDatabase.DeleteAsset("Assets/Plugins");
                else AssetDatabase.DeleteAsset("Assets/Plugins/FMOD/VendorBloom.asset");
            }
        }

        [Test]
        public void TheRealProjectProfilesPass()
        {
            var context = TempAssets.Context(ValidationContext.DefaultAssetSearchRoot);

            List<Violation> violations = new VolumeProfileRule().Run(context).ToList();

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
            Assert.That(AssetDatabase.FindAssets("t:VolumeProfile", new[] { "Assets" }), Is.Not.Empty,
                "the real project has volume profiles, so this pass case is not vacuous");
        }
    }
}
