using System.Collections.Generic;

namespace Bowling.Tooling.Editor
{
    /// <summary>
    /// An asmdef that foundation spec section 4 requires of a module: where it lives, and the name, root
    /// namespace and platforms its kind demands. M-1 judges the file against this, and M-2 uses it to
    /// know which assembly the file is, so a misnamed asmdef doesn't also read as an unknown assembly.
    /// </summary>
    internal sealed class ExpectedAsmdef
    {
        public enum AsmdefKind
        {
            Runtime,
            Editor,
            EditModeTests,
            PlayModeTests,
        }

        ExpectedAsmdef(string module, AsmdefKind kind, string assemblyName, string rootNamespace, string folder, string[] includePlatforms)
        {
            Module = module;
            Kind = kind;
            AssemblyName = assemblyName;
            RootNamespace = rootNamespace;
            RelativePath = $"{folder}/{assemblyName}.asmdef";
            IncludePlatforms = includePlatforms;
        }

        public string Module { get; }
        public AsmdefKind Kind { get; }
        public string AssemblyName { get; }
        public string RootNamespace { get; }

        /// <summary>Under the module folder, with forward slashes, for example <c>Runtime/Bowling.Gameplay.asmdef</c>.</summary>
        public string RelativePath { get; }

        /// <summary>The exact <c>includePlatforms</c> the kind requires: none for runtime and PlayMode, the editor otherwise.</summary>
        public string[] IncludePlatforms { get; }

        public bool IsTest => Kind == AsmdefKind.EditModeTests || Kind == AsmdefKind.PlayModeTests;

        /// <summary>The asmdefs a module has, in a fixed order: runtime, editor, EditMode tests, PlayMode tests.</summary>
        public static IReadOnlyList<ExpectedAsmdef> For(string module, ModuleRules.ModuleInfo info)
        {
            var result = new List<ExpectedAsmdef>();
            var editorOnly = new[] { "Editor" };
            var anyPlatform = new string[0];
            if (info.HasRuntime)
                result.Add(new ExpectedAsmdef(module, AsmdefKind.Runtime, $"Bowling.{module}", $"Bowling.{module}", "Runtime", anyPlatform));
            if (info.HasEditor)
                result.Add(new ExpectedAsmdef(module, AsmdefKind.Editor, $"Bowling.{module}.Editor", $"Bowling.{module}.Editor", "Editor", editorOnly));
            if (info.HasEditMode)
                result.Add(new ExpectedAsmdef(module, AsmdefKind.EditModeTests, $"Bowling.{module}.Tests.EditMode", $"Bowling.{module}.Tests", "Tests/EditMode", editorOnly));
            if (info.HasPlayMode)
                result.Add(new ExpectedAsmdef(module, AsmdefKind.PlayModeTests, $"Bowling.{module}.Tests.PlayMode", $"Bowling.{module}.Tests", "Tests/PlayMode", anyPlatform));
            return result;
        }
    }
}
