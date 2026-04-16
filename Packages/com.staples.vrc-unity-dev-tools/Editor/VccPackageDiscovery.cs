using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Staples.DevTools.Editor
{
    internal static class VccPackageDiscovery
    {
        internal static IEnumerable<string> FindVccProjectRoots()
            => ReadVccSettingsArray("userProjects")
                .Where(Directory.Exists)
                .Select(Path.GetFullPath);

        internal static List<VccLocalPackage> DiscoverVccLocalPackages()
        {
            var result = new List<VccLocalPackage>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var folder in ReadVccSettingsArray("userPackageFolders"))
            {
                if (!Directory.Exists(folder))
                    continue;

                TryAddVccPackage(folder, result, seen);
                foreach (var sub in Directory.GetDirectories(folder))
                    TryAddVccPackage(sub, result, seen);
            }

            foreach (var path in ReadVccSettingsArray("userPackages"))
                TryAddVccPackage(path, result, seen);

            return result;
        }

        internal static string TryGetManifestFileDependencyPath(string projectRoot, string packageName)
        {
            var dependencies = GetManifestDependencies(projectRoot);
            if (!dependencies.TryGetValue(packageName, out string dependencyValue)
                || string.IsNullOrWhiteSpace(dependencyValue)
                || !dependencyValue.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
                return null;

            string rawPath = dependencyValue.Substring("file:".Length).Trim();
            if (string.IsNullOrWhiteSpace(rawPath))
                return null;

            string normalizedPath = rawPath.Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(normalizedPath))
                return Path.GetFullPath(normalizedPath);

            return Path.GetFullPath(Path.Combine(projectRoot, normalizedPath));
        }

        internal static string GetProjectPackageStatus(string projectRoot, string packageName)
        {
            string fileDependencyPath = TryGetManifestFileDependencyPath(projectRoot, packageName);
            if (!string.IsNullOrEmpty(fileDependencyPath))
                return $"local → {fileDependencyPath}";

            string embeddedPath = Path.Combine(projectRoot, "Packages", packageName);
            if (Directory.Exists(embeddedPath) && File.Exists(Path.Combine(embeddedPath, "package.json")))
            {
                try
                {
                    bool isLink = (File.GetAttributes(embeddedPath) & FileAttributes.ReparsePoint) != 0;
                    return isLink ? "linked embedded" : "embedded";
                }
                catch
                {
                    return "embedded";
                }
            }

            var dependencies = GetManifestDependencies(projectRoot);
            if (dependencies.TryGetValue(packageName, out string dependencyValue) && !string.IsNullOrWhiteSpace(dependencyValue))
                return $"manifest → {dependencyValue}";

            return "not installed";
        }

        internal static List<VccLocalPackage> DiscoverSwitchablePackages(string projectRoot)
        {
            var result = new List<VccLocalPackage>();
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var manifestDependencies = GetManifestDependencies(projectRoot);
            var embeddedPackages = GetEmbeddedPackageBindings(projectRoot);
            var vccLocalPackages = BuildRelevantVccLocalPackages(projectRoot);
            var vccLocalPackagesByName = vccLocalPackages
                .GroupBy(x => x.PackageName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
            var packageNames = new HashSet<string>(manifestDependencies.Keys, StringComparer.OrdinalIgnoreCase);
            foreach (string embeddedPackageName in embeddedPackages.Keys)
                packageNames.Add(embeddedPackageName);

            foreach (string packageName in packageNames.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                string manifestLocalPath = TryGetManifestFileDependencyPath(projectRoot, packageName);

                vccLocalPackagesByName.TryGetValue(packageName, out var matchingLocalPackages);

                var localCandidates = new List<string>();
                if (!string.IsNullOrWhiteSpace(manifestLocalPath))
                    localCandidates.Add(manifestLocalPath);

                if (matchingLocalPackages != null)
                {
                    foreach (var localPackage in matchingLocalPackages)
                    {
                        if (!string.IsNullOrWhiteSpace(localPackage.LocalPath)
                            && !localCandidates.Any(existing => string.Equals(existing, localPackage.LocalPath, StringComparison.OrdinalIgnoreCase)))
                        {
                            localCandidates.Add(localPackage.LocalPath);
                        }
                    }
                }

                if (localCandidates.Count == 0)
                    continue;

                string preferredLocalPath = localCandidates[0];
                string version = matchingLocalPackages != null
                    ? matchingLocalPackages.Select(x => x.Version).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                    : null;
                if (string.IsNullOrWhiteSpace(version))
                    version = TryReadPackageVersionFromDirectory(preferredLocalPath)
                        ?? (manifestDependencies.TryGetValue(packageName, out string dependencyValue) ? dependencyValue : string.Empty);

                string displayName = matchingLocalPackages != null
                    ? matchingLocalPackages.Select(x => x.DisplayName).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                    : null;
                if (string.IsNullOrWhiteSpace(displayName))
                    displayName = packageName;

                if (!seenNames.Add(packageName))
                    continue;

                result.Add(new VccLocalPackage
                {
                    PackageName = packageName,
                    DisplayName = displayName,
                    Version = version ?? string.Empty,
                    LocalPath = preferredLocalPath,
                });
            }

            return result
                .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.PackageName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<VccLocalPackage> BuildRelevantVccLocalPackages(string projectRoot)
        {
            var manifestDependencies = GetManifestDependencies(projectRoot);
            var embeddedPackages = GetEmbeddedPackageBindings(projectRoot);
            var projectPackageNames = new HashSet<string>(manifestDependencies.Keys, StringComparer.OrdinalIgnoreCase);
            foreach (string embeddedPackageName in embeddedPackages.Keys)
                projectPackageNames.Add(embeddedPackageName);

            return DiscoverVccLocalPackages()
                .Where(x => projectPackageNames.Contains(x.PackageName))
                .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.PackageName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void TryAddVccPackage(string path, List<VccLocalPackage> result, HashSet<string> seen)
        {
            string pkgJsonPath = Path.Combine(path, "package.json");
            if (!File.Exists(pkgJsonPath))
                return;

            string fullPath = Path.GetFullPath(path);
            if (!seen.Add(fullPath))
                return;

            string content;
            try { content = File.ReadAllText(pkgJsonPath); }
            catch { return; }

            string name = ExtractJsonString(content, "name");
            if (string.IsNullOrEmpty(name))
                return;

            result.Add(new VccLocalPackage
            {
                PackageName = name,
                DisplayName = ExtractJsonString(content, "displayName") ?? name,
                Version = ExtractJsonString(content, "version") ?? string.Empty,
                LocalPath = fullPath,
            });
        }

        private static IEnumerable<string> ReadVccSettingsArray(string key)
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string settingsPath = Path.Combine(localAppData, "VRChatCreatorCompanion", "settings.json");
            if (!File.Exists(settingsPath))
                return Enumerable.Empty<string>();

            string json;
            try { json = File.ReadAllText(settingsPath); }
            catch { return Enumerable.Empty<string>(); }

            return ExtractJsonStringArray(json, key);
        }

        private static IEnumerable<string> ExtractJsonStringArray(string json, string key)
        {
            int idx = json.IndexOf($"\"{key}\"", StringComparison.Ordinal);
            if (idx < 0)
                return Enumerable.Empty<string>();

            int bracketStart = json.IndexOf('[', idx);
            if (bracketStart < 0)
                return Enumerable.Empty<string>();

            int bracketEnd = FindMatchingBracket(json, bracketStart);
            if (bracketEnd < 0)
                return Enumerable.Empty<string>();

            return ParseJsonStringArray(json.Substring(bracketStart + 1, bracketEnd - bracketStart - 1));
        }

        private static IEnumerable<string> ParseJsonStringArray(string content)
        {
            int pos = 0;
            while (pos < content.Length)
            {
                int qStart = content.IndexOf('"', pos);
                if (qStart < 0)
                    break;

                int qEnd = qStart + 1;
                while (qEnd < content.Length)
                {
                    if (content[qEnd] == '\\')
                    {
                        qEnd += 2;
                        continue;
                    }

                    if (content[qEnd] == '"')
                        break;

                    qEnd++;
                }

                if (qEnd >= content.Length)
                    break;

                yield return content.Substring(qStart + 1, qEnd - qStart - 1)
                    .Replace("\\\\", "\\")
                    .Replace("\\/", "/");
                pos = qEnd + 1;
            }
        }

        private static string ExtractJsonString(string json, string key)
        {
            int idx = json.IndexOf($"\"{key}\"", StringComparison.Ordinal);
            if (idx < 0)
                return null;

            int colon = json.IndexOf(':', idx);
            if (colon < 0)
                return null;

            int valueStart = colon + 1;
            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
                valueStart++;

            if (valueStart >= json.Length || json[valueStart] != '"')
                return null;

            int valueEnd = valueStart + 1;
            while (valueEnd < json.Length)
            {
                if (json[valueEnd] == '\\')
                {
                    valueEnd += 2;
                    continue;
                }

                if (json[valueEnd] == '"')
                    break;

                valueEnd++;
            }

            if (valueEnd >= json.Length)
                return null;

            return json.Substring(valueStart + 1, valueEnd - valueStart - 1);
        }

        private static int FindMatchingBrace(string text, int openIndex)
        {
            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = openIndex; i < text.Length; i++)
            {
                char c = text[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (c == '"')
                        inString = false;

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        private static int FindMatchingBracket(string text, int openIndex)
        {
            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = openIndex; i < text.Length; i++)
            {
                char c = text[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (c == '"')
                        inString = false;

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '[') depth++;
                else if (c == ']')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        private static Dictionary<string, string> GetManifestDependencies(string projectRoot)
        {
            string manifestPath = Path.Combine(projectRoot, "Packages", "manifest.json");
            if (!File.Exists(manifestPath))
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                string json = File.ReadAllText(manifestPath);
                return ReadTopLevelJsonObjectStringValues(json, "dependencies");
            }
            catch
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static Dictionary<string, string> GetEmbeddedPackageBindings(string projectRoot)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string packagesRoot = Path.Combine(projectRoot, "Packages");
            if (!Directory.Exists(packagesRoot))
                return result;

            string[] packageDirs;
            try
            {
                packageDirs = Directory.GetDirectories(packagesRoot);
            }
            catch
            {
                return result;
            }

            for (int i = 0; i < packageDirs.Length; i++)
            {
                string packageDir = packageDirs[i];
                string packageJsonPath = Path.Combine(packageDir, "package.json");
                if (!File.Exists(packageJsonPath))
                    continue;

                string content;
                try
                {
                    content = File.ReadAllText(packageJsonPath);
                }
                catch
                {
                    continue;
                }

                string packageName = ExtractJsonString(content, "name");
                if (string.IsNullOrWhiteSpace(packageName))
                    packageName = Path.GetFileName(packageDir);

                bool isLink = false;
                try
                {
                    isLink = (File.GetAttributes(packageDir) & FileAttributes.ReparsePoint) != 0;
                }
                catch
                {
                }

                result[packageName] = isLink ? $"linked => {packageDir}" : packageDir;
            }

            return result;
        }

        private static Dictionary<string, string> ReadTopLevelJsonObjectStringValues(string json, string sectionName)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(json))
                return result;

            string sectionNeedle = $"\"{sectionName}\"";
            int sectionIndex = json.IndexOf(sectionNeedle, StringComparison.Ordinal);
            if (sectionIndex < 0)
                return result;

            int sectionBraceStart = json.IndexOf('{', sectionIndex);
            if (sectionBraceStart < 0)
                return result;

            int sectionBraceEnd = FindMatchingBrace(json, sectionBraceStart);
            if (sectionBraceEnd < 0)
                return result;

            int pos = sectionBraceStart + 1;
            while (pos < sectionBraceEnd)
            {
                while (pos < sectionBraceEnd && (char.IsWhiteSpace(json[pos]) || json[pos] == ','))
                    pos++;

                if (pos >= sectionBraceEnd || json[pos] != '"')
                    break;

                string key = ReadJsonStringToken(json, ref pos);
                if (key == null)
                    break;

                while (pos < sectionBraceEnd && char.IsWhiteSpace(json[pos]))
                    pos++;

                if (pos >= sectionBraceEnd || json[pos] != ':')
                    break;

                pos++;
                while (pos < sectionBraceEnd && char.IsWhiteSpace(json[pos]))
                    pos++;

                string value;
                if (pos < sectionBraceEnd && json[pos] == '"')
                {
                    value = ReadJsonStringToken(json, ref pos) ?? string.Empty;
                }
                else
                {
                    int valueStart = pos;
                    while (pos < sectionBraceEnd && json[pos] != ',')
                        pos++;
                    value = json.Substring(valueStart, pos - valueStart).Trim();
                }

                result[key] = value;
            }

            return result;
        }

        private static string ReadJsonStringToken(string text, ref int pos)
        {
            if (pos < 0 || pos >= text.Length || text[pos] != '"')
                return null;

            pos++;
            var sb = new StringBuilder();
            while (pos < text.Length)
            {
                char c = text[pos++];
                if (c == '\\')
                {
                    if (pos >= text.Length)
                        break;

                    char escaped = text[pos++];
                    switch (escaped)
                    {
                        case '\\': sb.Append('\\'); break;
                        case '"': sb.Append('"'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (pos + 4 <= text.Length)
                            {
                                string hex = text.Substring(pos, 4);
                                if (ushort.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out ushort code))
                                {
                                    sb.Append((char)code);
                                    pos += 4;
                                }
                            }
                            break;
                        default:
                            sb.Append(escaped);
                            break;
                    }

                    continue;
                }

                if (c == '"')
                    return sb.ToString();

                sb.Append(c);
            }

            return sb.ToString();
        }

        private static string TryReadPackageVersionFromDirectory(string packageDirectory)
        {
            if (string.IsNullOrWhiteSpace(packageDirectory) || !Directory.Exists(packageDirectory))
                return null;

            string packageJsonPath = Path.Combine(packageDirectory, "package.json");
            if (!File.Exists(packageJsonPath))
                return null;

            try
            {
                string packageJson = File.ReadAllText(packageJsonPath);
                return ExtractJsonString(packageJson, "version");
            }
            catch
            {
                return null;
            }
        }
    }
}
