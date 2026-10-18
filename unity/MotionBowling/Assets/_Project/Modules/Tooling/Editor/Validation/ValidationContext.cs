using System;
using System.IO;
using UnityEngine;

namespace Bowling.Tooling.Editor
{
    /// <summary>
    /// What a rule needs to judge the project: where things are, and the module rules. A test points
    /// <see cref="ModulesRoot"/> at a fixture folder and loads <see cref="Rules"/> from a fixture file.
    /// </summary>
    public sealed class ValidationContext
    {
        /// <summary>Where <see cref="ForProject"/> finds the module rules, relative to <see cref="RepoRoot"/>.</summary>
        public const string RulesFileInRepo = "tools/checks/module-rules.json";

        /// <summary>The repository root (it holds <c>tools/</c> and <c>unity/</c>).</summary>
        public string RepoRoot;

        /// <summary>
        /// The Unity project folder. Relative paths in this context (<see cref="ModulesRoot"/>,
        /// <see cref="MainScenePath"/>) are relative to it, as asset paths are.
        /// </summary>
        public string ProjectRoot;

        /// <summary>The folder that holds the module folders: project-relative, or absolute for a fixture.</summary>
        public string ModulesRoot = "Assets/_Project/Modules";

        public string MainScenePath = "Assets/_Project/Scenes/Main.unity";

        /// <summary>
        /// The scenes G-5 judges, as asset paths. <c>null</c> (the default) means the enabled scenes of the build
        /// settings. A test points the rule at temporary scenes without touching the build settings.
        /// </summary>
        public string[] ScenePaths;

        /// <summary>The value of <see cref="AssetSearchRoot"/> that stands for the whole project.</summary>
        public const string DefaultAssetSearchRoot = "Assets";

        /// <summary>
        /// The asset folder the editor rules scan: G-4 looks for volume profiles under it, G-5 for prefabs. The
        /// default, <c>Assets</c>, means everything for G-4 and <c>Assets/_Project</c> for G-5 (spec section 8.1).
        /// A test sets a narrower folder, which both rules then use as it is.
        /// </summary>
        public string AssetSearchRoot = DefaultAssetSearchRoot;

        public ModuleRules Rules;

        /// <summary>
        /// A context for the open project. The repository root is three levels above <c>Assets</c>
        /// (<c>&lt;repo&gt;/unity/MotionBowling/Assets</c>); the rules come from <see cref="RulesFileInRepo"/>.
        /// </summary>
        public static ValidationContext ForProject()
        {
            string assets = Application.dataPath;
            string repoRoot = Path.GetFullPath(Path.Combine(assets, "..", "..", ".."));
            return new ValidationContext
            {
                RepoRoot = repoRoot,
                ProjectRoot = Path.GetFullPath(Path.Combine(assets, "..")),
                Rules = ModuleRules.Load(Path.Combine(repoRoot, RulesFileInRepo)),
            };
        }

        /// <summary>The path as a violation prints it: the module folder (or a file in it) under <see cref="ModulesRoot"/>.</summary>
        internal string ModulePath(string module, string relative = null)
        {
            string root = ModulesRoot.Replace('\\', '/').TrimEnd('/');
            return relative == null ? $"{root}/{module}" : $"{root}/{module}/{relative}";
        }

        /// <summary>The absolute path on disk of a path in this context (absolute as is, else under <see cref="ProjectRoot"/>).</summary>
        internal string ResolvePath(string path)
        {
            if (Path.IsPathRooted(path)) return Path.GetFullPath(path);
            if (string.IsNullOrEmpty(ProjectRoot))
                throw new InvalidOperationException($"ProjectRoot is not set, so the relative path '{path}' can't be resolved.");
            return Path.GetFullPath(Path.Combine(ProjectRoot, path));
        }
    }
}
