using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Bowling.Tooling.Editor
{
    /// <summary>
    /// M-7 (foundation spec, sections 3.5 and 8.2): every root object of <see cref="ValidationContext.MainScenePath"/>
    /// is a prefab instance root or is named <c>Bootstrap</c>. Later work adds prefab instances to the scene, never
    /// loose objects (SOT-2).
    /// </summary>
    public sealed class MainSceneRootRule : IValidationRule
    {
        const string BootstrapName = "Bootstrap";

        public string Id => "M-7";

        public IEnumerable<Violation> Run(ValidationContext context)
        {
            var violations = new List<Violation>();
            string path = context.MainScenePath.Replace('\\', '/');
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                violations.Add(Fail(path, "scene not found"));
                return violations;
            }
            try
            {
                OpenedScene.With(path, scene =>
                {
                    foreach (GameObject root in scene.GetRootGameObjects())
                    {
                        if (root.name == BootstrapName || PrefabUtility.IsAnyPrefabInstanceRoot(root)) continue;
                        violations.Add(Fail(path, $"root object '{root.name}' is neither a prefab instance root nor named {BootstrapName}"));
                    }
                });
            }
            catch (Exception e)
            {
                violations.Add(Fail(path, $"cannot be checked: {e.GetType().Name}: {e.Message}"));
            }
            return violations;
        }

        Violation Fail(string path, string message) => new Violation(Id, path, 0, message, false);
    }
}
