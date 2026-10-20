using System;
using Bowling.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Bowling.App.Editor
{
    /// <summary>
    /// Creates the committed config assets of the Gameplay module (gameplay spec, P-13): GameplayTuning and
    /// DifficultyCatalog under Assets/_Project/Config/Gameplay, each holding the defaults of its type. It writes only
    /// what is missing, so a second run changes nothing and never overwrites a hand-tuned asset. Run it headless with
    /// <c>tools/ci/unity.sh exec Bowling.App.Editor.ConfigAssetSetup.EnsureGameplayAssetsBatch</c>.
    /// </summary>
    public static class ConfigAssetSetup
    {
        /// <summary>
        /// Creates the Config/Gameplay folders and any missing Gameplay config asset.
        /// </summary>
        /// <returns>True if anything was written.</returns>
        public static bool EnsureGameplayAssets()
        {
            bool changed = EnsureAsset<GameplayTuning>(GameplayTuning.AssetPath);
            changed |= EnsureAsset<DifficultyCatalog>(DifficultyCatalog.AssetPath);

            if (changed)
                AssetDatabase.SaveAssets();
            return changed;
        }

        /// <summary>
        /// Batch entry point: runs <see cref="EnsureGameplayAssets"/>, then ends the editor with exit code 0. On any
        /// exception it logs it and ends the editor with exit code 1.
        /// </summary>
        public static void EnsureGameplayAssetsBatch()
        {
            int exitCode = 0;
            try
            {
                bool changed = EnsureGameplayAssets();
                Debug.Log($"[ConfigAssetSetup] {(changed ? "wrote" : "found")} the Gameplay config assets (GameplayTuning, DifficultyCatalog)");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[ConfigAssetSetup] failed: {exception}");
                exitCode = 1;
            }
            EditorApplication.Exit(exitCode);
        }

        // Makes the asset at assetPath exist as a T, creating its folders first. An existing file of another type is an error.
        private static bool EnsureAsset<T>(string assetPath) where T : ScriptableObject
        {
            if (AssetDatabase.AssetPathToGUID(assetPath).Length != 0)
            {
                if (AssetDatabase.LoadAssetAtPath<T>(assetPath) == null)
                    throw new InvalidOperationException($"{assetPath} exists but is not a {typeof(T).Name}.");
                return false;
            }

            EnsureFolder(assetPath.Substring(0, assetPath.LastIndexOf('/')));

            var instance = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(instance, assetPath);
            return true;
        }

        // Creates every missing folder along folderPath ("Assets/_Project/Config/Gameplay").
        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            int split = folderPath.LastIndexOf('/');
            string parent = folderPath.Substring(0, split);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderPath.Substring(split + 1));
        }
    }
}
