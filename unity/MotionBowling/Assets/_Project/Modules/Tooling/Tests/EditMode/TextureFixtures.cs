using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Bowling.Tooling.Tests
{
    /// <summary>
    /// Texture files for the G-6 tests: pixels built in code, encoded, written under the project and imported.
    /// They belong in a folder made by <see cref="TempAssets.Folder"/>, which teardown deletes.
    /// </summary>
    internal static class TextureFixtures
    {
        public static Color32 Gray(int value, byte alpha = 255) => new Color32((byte)value, (byte)value, (byte)value, alpha);

        /// <summary>A 256x4 ramp, one step per pixel: the smoothest thing G-6 can see.</summary>
        public static Color32 Ramp(int x, int y) => Gray(x);

        /// <summary>Two flat colors in vertical stripes 8 px wide.</summary>
        public static Color32 Stripes(int x, int y) => (x / 8) % 2 == 0 ? Gray(40) : Gray(200);

        /// <summary>Writes a PNG and imports it. Returns the asset path.</summary>
        public static string Png(string folder, string name, int width, int height, Func<int, int, Color32> pixelAt)
        {
            return Write($"{folder}/{name}.png", Encode(width, height, pixelAt, texture => texture.EncodeToPNG()));
        }

        /// <summary>Writes a TGA (a color format G-6 does not read) and imports it. Returns the asset path.</summary>
        public static string Tga(string folder, string name, int width, int height, Func<int, int, Color32> pixelAt)
        {
            return Write($"{folder}/{name}.tga", Encode(width, height, pixelAt, texture => texture.EncodeToTGA()));
        }

        /// <summary>Writes the bytes to an asset path and imports it. Returns the asset path.</summary>
        public static string Write(string assetPath, byte[] bytes)
        {
            WriteFile(assetPath, bytes);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            return assetPath;
        }

        /// <summary>Writes the bytes to the file of an asset path without importing, so the AssetDatabase keeps what it knew.</summary>
        public static void WriteFile(string assetPath, byte[] bytes)
        {
            string project = Path.GetDirectoryName(Application.dataPath);
            File.WriteAllBytes(Path.Combine(project, assetPath), bytes);
        }

        /// <summary>Changes the import settings of a texture asset and reimports it.</summary>
        public static void Configure(string assetPath, Action<TextureImporter> change)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            change(importer);
            importer.SaveAndReimport();
        }

        static byte[] Encode(int width, int height, Func<int, int, Color32> pixelAt, Func<Texture2D, byte[]> encoder)
        {
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = pixelAt(x, y);

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply();
                return encoder(texture);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
