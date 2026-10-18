using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Bowling.Tooling.Editor
{
    /// <summary>
    /// The GameObject-level part of G-5 (foundation spec, section 8.1), shared by scenes and prefabs: a camera that
    /// clears to the skybox, and a line or trail renderer whose <c>colorGradient</c> is not one flat color. Inactive
    /// objects are included.
    /// <para>
    /// A flat gradient is one color and one alpha along its whole length. Unity can't store fewer than two keys of
    /// each (a gradient set with one key reads back with two equal ones, so a LineRenderer can never have "one
    /// key"), so the keys are judged by value: more than one distinct color, or more than one distinct alpha, is a
    /// gradient.
    /// </para>
    /// </summary>
    internal static class GradientHazards
    {
        /// <summary>One message per fault found in the hierarchies under <paramref name="roots"/>.</summary>
        public static IEnumerable<string> Find(IEnumerable<GameObject> roots)
        {
            foreach (GameObject root in roots)
            {
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                {
                    if (camera.clearFlags == CameraClearFlags.Skybox)
                        yield return $"Camera '{HierarchyPath(camera)}' has clearFlags Skybox";
                }
                foreach (LineRenderer line in root.GetComponentsInChildren<LineRenderer>(true))
                {
                    string problem = KeyProblem(line, line.colorGradient);
                    if (problem != null) yield return problem;
                }
                foreach (TrailRenderer trail in root.GetComponentsInChildren<TrailRenderer>(true))
                {
                    string problem = KeyProblem(trail, trail.colorGradient);
                    if (problem != null) yield return problem;
                }
            }
        }

        static string KeyProblem(Component renderer, Gradient gradient)
        {
            int colors = gradient.colorKeys.Select(key => key.color).Distinct().Count();
            int alphas = gradient.alphaKeys.Select(key => key.alpha).Distinct().Count();
            var parts = new List<string>();
            if (colors > 1) parts.Add($"{colors} different colors");
            if (alphas > 1) parts.Add($"{alphas} different alphas");
            if (parts.Count == 0) return null;
            return $"{renderer.GetType().Name} '{HierarchyPath(renderer)}' colorGradient is not one flat color: "
                + string.Join(" and ", parts) + " across its keys";
        }

        static string HierarchyPath(Component component)
        {
            var names = new Stack<string>();
            for (Transform t = component.transform; t != null; t = t.parent) names.Push(t.name);
            return string.Join("/", names);
        }
    }
}
