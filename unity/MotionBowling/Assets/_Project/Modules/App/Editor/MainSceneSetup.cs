using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bowling.App.Editor
{
    /// <summary>
    /// Puts the Bootstrap root into Main.unity (foundation spec, section 3.5): a plain GameObject named
    /// "Bootstrap" carrying <see cref="GameBootstrap"/>, the one loose root the scene allows. It checks first and
    /// writes only what is missing, so a second run changes nothing. Run it headless with
    /// <c>tools/ci/unity.sh exec Bowling.App.Editor.MainSceneSetup.EnsureBootstrapBatch</c>.
    /// </summary>
    public static class MainSceneSetup
    {
        public const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        public const string BootstrapName = "Bootstrap";

        /// <summary>
        /// Opens Main.unity and makes sure it has a root named Bootstrap with a GameBootstrap on it, saving the
        /// scene only when something was added.
        /// </summary>
        /// <returns>True if the scene was changed and saved.</returns>
        public static bool EnsureBootstrap()
        {
            if (AssetDatabase.AssetPathToGUID(ScenePath).Length == 0)
                throw new InvalidOperationException($"{ScenePath} does not exist; run ProjectSettingsBaseline.ApplyAllBatch first.");

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(candidate => candidate.name == BootstrapName);
            bool changed = false;

            if (root == null)
            {
                root = new GameObject(BootstrapName);
                SceneManager.MoveGameObjectToScene(root, scene);
                root.transform.SetAsFirstSibling();
                changed = true;
            }

            if (root.GetComponent<GameBootstrap>() == null)
            {
                root.AddComponent<GameBootstrap>();
                changed = true;
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new InvalidOperationException($"Could not save {ScenePath}");
            }

            return changed;
        }

        /// <summary>
        /// Batch entry point: runs <see cref="EnsureBootstrap"/>, then ends the editor with exit code 0. On any
        /// exception it logs it and ends the editor with exit code 1.
        /// </summary>
        public static void EnsureBootstrapBatch()
        {
            int exitCode = 0;
            try
            {
                bool changed = EnsureBootstrap();
                Debug.Log($"[MainSceneSetup] {(changed ? "added the Bootstrap root to" : "found the Bootstrap root in")} {ScenePath}");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[MainSceneSetup] failed: {exception}");
                exitCode = 1;
            }
            EditorApplication.Exit(exitCode);
        }
    }
}
