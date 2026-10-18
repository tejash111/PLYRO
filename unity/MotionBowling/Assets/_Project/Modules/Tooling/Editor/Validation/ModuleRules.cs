using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bowling.Tooling.Editor
{
    /// <summary>
    /// A typed view of <c>tools/checks/module-rules.json</c>: which modules exist and which asmdefs each has
    /// (<see cref="Modules"/>), and what each assembly may reference (<see cref="Assemblies"/>,
    /// <see cref="TestAssemblies"/>). Foundation spec, section 4 is the source for the values.
    /// </summary>
    /// <remarks>
    /// <para>Reference lists may hold three tokens. <c>$allRuntime</c> (any list) is every <c>Bowling.&lt;Module&gt;</c>
    /// runtime assembly except the test-only one. <c>$module</c> and <c>$moduleAllowed</c> (<c>testAssemblies.references</c>
    /// only) are the module's primary assembly, which is its runtime assembly or, without one, its editor
    /// assembly, and everything that primary assembly may reference.</para>
    /// <para>An <c>assemblies</c> key that ends in <c>*</c> is a prefix, so <c>Bowling.App.Tests.*</c> covers both
    /// App test assemblies. For a test assembly such an entry adds to <c>testAssemblies</c>.</para>
    /// <para>The loader is strict. A missing section, an unknown key, an unknown token or a badly typed value
    /// throws <see cref="InvalidDataException"/>, so a typo can't turn into a rule that quietly allows less (or
    /// nothing).</para>
    /// </remarks>
    public sealed class ModuleRules
    {
        public const string ModuleToken = "$module";
        public const string ModuleAllowedToken = "$moduleAllowed";
        public const string AllRuntimeToken = "$allRuntime";

        static readonly Regex ModuleName = new Regex("^[A-Za-z][A-Za-z0-9]*$");

        /// <summary>Which asmdefs a module has (foundation spec, section 4 module table).</summary>
        public sealed class ModuleInfo
        {
            public ModuleInfo(bool hasRuntime, bool hasEditor, bool hasEditMode, bool hasPlayMode, bool isTestOnly)
            {
                HasRuntime = hasRuntime;
                HasEditor = hasEditor;
                HasEditMode = hasEditMode;
                HasPlayMode = hasPlayMode;
                IsTestOnly = isTestOnly;
            }

            public bool HasRuntime { get; }
            public bool HasEditor { get; }
            public bool HasEditMode { get; }
            public bool HasPlayMode { get; }

            /// <summary>The runtime asmdef never ships (it needs <c>UNITY_INCLUDE_TESTS</c>); only <c>Testing</c>.</summary>
            public bool IsTestOnly { get; }
        }

        /// <summary>What one assembly may reference, from the allowed-references table.</summary>
        public sealed class AssemblyInfo
        {
            public AssemblyInfo(IReadOnlyList<string> references, IReadOnlyList<string> precompiled, bool allowUnsafeCode)
            {
                References = references;
                Precompiled = precompiled;
                AllowUnsafeCode = allowUnsafeCode;
            }

            /// <summary>Assembly names, possibly with tokens. Use <see cref="ExpandReferences"/> to resolve the tokens.</summary>
            public IReadOnlyList<string> References { get; }

            /// <summary>Precompiled DLL names, such as <c>Newtonsoft.Json.dll</c>.</summary>
            public IReadOnlyList<string> Precompiled { get; }

            public bool AllowUnsafeCode { get; }
        }

        ModuleRules(
            IReadOnlyDictionary<string, ModuleInfo> modules,
            IReadOnlyDictionary<string, AssemblyInfo> assemblies,
            AssemblyInfo testAssemblies)
        {
            Modules = modules;
            Assemblies = assemblies;
            TestAssemblies = testAssemblies;
        }

        public IReadOnlyDictionary<string, ModuleInfo> Modules { get; }

        /// <summary>Allowed references per assembly name (a key may end in <c>*</c>); see <see cref="FindAssembly"/>.</summary>
        public IReadOnlyDictionary<string, AssemblyInfo> Assemblies { get; }

        /// <summary>What every module's EditMode and PlayMode test assemblies may reference, besides their own entry.</summary>
        public AssemblyInfo TestAssemblies { get; }

        /// <summary>Reads and checks the file. Throws <see cref="FileNotFoundException"/> or <see cref="InvalidDataException"/>.</summary>
        public static ModuleRules Load(string jsonPath)
        {
            if (!File.Exists(jsonPath)) throw new FileNotFoundException("module rules file not found", jsonPath);
            JObject root;
            try
            {
                root = JObject.Parse(File.ReadAllText(jsonPath));
            }
            catch (JsonException e)
            {
                throw new InvalidDataException($"{jsonPath} is not valid JSON: {e.Message}", e);
            }
            try
            {
                return Parse(root);
            }
            catch (InvalidDataException e)
            {
                throw new InvalidDataException($"{jsonPath}: {e.Message}", e);
            }
        }

        /// <summary>The entry for an assembly: the exact key, else the longest <c>prefix*</c> key that matches. Null if none.</summary>
        public AssemblyInfo FindAssembly(string assemblyName)
        {
            if (Assemblies.TryGetValue(assemblyName, out AssemblyInfo exact)) return exact;
            string best = null;
            foreach (string key in Assemblies.Keys)
            {
                if (!key.EndsWith("*", StringComparison.Ordinal)) continue;
                string prefix = key.Substring(0, key.Length - 1);
                if (assemblyName.StartsWith(prefix, StringComparison.Ordinal) && (best == null || key.Length > best.Length)) best = key;
            }
            return best == null ? null : Assemblies[best];
        }

        /// <summary>The module's runtime assembly, or its editor assembly when it has no runtime assembly.</summary>
        public string PrimaryAssembly(string module)
        {
            if (!Modules.TryGetValue(module, out ModuleInfo info)) throw new ArgumentException($"unknown module '{module}'", nameof(module));
            return info.HasRuntime ? $"Bowling.{module}" : $"Bowling.{module}.Editor";
        }

        /// <summary>Every <c>Bowling.&lt;Module&gt;</c> runtime assembly that ships, sorted by name.</summary>
        public IReadOnlyList<string> RuntimeAssemblies()
        {
            return Modules
                .Where(m => m.Value.HasRuntime && !m.Value.IsTestOnly)
                .Select(m => $"Bowling.{m.Key}")
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Replaces the tokens in a reference list. <paramref name="module"/> is needed only for
        /// <c>$module</c> and <c>$moduleAllowed</c>. The result has no duplicates.
        /// </summary>
        public IReadOnlyList<string> ExpandReferences(IEnumerable<string> entries, string module)
        {
            var result = new List<string>();
            foreach (string entry in entries)
            {
                switch (entry)
                {
                    case AllRuntimeToken:
                        result.AddRange(RuntimeAssemblies());
                        break;
                    case ModuleToken:
                        result.Add(PrimaryAssembly(RequireModule(module, entry)));
                        break;
                    case ModuleAllowedToken:
                        AssemblyInfo primary = FindAssembly(PrimaryAssembly(RequireModule(module, entry)));
                        if (primary != null) result.AddRange(ExpandReferences(primary.References, null));
                        break;
                    default:
                        result.Add(entry);
                        break;
                }
            }
            return result.Distinct().ToList();
        }

        static string RequireModule(string module, string token)
        {
            if (module == null) throw new InvalidOperationException($"'{token}' needs a module to expand against");
            return module;
        }

        // --- Parsing: strict, so a typo can't loosen or empty a rule ---

        static ModuleRules Parse(JObject root)
        {
            RejectUnknown(root, "file", "modules", "assemblies", "testAssemblies");

            var modules = new Dictionary<string, ModuleInfo>(StringComparer.Ordinal);
            foreach (JProperty property in Section(root, "modules").Properties())
            {
                if (!ModuleName.IsMatch(property.Name))
                    throw new InvalidDataException($"module name '{property.Name}' must be letters and digits and start with a letter");
                modules[property.Name] = ParseModule(property.Name, property.Value);
            }

            var assemblies = new Dictionary<string, AssemblyInfo>(StringComparer.Ordinal);
            foreach (JProperty property in Section(root, "assemblies").Properties())
            {
                string where = $"assemblies.{property.Name}";
                int star = property.Name.IndexOf('*');
                if (property.Name.Length == 0 || (star >= 0 && (star != property.Name.Length - 1 || star == 0)))
                    throw new InvalidDataException($"{where}: a '*' is allowed only at the end of a non-empty name");
                assemblies[property.Name] = ParseAssembly(property.Value, where, false);
            }

            AssemblyInfo testAssemblies = ParseAssembly(Section(root, "testAssemblies"), "testAssemblies", true);
            return new ModuleRules(modules, assemblies, testAssemblies);
        }

        static ModuleInfo ParseModule(string name, JToken token)
        {
            string where = $"modules.{name}";
            JObject module = AsObject(token, where);
            RejectUnknown(module, where, "runtime", "editor", "editMode", "playMode", "testOnly");
            bool runtime = Flag(module, "runtime", where, true);
            bool editor = Flag(module, "editor", where, true);
            bool testOnly = Flag(module, "testOnly", where, false);
            if (!runtime && !editor) throw new InvalidDataException($"{where}: needs runtime or editor, or it has no primary assembly");
            if (testOnly && !runtime) throw new InvalidDataException($"{where}: testOnly needs runtime: true");
            return new ModuleInfo(runtime, editor, Flag(module, "editMode", where, true), Flag(module, "playMode", where, true), testOnly);
        }

        static AssemblyInfo ParseAssembly(JToken token, string where, bool isTestList)
        {
            JObject entry = AsObject(token, where);
            if (isTestList) RejectUnknown(entry, where, "references", "precompiled");
            else RejectUnknown(entry, where, "references", "precompiled", "allowUnsafeCode");
            IReadOnlyList<string> references = Strings(entry, "references", where, true);
            foreach (string reference in references.Where(r => r.StartsWith("$", StringComparison.Ordinal)))
            {
                bool moduleToken = reference == ModuleToken || reference == ModuleAllowedToken;
                if (moduleToken && !isTestList) throw new InvalidDataException($"{where}.references: '{reference}' is only allowed in testAssemblies.references");
                if (!moduleToken && reference != AllRuntimeToken) throw new InvalidDataException($"{where}.references: unknown token '{reference}'");
            }
            bool allowUnsafe = !isTestList && Flag(entry, "allowUnsafeCode", where, false);
            return new AssemblyInfo(references, Strings(entry, "precompiled", where, false), allowUnsafe);
        }

        static JObject Section(JObject parent, string key)
        {
            return parent[key] as JObject ?? throw new InvalidDataException($"missing or not an object: \"{key}\"");
        }

        static JObject AsObject(JToken token, string where)
        {
            return token as JObject ?? throw new InvalidDataException($"{where} must be an object");
        }

        static void RejectUnknown(JObject obj, string where, params string[] known)
        {
            string unknown = obj.Properties().Select(p => p.Name).FirstOrDefault(name => !known.Contains(name));
            if (unknown != null) throw new InvalidDataException($"{where}: unknown key \"{unknown}\" (known: {string.Join(", ", known)})");
        }

        static bool Flag(JObject obj, string key, string where, bool required)
        {
            JToken token = obj[key];
            if (token == null)
            {
                if (required) throw new InvalidDataException($"{where}: missing \"{key}\"");
                return false;
            }
            if (token.Type != JTokenType.Boolean) throw new InvalidDataException($"{where}.{key} must be true or false");
            return token.Value<bool>();
        }

        static IReadOnlyList<string> Strings(JObject obj, string key, string where, bool required)
        {
            JToken token = obj[key];
            if (token == null)
            {
                if (required) throw new InvalidDataException($"{where}: missing \"{key}\"");
                return new string[0];
            }
            if (!(token is JArray array) || array.Any(item => item.Type != JTokenType.String))
                throw new InvalidDataException($"{where}.{key} must be a list of strings");
            return array.Select(item => (string)item).ToArray();
        }
    }
}
