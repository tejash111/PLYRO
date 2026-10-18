using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Bowling.Tooling.Editor
{
    /// <summary>
    /// The rules for the project's package set (foundation spec, section 3), as pure checks on the
    /// <c>dependencies</c> of a <c>Packages/manifest.json</c> (package name to version). Each check returns the
    /// names of the offending packages, so an empty list means the check passes. The caller parses the JSON,
    /// because this assembly references no JSON library.
    /// </summary>
    public static class PackageManifestRules
    {
        /// <summary>Built-in packages: always allowed.</summary>
        public const string ModulePrefix = "com.unity.modules.";

        public const string CinemachineName = "com.unity.cinemachine";

        /// <summary>Allowed besides <see cref="PackageInstaller.Required"/> and the built-in modules.</summary>
        public static readonly string[] ApprovedExtras = { "com.unity.ide.rider", "com.unity.ide.visualstudio" };

        static readonly Regex ExactVersion = new Regex(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?$");

        /// <summary>Required packages that are not in the manifest.</summary>
        public static IReadOnlyList<string> MissingRequired(IReadOnlyDictionary<string, string> dependencies)
        {
            return PackageInstaller.Required.Where(name => !dependencies.ContainsKey(name)).ToList();
        }

        /// <summary>Packages that are neither built-in modules, required, nor approved extras.</summary>
        public static IReadOnlyList<string> Unapproved(IReadOnlyDictionary<string, string> dependencies)
        {
            return dependencies.Keys
                .Where(name => !name.StartsWith(ModulePrefix, StringComparison.Ordinal))
                .Where(name => !PackageInstaller.Required.Contains(name) && !ApprovedExtras.Contains(name))
                .ToList();
        }

        /// <summary>
        /// Required packages whose version is not exact (for example a range, a path or a URL). A required
        /// package that is absent is not judged here; <see cref="MissingRequired"/> reports it.
        /// </summary>
        public static IReadOnlyList<string> InexactVersions(IReadOnlyDictionary<string, string> dependencies)
        {
            return PackageInstaller.Required
                .Where(name => dependencies.TryGetValue(name, out string version) && !ExactVersion.IsMatch(version))
                .ToList();
        }

        /// <summary>
        /// Cinemachine if it is present with a version outside the 3.x line. Absent is not judged here;
        /// <see cref="MissingRequired"/> reports it.
        /// </summary>
        public static IReadOnlyList<string> CinemachineNotMajor3(IReadOnlyDictionary<string, string> dependencies)
        {
            bool present = dependencies.TryGetValue(CinemachineName, out string version);
            bool major3 = present && version.StartsWith("3.", StringComparison.Ordinal);
            return present && !major3 ? new[] { CinemachineName } : new string[0];
        }
    }
}
