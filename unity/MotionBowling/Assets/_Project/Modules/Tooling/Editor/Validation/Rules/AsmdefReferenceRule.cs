using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Bowling.Tooling.Editor
{
    /// <summary>
    /// M-2: an asmdef references only what <c>module-rules.json</c> allows its assembly (foundation spec,
    /// section 4): assemblies, precompiled DLLs and <c>allowUnsafeCode</c>. It judges the asmdefs M-1 expects,
    /// by their expected identity, so a misnamed asmdef is M-1's finding and not also an unknown assembly here.
    /// A <c>GUID:&lt;32 hex&gt;</c> reference is resolved to an assembly name through the AssetDatabase; one that
    /// can't be resolved is a violation, because nothing can then be said about what it references.
    /// </summary>
    public sealed class AsmdefReferenceRule : IValidationRule
    {
        const string GuidPrefix = "GUID:";

        static readonly Regex GuidReference = new Regex("^GUID:[0-9a-fA-F]{32}$");

        sealed class Allowed
        {
            public Allowed(IEnumerable<string> references, IEnumerable<string> precompiled, bool allowUnsafeCode)
            {
                References = new HashSet<string>(references, StringComparer.Ordinal);
                Precompiled = new HashSet<string>(precompiled, StringComparer.Ordinal);
                AllowUnsafeCode = allowUnsafeCode;
            }

            public HashSet<string> References { get; }
            public HashSet<string> Precompiled { get; }
            public bool AllowUnsafeCode { get; }
        }

        public string Id => "M-2";

        public IEnumerable<Violation> Run(ValidationContext context)
        {
            var violations = new List<Violation>();
            string modulesDir = context.ResolvePath(context.ModulesRoot);
            if (!Directory.Exists(modulesDir))
            {
                violations.Add(Fail(context.ModulesRoot.Replace('\\', '/'), 0, "modules root folder not found"));
                return violations;
            }

            foreach (KeyValuePair<string, ModuleRules.ModuleInfo> module in context.Rules.Modules.OrderBy(m => m.Key, StringComparer.Ordinal))
            {
                foreach (ExpectedAsmdef expected in ExpectedAsmdef.For(module.Key, module.Value))
                    CheckAsmdef(context, Path.Combine(modulesDir, module.Key), expected, violations);
            }
            return violations;
        }

        void CheckAsmdef(ValidationContext context, string moduleDir, ExpectedAsmdef expected, List<Violation> violations)
        {
            string file = Path.Combine(moduleDir, expected.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(file)) return; // M-1 reports a missing asmdef (or module folder)

            string path = context.ModulePath(expected.Module, expected.RelativePath);
            AsmdefFile asmdef;
            try
            {
                asmdef = AsmdefFile.Read(file);
            }
            catch (Exception e) when (e is IOException || e is FormatException || e is UnauthorizedAccessException)
            {
                violations.Add(Fail(path, 0, $"cannot read asmdef: {e.Message}"));
                return;
            }

            Allowed allowed = AllowedFor(context.Rules, expected);
            if (allowed == null)
            {
                violations.Add(Fail(path, 0, $"{expected.AssemblyName} is not listed under assemblies in tools/checks/module-rules.json"));
                return;
            }

            foreach (string reference in asmdef.References)
            {
                int line = asmdef.LineOf("references", reference);
                string name = reference;
                if (reference.StartsWith(GuidPrefix, StringComparison.Ordinal) && !TryResolveGuid(reference, out name))
                {
                    violations.Add(Fail(path, line, $"unresolvable GUID reference '{reference}'"));
                    continue;
                }
                if (allowed.References.Contains(name)) continue;
                string via = name == reference ? "" : $" (as {reference})";
                violations.Add(Fail(path, line, $"references '{name}'{via}, which module-rules.json does not allow {expected.AssemblyName} to reference"));
            }

            foreach (string dll in asmdef.PrecompiledReferences.Where(d => !allowed.Precompiled.Contains(d)))
                violations.Add(Fail(path, asmdef.LineOf("precompiledReferences", dll), $"lists precompiled reference '{dll}', which module-rules.json does not allow {expected.AssemblyName}"));

            if (asmdef.AllowUnsafeCode && !allowed.AllowUnsafeCode)
                violations.Add(Fail(path, asmdef.LineOf("allowUnsafeCode"), $"sets allowUnsafeCode: true, which module-rules.json does not allow {expected.AssemblyName}"));
        }

        /// <summary>
        /// A non-test assembly gets its own entry. A test assembly gets <c>testAssemblies</c> (with
        /// <c>$module</c> and <c>$moduleAllowed</c> expanded for its module), what its module's primary assembly
        /// may load as precompiled DLLs, and its own entry if there is one (<c>Bowling.App.Tests.*</c>).
        /// Null when a non-test assembly has no entry.
        /// </summary>
        static Allowed AllowedFor(ModuleRules rules, ExpectedAsmdef expected)
        {
            ModuleRules.AssemblyInfo own = rules.FindAssembly(expected.AssemblyName);
            if (!expected.IsTest)
                return own == null ? null : new Allowed(rules.ExpandReferences(own.References, expected.Module), own.Precompiled, own.AllowUnsafeCode);

            List<string> references = rules.ExpandReferences(rules.TestAssemblies.References, expected.Module).ToList();
            List<string> precompiled = rules.TestAssemblies.Precompiled.ToList();
            ModuleRules.AssemblyInfo primary = rules.FindAssembly(rules.PrimaryAssembly(expected.Module));
            if (primary != null) precompiled.AddRange(primary.Precompiled);
            if (own != null)
            {
                references.AddRange(rules.ExpandReferences(own.References, expected.Module));
                precompiled.AddRange(own.Precompiled);
            }
            return new Allowed(references, precompiled, own != null && own.AllowUnsafeCode);
        }

        /// <summary>Resolves <c>GUID:&lt;hex&gt;</c> to the <c>name</c> of the asmdef asset with that GUID.</summary>
        static bool TryResolveGuid(string reference, out string assemblyName)
        {
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

        Violation Fail(string path, int line, string message) => new Violation(Id, path, line, message, false);
    }
}
