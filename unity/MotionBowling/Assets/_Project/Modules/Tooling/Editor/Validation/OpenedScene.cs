using System;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Bowling.Tooling.Editor
{
    /// <summary>
    /// Opens a scene to read it, and leaves the editor as it was: a scene the editor already had open stays open,
    /// one opened here is closed again, and the scene that was active is active again. Nothing here modifies a scene,
    /// so no scene is left dirty.
    /// </summary>
    internal static class OpenedScene
    {
        /// <summary>
        /// Runs <paramref name="inspect"/> with the scene loaded and active, so <c>RenderSettings</c> is the scene's.
        /// Throws when the scene can't be opened.
        /// </summary>
        public static void With(string scenePath, Action<Scene> inspect)
        {
            Scene previouslyActive = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedHere = !(scene.IsValid() && scene.isLoaded);
            if (openedHere) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                inspect(scene);
            }
            finally
            {
                if (previouslyActive.IsValid() && previouslyActive.isLoaded) SceneManager.SetActiveScene(previouslyActive);
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
