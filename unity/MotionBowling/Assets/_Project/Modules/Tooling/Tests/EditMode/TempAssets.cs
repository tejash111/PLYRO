using System;
using System.IO;
using Bowling.Tooling.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Bowling.Tooling.Tests
{
    /// <summary>
    /// The temporary assets of the editor-rule tests (foundation spec, section 8.3): everything lives under
    /// <c>Assets/_TestTemp/&lt;TestName&gt;/</c> (gitignored), and <see cref="DeleteAll"/> removes it in teardown.
    /// A scene or prefab is built, saved and closed again, so no test leaves a scene open or modified.
    /// </summary>
    internal static class TempAssets
    {
        public const string Root = "Assets/_TestTemp";

        /// <summary>The asset folder <c>Assets/_TestTemp/&lt;name&gt;</c>, created if needed.</summary>
        public static string Folder(string name)
        {
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", "_TestTemp");
            string path = $"{Root}/{name}";
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(Root, name);
            return path;
        }

        public static void DeleteAll() => AssetDatabase.DeleteAsset(Root);

        /// <summary>A context for a rule test: the open project, scanning only <paramref name="folder"/> and no build scene.</summary>
        public static ValidationContext Context(string folder)
        {
            string project = Path.GetDirectoryName(Application.dataPath);
            return new ValidationContext
            {
                RepoRoot = Path.GetFullPath(Path.Combine(project, "..", "..")),
                ProjectRoot = project,
                AssetSearchRoot = folder,
                ScenePaths = new string[0],
            };
        }

        /// <summary>A volume profile asset, its components saved with it. Returns the asset path.</summary>
        public static string SavedProfile(string folder, string name, Action<VolumeProfile> build)
        {
            string path = $"{folder}/{name}.asset";
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            build(profile);
            foreach (VolumeComponent component in profile.components)
            {
                AssetDatabase.AddObjectToAsset(component, profile);
                EditorUtility.SetDirty(component);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return path;
        }

        /// <summary>A prefab whose root is a new GameObject named <paramref name="name"/>. Returns the asset path.</summary>
        public static string SavedPrefab(string folder, string name, Action<GameObject> build)
        {
            string path = $"{folder}/{name}.prefab";
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject(name);
                SceneManager.MoveGameObjectToScene(root, preview);
                build?.Invoke(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
            return path;
        }

        /// <summary>
        /// A scene that satisfies G-5 as it is (flat ambient, no skybox, no fog), changed by
        /// <paramref name="build"/>, which runs with the new scene active. Returns the asset path.
        /// The scene is built as the only open scene (Unity refuses an additive new scene while the test runner's
        /// untitled scene is unsaved) and an empty untitled scene replaces it, so the temp file is not left open.
        /// </summary>
        public static string SavedScene(string folder, string name, Action<Scene> build)
        {
            string path = $"{folder}/{name}.unity";
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            try
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.2f, 0.2f, 0.2f);
                RenderSettings.skybox = null;
                RenderSettings.fog = false;
                build?.Invoke(scene);
                Assert(EditorSceneManager.SaveScene(scene, path), $"could not save the temp scene {path}");
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            return path;
        }

        /// <summary>A skybox material asset (a real one, so a scene can keep the reference).</summary>
        public static Material SkyboxMaterial(string folder)
        {
            Shader shader = Shader.Find("Skybox/Procedural");
            if (shader == null) throw new InvalidOperationException("The built-in shader Skybox/Procedural was not found.");
            var material = new Material(shader);
            AssetDatabase.CreateAsset(material, $"{folder}/Sky.mat");
            return material;
        }

        static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
