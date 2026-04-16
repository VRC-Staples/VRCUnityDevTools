using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Staples.DevTools.Editor
{
    internal static class ProjectBindingTool
    {
        internal static void ShowCurrentProjectBinding()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                Debug.LogWarning("[Dev Tools] Could not resolve the current Unity project root.");
                return;
            }

            var manifestDependencies = GetManifestDependencies(projectRoot);
            var embeddedPackages = GetEmbeddedPackageBindings(projectRoot);
            var sb = new StringBuilder(1024);

            sb.AppendLine($"[Dev Tools] Current project bindings for '{Path.GetFileName(projectRoot)}'");
            sb.AppendLine($"Project root: {projectRoot}");
            sb.AppendLine();
            sb.AppendLine("Manifest dependencies:");

            if (manifestDependencies.Count == 0)
            {
                sb.AppendLine("- <none>");
            }
            else
            {
                foreach (var dependency in manifestDependencies.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                    sb.AppendLine($"- {dependency.Key} => {dependency.Value} [{DescribeDependencyValue(dependency.Value)}]");
            }

            sb.AppendLine();
            sb.AppendLine("Embedded packages:");

            if (embeddedPackages.Count == 0)
            {
                sb.AppendLine("- <none>");
            }
            else
            {
                foreach (var embedded in embeddedPackages.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                    sb.AppendLine($"- {embedded.Key} => {embedded.Value}");
            }

            Debug.Log(sb.ToString());
        }

        private static Dictionary<string, string> GetManifestDependencies(string projectRoot)
        {
            string manifestPath = Path.Combine(projectRoot, "Packages", "manifest.json");
            if (!File.Exists(manifestPath))
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            string json;
            try { json = File.ReadAllText(manifestPath); }
            catch { return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); }

            string depsBody = ExtractTopLevelJsonObject(json, "dependencies");
            if (string.IsNullOrEmpty(depsBody))
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            return ParseSimpleJsonObject(depsBody);
        }

        private static Dictionary<string, string> GetEmbeddedPackageBindings(string projectRoot)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string packagesRoot = Path.Combine(projectRoot, "Packages");
            if (!Directory.Exists(packagesRoot))
                return result;

            foreach (var dir in Directory.GetDirectories(packagesRoot))
            {
                string pkgJsonPath = Path.Combine(dir, "package.json");
                if (!File.Exists(pkgJsonPath))
                    continue;

                string json;
                try { json = File.ReadAllText(pkgJsonPath); }
                catch { continue; }

                string name = ExtractJsonString(json, "name");
                if (!string.IsNullOrEmpty(name))
                    result[name] = dir;
            }

            return result;
        }

        private static string DescribeDependencyValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unknown";
            if (value.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
                return "local path";
            if (value.StartsWith("git+", StringComparison.OrdinalIgnoreCase) || value.Contains("://"))
                return "remote";
            return "version / registry";
        }

        private static string ExtractTopLevelJsonObject(string json, string sectionName)
        {
            int sectionIndex = json.IndexOf($"\"{sectionName}\"", StringComparison.Ordinal);
            if (sectionIndex < 0)
                return null;

            int braceStart = json.IndexOf('{', sectionIndex);
            if (braceStart < 0)
                return null;

            int depth = 0;
            for (int i = braceStart; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return json.Substring(braceStart + 1, i - braceStart - 1);
                }
            }

            return null;
        }

        private static Dictionary<string, string> ParseSimpleJsonObject(string body)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int pos = 0;
            while (pos < body.Length)
            {
                int keyStart = body.IndexOf('"', pos);
                if (keyStart < 0)
                    break;

                int keyEnd = FindStringEnd(body, keyStart + 1);
                if (keyEnd < 0)
                    break;

                string key = body.Substring(keyStart + 1, keyEnd - keyStart - 1)
                    .Replace("\\\"", "\"")
                    .Replace("\\\\", "\\");

                int colonIndex = body.IndexOf(':', keyEnd + 1);
                if (colonIndex < 0)
                    break;

                int valueStart = body.IndexOf('"', colonIndex + 1);
                if (valueStart < 0)
                    break;

                int valueEnd = FindStringEnd(body, valueStart + 1);
                if (valueEnd < 0)
                    break;

                string value = body.Substring(valueStart + 1, valueEnd - valueStart - 1)
                    .Replace("\\\"", "\"")
                    .Replace("\\\\", "\\");

                result[key] = value;
                pos = valueEnd + 1;
            }

            return result;
        }

        private static int FindStringEnd(string text, int start)
        {
            for (int i = start; i < text.Length; i++)
            {
                if (text[i] == '\\')
                {
                    i++;
                    continue;
                }

                if (text[i] == '"')
                    return i;
            }

            return -1;
        }

        private static string ExtractJsonString(string json, string key)
        {
            int idx = json.IndexOf($"\"{key}\"", StringComparison.Ordinal);
            if (idx < 0)
                return null;

            int colon = json.IndexOf(':', idx);
            if (colon < 0)
                return null;

            int q1 = json.IndexOf('"', colon + 1);
            if (q1 < 0)
                return null;

            int q2 = FindStringEnd(json, q1 + 1);
            if (q2 < 0)
                return null;

            return json.Substring(q1 + 1, q2 - q1 - 1)
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
        }
    }
}
