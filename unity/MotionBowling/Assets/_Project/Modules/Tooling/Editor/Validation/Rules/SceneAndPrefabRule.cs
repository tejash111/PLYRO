using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bowling.Tooling.Editor
{
    /// <summary>
    /// G-5 (foundation spec, section 8.1): a scene in the build has flat ambient light, no fog and no skybox, and
    /// neither scenes nor prefabs hold a camera that clears to the skybox or a line or trail renderer with a
    /// gradient. The scenes are <see cref="ValidationContext.ScenePaths"/>, or the enabled scenes of the build
    /// settings when that is <c>null</c>. The prefabs are every <c>t:Prefab</c> asset under <c>Assets/_Project</c>
    /// (see <see cref="ValidationContext.AssetSearchRoot"/>), except the Tooling module and vendor folders.
    /// </summary>
    public sealed class SceneAndPrefabRule : IValidationRule
    {
        public string Id => "G-5";

        public IEnumerable<Violation> Run(ValidationContext context)
        {
            var violations = new List<Violation>();
            IEnumerable<string> scenes = context.ScenePaths
                ?? EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path);
            foreach (string scenePath in scenes.Select(path => path.Replace('\\', '/')))
                CheckScene(scenePath, violations);
            CheckPrefabs(ScanScope.PrefabRoot(context), violations);
            return violations;
        }

        void CheckScene(string path, List<Violation> violations)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                violations.Add(Fail(path, "scene not found"));
                return;
            }
            try
            {
                OpenedScene.With(path, scene =>
                {
                    foreach (string problem in RenderSettingsProblems()) violations.Add(Fail(path, problem));
                    foreach (string problem in GradientHazards.Find(scene.GetRootGameObjects())) violations.Add(Fail(path, problem));
                });
            }
            catch (Exception e)
            {
                violations.Add(Fail(path, $"cannot be checked: {e.GetType().Name}: {e.Message}"));
            }
        }

        // The active scene's settings: OpenedScene makes the scene under test the active one.
        static IEnumerable<string> RenderSettingsProblems()
        {
            if (RenderSettings.ambientMode != AmbientMode.Flat)
                yield return $"RenderSettings.ambientMode is {RenderSettings.ambientMode}, not Flat";
            if (RenderSettings.fog)
                yield return "RenderSettings.fog is on";
            if (RenderSettings.skybox != null)
                yield return $"RenderSettings.skybox is set to '{RenderSettings.skybox.name}'";
        }

        void CheckPrefabs(string root, List<Violation> violations)
        {
            if (!AssetDatabase.IsValidFolder(root))
            {
                violations.Add(Fail(root, "search folder not found"));
                return;
            }
            foreach (string path in ScanScope.AssetPaths("t:Prefab", root).Where(p => !ScanScope.IsVendor(p) && !ScanScope.IsTooling(p)))
            {
                GameObject contents = null;
                try
                {
                    contents = PrefabUtility.LoadPrefabContents(path);
                    foreach (string problem in GradientHazards.Find(new[] { contents })) violations.Add(Fail(path, problem));
                }
                catch (Exception e)
                {
                    violations.Add(Fail(path, $"cannot be checked: {e.GetType().Name}: {e.Message}"));
                }
                finally
                {
                    if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
                }
            }
        }

        Violation Fail(string path, string message) => new Violation(Id, path, 0, message, false);
    }
}
