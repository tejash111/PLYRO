using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Bowling.Tooling.Editor
{
    /// <summary>
    /// G-6 (foundation spec, section 8.1): a color texture anywhere under <c>Assets/_Project</c> except the Tooling
    /// module (the context's <see cref="ValidationContext.DefaultTextureSearchRoots"/>, or the folders named in
    /// <see cref="ValidationContext.TextureSearchRoots"/>) may not hold a smooth ramp, judged by
    /// <see cref="GradientHeuristic"/> with the thresholds of <see cref="GradientHeuristicSettings"/>.
    /// <para>
    /// The rule reads the source PNG bytes, so any other color-texture file fails with "convert to PNG", a texture
    /// saved as a native <c>.asset</c> (<c>Texture2D</c>, <c>Cubemap</c>, <c>Texture2DArray</c>) included. Normal maps,
    /// non-sRGB data textures and the lightmap importer types are not color textures and are skipped first, whatever
    /// their file format. A texture held as a sub-asset of another asset is not a texture file and is not judged.
    /// A default folder that does not exist is skipped; a folder named in the context must exist.
    /// </para>
    /// </summary>
    public sealed class TextureGradientRule : IValidationRule
    {
        public string Id => "G-6";

        public IEnumerable<Violation> Run(ValidationContext context)
        {
            var violations = new List<Violation>();
            string[] roots = ExistingRoots(context, violations);
            if (roots.Length == 0) return violations;

            var settings = AssetDatabase.LoadAssetAtPath<GradientHeuristicSettings>(GradientHeuristicSettings.AssetPath);
            int maxStep = settings != null ? settings.MaxStep : GradientHeuristicSettings.DefaultMaxStep;
            float maxFraction = settings != null ? settings.MaxFraction : GradientHeuristicSettings.DefaultMaxFraction;

            foreach (string path in ScanScope.AssetPaths("t:Texture", roots).Where(p => !ScanScope.IsVendor(p) && !ScanScope.IsTooling(p)))
            {
                if (!IsColorTextureFile(path)) continue;

                if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    violations.Add(Fail(path, $"convert to PNG (G-6 reads PNG only, not {Path.GetExtension(path).ToLowerInvariant()})"));
                else
                    CheckPng(context, path, maxStep, maxFraction, violations);
            }
            return violations;
        }

        // The folders to scan. An explicit folder that is missing is a violation; a missing default is skipped
        // silently (ruling R16).
        string[] ExistingRoots(ValidationContext context, List<Violation> violations)
        {
            bool named = context.TextureSearchRoots != null;
            var roots = new List<string>();
            foreach (string folder in context.TextureSearchRoots ?? context.DefaultTextureSearchRoots)
            {
                string root = folder.Replace('\\', '/').TrimEnd('/');
                if (AssetDatabase.IsValidFolder(root)) roots.Add(root);
                else if (named) violations.Add(Fail(root, "search folder not found"));
            }
            return roots.ToArray();
        }

        // A file the rule judges: an imported texture that is a color texture, or a native texture asset. A native
        // asset (a Texture2D saved with AssetDatabase.CreateAsset) has no TextureImporter and can't be read as a
        // PNG, so it must be judged as "convert to PNG" or it would be the way around the gate. Only a main asset
        // counts: the search also lists a file that merely holds a texture as a sub-asset (an FBX, a material).
        static bool IsColorTextureFile(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer) return !IsNotColorTexture(importer);

            Type main = AssetDatabase.GetMainAssetTypeAtPath(path);
            return main != null
                && (typeof(Texture2D).IsAssignableFrom(main)
                    || typeof(Cubemap).IsAssignableFrom(main)
                    || typeof(Texture2DArray).IsAssignableFrom(main));
        }

        static bool IsNotColorTexture(TextureImporter importer)
        {
            if (!importer.sRGBTexture) return true;
            switch (importer.textureType)
            {
                case TextureImporterType.NormalMap:
                case TextureImporterType.Lightmap:
                case TextureImporterType.DirectionalLightmap:
                case TextureImporterType.Shadowmask:
                    return true;
                default:
                    return false;
            }
        }

        void CheckPng(ValidationContext context, string path, int maxStep, float maxFraction, List<Violation> violations)
        {
            var texture = new Texture2D(2, 2);
            try
            {
                byte[] bytes = File.ReadAllBytes(context.ResolvePath(path));
                if (!ImageConversion.LoadImage(texture, bytes))
                {
                    violations.Add(Fail(path, "cannot be read: Unity could not decode the PNG"));
                    return;
                }
                float fraction = GradientHeuristic.SmoothStepFraction(texture.GetPixels32(), texture.width, texture.height, maxStep);
                if (fraction > maxFraction)
                    violations.Add(Fail(path, $"{Percent(fraction)}% smooth-ramp pixels (max {Percent(maxFraction)}%)"));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is UnityException)
            {
                violations.Add(Fail(path, $"cannot be read: {e.GetType().Name}: {e.Message}"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        static string Percent(float fraction) => (fraction * 100f).ToString("F1", CultureInfo.InvariantCulture);

        Violation Fail(string path, string message) => new Violation(Id, path, 0, message, false);
    }
}
