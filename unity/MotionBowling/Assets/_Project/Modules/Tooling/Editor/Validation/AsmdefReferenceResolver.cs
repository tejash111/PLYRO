using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Bowling.Tooling.Editor
{
    /// <summary>
    /// Turns an asmdef <c>references</c> entry into an assembly name. An entry is either the name itself or
    /// <c>GUID:&lt;32 hex&gt;</c>, which Unity writes when a reference is added in the inspector. A GUID is
    /// resolved through the AssetDatabase to the asmdef asset and its <c>name</c>. Both M-1 (is the Contracts
    /// reference there) and M-2 (is the reference allowed) read references through this one helper.
    /// </summary>
    internal static class AsmdefReferenceResolver
    {
        const string GuidPrefix = "GUID:";

        static readonly Regex GuidReference = new Regex("^GUID:[0-9a-fA-F]{32}$");

        /// <summary>
        /// The assembly name of a reference: the entry itself when it is a name, the resolved <c>name</c> when it
        /// is a GUID. False when a GUID can't be resolved (malformed, unknown, or not an asmdef).
        /// </summary>
        public static bool TryResolve(string reference, out string assemblyName)
        {
            assemblyName = reference;
            if (!reference.StartsWith(GuidPrefix, StringComparison.Ordinal)) return true;

            assemblyName = null;
            if (!GuidReference.IsMatch(reference)) return false;
            string assetPath = AssetDatabase.GUIDToAssetPath(reference.Substring(GuidPrefix.Length));
            if (string.IsNullOrEmpty(assetPath)) return false;
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            if (asset == null) return false;
            try
            {
                assemblyName = AsmdefFile.Parse(asset.text).Name;
            }
            catch (FormatException)
            {
                return false;
            }
            return !string.IsNullOrEmpty(assemblyName);
        }

        /// <summary>True if any entry names the assembly, directly or through a GUID that resolves to it.</summary>
        public static bool ReferencesAssembly(IEnumerable<string> references, string assemblyName)
        {
            return references.Any(reference => TryResolve(reference, out string name) && name == assemblyName);
        }
    }
}
