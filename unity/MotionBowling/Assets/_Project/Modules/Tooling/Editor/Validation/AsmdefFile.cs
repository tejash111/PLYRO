using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bowling.Tooling.Editor
{
    /// <summary>
    /// The fields of an asmdef that M-1 and M-2 judge, read as plain JSON from disk (no AssetDatabase), with
    /// the line of each field for the violation output. Absent fields read as Unity reads them:
    /// <c>autoReferenced</c> defaults to true, every other flag to false and every list to empty.
    /// </summary>
    internal sealed class AsmdefFile
    {
        readonly JObject _json;

        AsmdefFile(JObject json)
        {
            _json = json;
            Name = Text("name");
            RootNamespace = Text("rootNamespace");
            References = List("references");
            IncludePlatforms = List("includePlatforms");
            PrecompiledReferences = List("precompiledReferences");
            DefineConstraints = List("defineConstraints");
            AllowUnsafeCode = Flag("allowUnsafeCode", false);
            OverrideReferences = Flag("overrideReferences", false);
            AutoReferenced = Flag("autoReferenced", true);
        }

        public string Name { get; }
        public string RootNamespace { get; }
        public IReadOnlyList<string> References { get; }
        public IReadOnlyList<string> IncludePlatforms { get; }
        public IReadOnlyList<string> PrecompiledReferences { get; }
        public IReadOnlyList<string> DefineConstraints { get; }
        public bool AllowUnsafeCode { get; }
        public bool OverrideReferences { get; }
        public bool AutoReferenced { get; }

        /// <summary>Parses asmdef text. Throws <see cref="FormatException"/> if it is not JSON or a field has the wrong type.</summary>
        public static AsmdefFile Parse(string text)
        {
            try
            {
                return new AsmdefFile(JObject.Parse(text));
            }
            catch (JsonException e)
            {
                throw new FormatException(e.Message, e);
            }
        }

        /// <summary>Reads and parses a file. Throws <see cref="IOException"/> or <see cref="FormatException"/>.</summary>
        public static AsmdefFile Read(string path) => Parse(File.ReadAllText(path));

        /// <summary>The 1-based line of a top-level field, or 0 if it is absent.</summary>
        public int LineOf(string field) => LineOf(_json.Property(field));

        /// <summary>The 1-based line of one string in a top-level list, or of the list itself, or 0 if neither is there.</summary>
        public int LineOf(string listField, string entry)
        {
            JProperty property = _json.Property(listField);
            JToken item = (property?.Value as JArray)?.FirstOrDefault(i => (string)i == entry);
            int line = LineOf(item);
            return line > 0 ? line : LineOf(property);
        }

        static int LineOf(JToken token) => token is IJsonLineInfo info && info.HasLineInfo() ? info.LineNumber : 0;

        string Text(string field)
        {
            JToken token = _json[field];
            if (token == null) return "";
            if (token.Type != JTokenType.String) throw new FormatException($"\"{field}\" must be a string");
            return (string)token;
        }

        bool Flag(string field, bool fallback)
        {
            JToken token = _json[field];
            if (token == null) return fallback;
            if (token.Type != JTokenType.Boolean) throw new FormatException($"\"{field}\" must be true or false");
            return token.Value<bool>();
        }

        IReadOnlyList<string> List(string field)
        {
            JToken token = _json[field];
            if (token == null) return new string[0];
            if (!(token is JArray array) || array.Any(item => item.Type != JTokenType.String))
                throw new FormatException($"\"{field}\" must be a list of strings");
            return array.Select(item => (string)item).ToArray();
        }
    }
}
