using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Bowling.Tooling.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Bowling.Tooling.Tests
{
    /// <summary>
    /// G-6 (foundation spec, section 8.1): a color texture in the scanned folders may not hold smooth ramps.
    /// Each case writes real files under <c>Assets/_TestTemp/Art/</c>, imports them and runs the rule on that
    /// folder, which the context names as an explicit search root.
    /// </summary>
    public class TextureGradientRuleTests
    {
        string _folder;

        [SetUp]
        public void SetUp() => _folder = TempAssets.Folder("Art");

        [TearDown]
        public void TearDown() => TempAssets.DeleteAll();

        List<Violation> Run() => new TextureGradientRule().Run(TempAssets.Context(_folder)).ToList();

        // A 256x4 ramp as an in-memory texture, for tests that save it as a native asset.
        static Texture2D NativeTexture()
        {
            var texture = new Texture2D(256, 4, TextureFormat.RGBA32, false);
            var pixels = new Color32[256 * 4];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = TextureFixtures.Ramp(i % 256, i / 256);
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        static void AssertOneViolation(List<Violation> violations, string path, string messagePart)
        {
            Assert.That(violations, Has.Count.EqualTo(1), string.Join("\n", violations));
            Assert.That(violations[0].RuleId, Is.EqualTo("G-6"));
            Assert.That(violations[0].Path, Is.EqualTo(path));
            Assert.That(violations[0].IsWarning, Is.False);
            Assert.That(violations[0].Message, Does.Contain(messagePart));
        }

        [Test]
        public void RuleIdIsG6()
        {
            Assert.That(new TextureGradientRule().Id, Is.EqualTo("G-6"));
        }

        // --- Fail ---

        [Test]
        public void RampPngFailsWithThePercentagesInTheMessage()
        {
            string path = TextureFixtures.Png(_folder, "Ramp", 256, 4, TextureFixtures.Ramp);

            List<Violation> violations = Run();

            AssertOneViolation(violations, path, "100.0% smooth-ramp pixels (max 2.0%)");
            Assert.That(violations[0].ToString(), Is.EqualTo($"G-6 {path} 100.0% smooth-ramp pixels (max 2.0%)"));
        }

        [Test]
        public void RampImportedAsASpriteFails()
        {
            string path = TextureFixtures.Png(_folder, "RampSprite", 256, 4, TextureFixtures.Ramp);
            TextureFixtures.Configure(path, importer => importer.textureType = TextureImporterType.Sprite);

            AssertOneViolation(Run(), path, "smooth-ramp pixels");
        }

        [Test]
        public void SmoothPixelsAboveTheThresholdFail()
        {
            // Flat gray with three adjacent columns stepping 101, 102, 103: those and one more each side, 5 of 100 columns.
            string path = TextureFixtures.Png(_folder, "Patch", 100, 100, (x, y) => TextureFixtures.Gray(x >= 50 && x <= 52 ? 51 + x : 100));

            AssertOneViolation(Run(), path, "5.0% smooth-ramp pixels (max 2.0%)");
        }

        [Test]
        public void TgaFailsWithConvertToPng()
        {
            string path = TextureFixtures.Tga(_folder, "Stripes", 64, 64, TextureFixtures.Stripes);

            AssertOneViolation(Run(), path, "convert to PNG");
        }

        [Test]
        public void ATexture2DSavedAsANativeAssetFailsWithConvertToPng()
        {
            // A native .asset gets a NativeFormatImporter, not a TextureImporter: the easiest way around the gate.
            string path = $"{_folder}/Glow.asset";
            AssetDatabase.CreateAsset(NativeTexture(), path);

            AssertOneViolation(Run(), path, "convert to PNG");
        }

        [Test]
        public void ACubemapSavedAsANativeAssetFailsWithConvertToPng()
        {
            string path = $"{_folder}/Sky.asset";
            AssetDatabase.CreateAsset(new Cubemap(4, TextureFormat.RGBA32, false), path);

            AssertOneViolation(Run(), path, "convert to PNG");
        }

        [Test]
        public void ATexture2DArraySavedAsANativeAssetFailsWithConvertToPng()
        {
            string path = $"{_folder}/Layers.asset";
            AssetDatabase.CreateAsset(new Texture2DArray(4, 4, 2, TextureFormat.RGBA32, false), path);

            AssertOneViolation(Run(), path, "convert to PNG");
        }

        [Test]
        public void ATextureThatCannotBeDecodedFails()
        {
            // Unity does not list a PNG it can't import at all. This one imported fine, then its file was damaged:
            // the AssetDatabase still lists it as a texture until the next refresh, but the bytes are no PNG.
            string path = TextureFixtures.Png(_folder, "Broken", 4, 4, TextureFixtures.Ramp);
            TextureFixtures.WriteFile(path, new byte[] { 1, 2, 3, 4 });

            AssertOneViolation(Run(), path, "cannot be read");
        }

        [Test]
        public void ARootThatWasNamedAndDoesNotExistFails()
        {
            var context = TempAssets.Context(_folder);
            context.TextureSearchRoots = new[] { $"{_folder}/Missing" };

            List<Violation> violations = new TextureGradientRule().Run(context).ToList();

            AssertOneViolation(violations, $"{_folder}/Missing", "search folder not found");
        }

        // --- Pass ---

        [Test]
        public void FlatStripesPass()
        {
            TextureFixtures.Png(_folder, "Stripes", 64, 64, TextureFixtures.Stripes);

            Assert.That(Run(), Is.Empty);
        }

        [Test]
        public void SmoothPixelsUnderTheThresholdPass()
        {
            // Flat gray with one column of 101: that column and the two beside it are smooth, 3 of 200 columns (1.5%).
            TextureFixtures.Png(_folder, "Seam", 200, 50, (x, y) => TextureFixtures.Gray(x == 100 ? 101 : 100));

            Assert.That(Run(), Is.Empty);
        }

        [Test]
        public void ATranslucentRampPasses()
        {
            TextureFixtures.Png(_folder, "Fade", 256, 4, (x, y) => TextureFixtures.Gray(x, 128));

            Assert.That(Run(), Is.Empty);
        }

        [Test]
        public void ATextureEmbeddedInAnotherAssetIsNotJudged()
        {
            // The way an FBX carries its textures: a sub-asset of a main asset that is not a texture. The
            // AssetDatabase lists the file for t:Texture, but only a texture main asset is a texture file.
            string path = $"{_folder}/Holder.mat";
            var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            AssetDatabase.CreateAsset(material, path);
            Texture2D embedded = NativeTexture();
            embedded.name = "Embedded";
            AssetDatabase.AddObjectToAsset(embedded, material);
            AssetDatabase.SaveAssets();

            Assert.That(AssetDatabase.FindAssets("t:Texture", new[] { _folder }), Is.Not.Empty, "the search no longer lists the host asset");
            Assert.That(AssetDatabase.GetMainAssetTypeAtPath(path), Is.EqualTo(typeof(Material)));
            Assert.That(Run(), Is.Empty);
        }

        [Test]
        public void NoTexturesPass()
        {
            Assert.That(Run(), Is.Empty);
        }

        [Test]
        public void RampImportedAsANormalMapIsSkipped()
        {
            string path = TextureFixtures.Png(_folder, "Normal", 256, 4, TextureFixtures.Ramp);
            TextureFixtures.Configure(path, importer => importer.textureType = TextureImporterType.NormalMap);

            Assert.That(Run(), Is.Empty);
        }

        [Test]
        public void RampWithoutSrgbIsSkipped()
        {
            string path = TextureFixtures.Png(_folder, "Data", 256, 4, TextureFixtures.Ramp);
            TextureFixtures.Configure(path, importer => importer.sRGBTexture = false);

            Assert.That(Run(), Is.Empty);
        }

        [TestCase(TextureImporterType.Lightmap)]
        [TestCase(TextureImporterType.DirectionalLightmap)]
        [TestCase(TextureImporterType.Shadowmask)]
        public void RampImportedAsALightmapTypeIsSkipped(TextureImporterType type)
        {
            string path = TextureFixtures.Png(_folder, "Baked", 256, 4, TextureFixtures.Ramp);
            TextureFixtures.Configure(path, importer => importer.textureType = type);

            Assert.That(Run(), Is.Empty);
        }

        [Test]
        public void ANonPngNormalMapIsSkippedToo()
        {
            // Only color textures must be PNG, so a skipped type needs no conversion.
            string path = TextureFixtures.Tga(_folder, "NormalTga", 64, 64, TextureFixtures.Stripes);
            TextureFixtures.Configure(path, importer => importer.textureType = TextureImporterType.NormalMap);

            Assert.That(Run(), Is.Empty);
        }

        [Test]
        public void TheDefaultFoldersAreSkippedSilentlyWhenTheyDoNotExist()
        {
            var context = TempAssets.Context(_folder);
            context.TextureSearchRoots = null;

            List<Violation> violations = new TextureGradientRule().Run(context).ToList();

            Assert.That(violations.Where(v => v.Message.Contains("not found")), Is.Empty);
        }

        [Test]
        public void TheDefaultFoldersAreTheArtAndUiFolders()
        {
            Assert.That(TextureGradientRule.DefaultRoots, Is.EqualTo(new[] { "Assets/_Project/Art", "Assets/_Project/Modules/UI" }));
        }

        [Test]
        public void PercentagesPrintWithADotWhateverTheCulture()
        {
            string path = TextureFixtures.Png(_folder, "Ramp", 256, 4, TextureFixtures.Ramp);
            CultureInfo saved = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            try
            {
                AssertOneViolation(Run(), path, "100.0% smooth-ramp pixels (max 2.0%)");
            }
            finally
            {
                CultureInfo.CurrentCulture = saved;
            }
        }

        // --- Settings ---

        [Test]
        public void TheCommittedSettingsAssetHoldsTheDefaults()
        {
            var settings = AssetDatabase.LoadAssetAtPath<GradientHeuristicSettings>(GradientHeuristicSettings.AssetPath);

            Assert.That(settings, Is.Not.Null, $"no settings asset at {GradientHeuristicSettings.AssetPath}");
            Assert.That(settings.MaxStep, Is.EqualTo(3));
            Assert.That(settings.MaxFraction, Is.EqualTo(0.02f));
        }
    }
}
