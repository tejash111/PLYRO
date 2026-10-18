using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Bowling.Tooling.Editor
{
    /// <summary>
    /// Where the asset-level rules look (foundation spec, section 8): the folder they search, and the vendor
    /// and Tooling folders they leave out.
    /// </summary>
    internal static class ScanScope
    {
        const string FmodFolder = "Assets/Plugins/FMOD/";
        const string ToolingFolder = "Assets/_Project/Modules/Tooling/";
        const string ProjectFolder = "Assets/_Project";

        /// <summary>Vendor assets are not ours to judge.</summary>
        public static bool IsVendor(string assetPath) => assetPath.StartsWith(FmodFolder, StringComparison.Ordinal);

        /// <summary>The Tooling module holds the banned patterns as data, so it is not scanned.</summary>
        public static bool IsTooling(string assetPath) => assetPath.StartsWith(ToolingFolder, StringComparison.Ordinal);

        /// <summary>The folder G-4 searches: <see cref="ValidationContext.AssetSearchRoot"/>.</summary>
        public static string SearchRoot(ValidationContext context) => context.AssetSearchRoot.Replace('\\', '/').TrimEnd('/');

        /// <summary>
        /// The folder G-5 searches for prefabs: <c>Assets/_Project</c>, unless the context narrows
        /// <see cref="ValidationContext.AssetSearchRoot"/>.
        /// </summary>
        public static string PrefabRoot(ValidationContext context)
        {
            string root = SearchRoot(context);
            return root == ValidationContext.DefaultAssetSearchRoot ? ProjectFolder : root;
        }

        /// <summary>The paths of the assets under <paramref name="folder"/> that match the filter, in a stable order.</summary>
        public static IEnumerable<string> AssetPaths(string filter, string folder)
        {
            return AssetDatabase.FindAssets(filter, new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .OrderBy(path => path, StringComparer.Ordinal);
        }
    }
}
