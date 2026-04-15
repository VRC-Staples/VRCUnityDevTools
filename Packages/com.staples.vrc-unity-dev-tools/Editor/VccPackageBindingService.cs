using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Staples.DevTools.Editor
{
    internal static class VccPackageBindingService
    {
        internal static void ApplySwitchToFileLocal(string projectRoot, string selectedLocalPath, string packageName)
        {
            bool isCurrentProject = string.Equals(
                projectRoot,
                Directory.GetParent(Application.dataPath)?.FullName,
                StringComparison.OrdinalIgnoreCase);

            string embeddedPath = Path.GetFullPath(Path.Combine(projectRoot, "Packages", packageName));
            string dependencyValue = ToUnityFileDependencyValue(selectedLocalPath);

            if (!TrySetManifestDependency(projectRoot, packageName, dependencyValue, out var manifestError))
            {
                Debug.LogError($"[Dev Tools] Switch failed for '{Path.GetFileName(projectRoot)}': {manifestError}");
                return;
            }

            bool vpmChanged = TryRemoveVpmManifestPackageEntry(projectRoot, packageName);

            string embeddedRemovalNote = string.Empty;
            bool embeddedExists = Directory.Exists(embeddedPath)
                && !string.Equals(embeddedPath, Path.GetFullPath(selectedLocalPath), StringComparison.OrdinalIgnoreCase);

            if (embeddedExists)
            {
                if (isCurrentProject)
                    AssetDatabase.Refresh();

                if (!TryRemoveEmbeddedPackageFolder(projectRoot, embeddedPath, out var removalNote, out var removalError))
                    Debug.LogWarning($"[Dev Tools] Could not remove embedded folder '{embeddedPath}': {removalError}. Unity may still use the embedded copy.");
                else
                    embeddedRemovalNote = removalNote;
            }

            if (isCurrentProject)
            {
                Client.Resolve();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[Dev Tools] [{Path.GetFileName(projectRoot)}] Switched to file dependency: {packageName} => '{dependencyValue}' ({(vpmChanged ? "removed stale vpm-manifest entry" : "no vpm-manifest change")}){(string.IsNullOrEmpty(embeddedRemovalNote) ? string.Empty : ". " + embeddedRemovalNote)}.");
        }

        internal static void ApplySwitchToEmbedded(string projectRoot, string packageName)
        {
            bool isCurrentProject = string.Equals(
                projectRoot,
                Directory.GetParent(Application.dataPath)?.FullName,
                StringComparison.OrdinalIgnoreCase);

            string embeddedPath = Path.GetFullPath(Path.Combine(projectRoot, "Packages", packageName));
            string backupRoot = Path.Combine(projectRoot, ".dev-tools-package-backups");
            string restoredFrom = null;

            if (!Directory.Exists(embeddedPath) && Directory.Exists(backupRoot))
            {
                string latestBackup = Directory.GetDirectories(backupRoot, packageName + "__embedded_backup_*")
                    .OrderByDescending(x => x, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();

                if (latestBackup != null)
                {
                    try
                    {
                        Directory.Move(latestBackup, embeddedPath);
                        restoredFrom = latestBackup;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[Dev Tools] Could not restore embedded backup '{latestBackup}': {ex.Message}.");
                    }
                }
            }

            bool embeddedAvailable = Directory.Exists(embeddedPath) && File.Exists(Path.Combine(embeddedPath, "package.json"));
            if (!embeddedAvailable)
            {
                Debug.LogWarning($"[Dev Tools] No embedded package is available for '{packageName}' in '{Path.GetFileName(projectRoot)}'. Restore or add Packages/{packageName} before switching to embedded.");
                return;
            }

            if (!TryRemoveManifestDependency(projectRoot, packageName, out var manifestError))
            {
                Debug.LogError($"[Dev Tools] Switch failed for '{Path.GetFileName(projectRoot)}': {manifestError}");
                return;
            }

            bool vpmChanged = TryRemoveVpmManifestPackageEntry(projectRoot, packageName);

            if (isCurrentProject)
            {
                Client.Resolve();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[Dev Tools] [{Path.GetFileName(projectRoot)}] Switched to embedded package: {packageName} => '{embeddedPath}' ({(vpmChanged ? "removed stale vpm-manifest entry" : "no vpm-manifest change")}){(restoredFrom == null ? string.Empty : $". Restored backup from '{restoredFrom}'")}." );
        }

        private static bool TryRemoveEmbeddedPackageFolder(string projectRoot, string embeddedPath, out string note, out string error)
        {
            note = null;
            error = null;
            try
            {
                bool isLink = (File.GetAttributes(embeddedPath) & FileAttributes.ReparsePoint) != 0;
                if (isLink)
                {
                    Directory.Delete(embeddedPath, recursive: false);
                    note = "removed junction/symlink at embedded path";
                    return true;
                }

                string backupRoot = Path.Combine(projectRoot, ".dev-tools-package-backups");
                Directory.CreateDirectory(backupRoot);
                string backupName = Path.GetFileName(embeddedPath) + "__embedded_backup_" + DateTime.Now.ToString("yyyyMMddHHmmss");
                string backupPath = Path.Combine(backupRoot, backupName);
                Directory.Move(embeddedPath, backupPath);
                note = $"moved embedded folder to '{backupPath}'";
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TrySetManifestDependency(string projectRoot, string packageName, string dependencyValue, out string error)
        {
            error = null;
            try
            {
                string manifestPath = Path.Combine(projectRoot, "Packages", "manifest.json");
                if (!File.Exists(manifestPath))
                {
                    error = $"manifest.json not found at '{manifestPath}'.";
                    return false;
                }

                string json = File.ReadAllText(manifestPath);
                RemoveTopLevelJsonObjectEntry(ref json, "dependencies", packageName);

                if (!AddTopLevelJsonObjectEntry(ref json, "dependencies", packageName, dependencyValue, out var addError))
                {
                    error = addError;
                    return false;
                }

                string backupPath = manifestPath + ".bak";
                File.Copy(manifestPath, backupPath, overwrite: true);
                File.WriteAllText(manifestPath, json);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryRemoveManifestDependency(string projectRoot, string packageName, out string error)
        {
            error = null;
            try
            {
                string manifestPath = Path.Combine(projectRoot, "Packages", "manifest.json");
                if (!File.Exists(manifestPath))
                {
                    error = $"manifest.json not found at '{manifestPath}'.";
                    return false;
                }

                string json = File.ReadAllText(manifestPath);
                bool changed = RemoveTopLevelJsonObjectEntry(ref json, "dependencies", packageName);
                if (!changed)
                    return true;

                string backupPath = manifestPath + ".bak";
                File.Copy(manifestPath, backupPath, overwrite: true);
                File.WriteAllText(manifestPath, json);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static string ToUnityFileDependencyValue(string absolutePath)
        {
            string normalized = Path.GetFullPath(absolutePath)
                .Replace('\\', '/');

            return $"file:{normalized}";
        }

        private static bool AddTopLevelJsonObjectEntry(ref string json, string sectionName, string key, string value, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(json))
            {
                error = "json content was empty.";
                return false;
            }

            string sectionNeedle = $"\"{sectionName}\"";
            int sectionIndex = json.IndexOf(sectionNeedle, StringComparison.Ordinal);
            if (sectionIndex < 0)
            {
                error = $"section '{sectionName}' was not found.";
                return false;
            }

            int sectionBraceStart = json.IndexOf('{', sectionIndex);
            if (sectionBraceStart < 0)
            {
                error = $"section '{sectionName}' has no opening brace.";
                return false;
            }

            int sectionBraceEnd = FindMatchingBrace(json, sectionBraceStart);
            if (sectionBraceEnd < 0)
            {
                error = $"section '{sectionName}' has no matching closing brace.";
                return false;
            }

            string sectionBody = json.Substring(sectionBraceStart + 1, sectionBraceEnd - sectionBraceStart - 1);
            bool sectionEmpty = string.IsNullOrWhiteSpace(sectionBody);

            string escapedValue = value?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? string.Empty;
            string newEntry = $"\n    \"{key}\": \"{escapedValue}\"";

            if (sectionEmpty)
            {
                json = json.Insert(sectionBraceStart + 1, newEntry + "\n  ");
            }
            else
            {
                json = json.Insert(sectionBraceEnd, "," + newEntry);
            }

            return true;
        }

        private static bool TryRemoveVpmManifestPackageEntry(string projectRoot, string packageName)
        {
            try
            {
                string vpmManifestPath = Path.Combine(projectRoot, "Packages", "vpm-manifest.json");
                if (!File.Exists(vpmManifestPath))
                    return false;

                string json = File.ReadAllText(vpmManifestPath);
                bool changed = false;

                changed |= RemoveTopLevelJsonObjectEntry(ref json, "dependencies", packageName);
                changed |= RemoveTopLevelJsonObjectEntry(ref json, "locked", packageName);

                if (!changed)
                    return false;

                string backupPath = vpmManifestPath + ".bak";
                File.Copy(vpmManifestPath, backupPath, overwrite: true);
                File.WriteAllText(vpmManifestPath, json);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Dev Tools] vpm-manifest cleanup skipped: {ex.Message}");
                return false;
            }
        }

        private static bool RemoveTopLevelJsonObjectEntry(ref string json, string sectionName, string key)
        {
            if (string.IsNullOrEmpty(json))
                return false;

            string sectionNeedle = $"\"{sectionName}\"";
            int sectionIndex = json.IndexOf(sectionNeedle, StringComparison.Ordinal);
            if (sectionIndex < 0)
                return false;

            int sectionBraceStart = json.IndexOf('{', sectionIndex);
            if (sectionBraceStart < 0)
                return false;

            int sectionBraceEnd = FindMatchingBrace(json, sectionBraceStart);
            if (sectionBraceEnd < 0)
                return false;

            string keyNeedle = $"\"{key}\"";
            int keyIndex = json.IndexOf(keyNeedle, sectionBraceStart, sectionBraceEnd - sectionBraceStart + 1, StringComparison.Ordinal);
            if (keyIndex < 0)
                return false;

            int entryStart = keyIndex;
            while (entryStart > sectionBraceStart && char.IsWhiteSpace(json[entryStart - 1]))
                entryStart--;
            if (entryStart > sectionBraceStart && json[entryStart - 1] == ',')
                entryStart--;

            int colonIndex = json.IndexOf(':', keyIndex);
            if (colonIndex < 0 || colonIndex > sectionBraceEnd)
                return false;

            int valueStart = colonIndex + 1;
            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
                valueStart++;

            int entryEnd = valueStart;
            if (entryEnd >= json.Length)
                return false;

            char startChar = json[entryEnd];
            if (startChar == '{')
            {
                int valueEnd = FindMatchingBrace(json, entryEnd);
                if (valueEnd < 0)
                    return false;
                entryEnd = valueEnd + 1;
            }
            else if (startChar == '[')
            {
                int valueEnd = FindMatchingBracket(json, entryEnd);
                if (valueEnd < 0)
                    return false;
                entryEnd = valueEnd + 1;
            }
            else
            {
                while (entryEnd < json.Length && json[entryEnd] != ',' && json[entryEnd] != '}')
                    entryEnd++;
            }

            while (entryEnd < json.Length && char.IsWhiteSpace(json[entryEnd]))
                entryEnd++;
            if (entryEnd < json.Length && json[entryEnd] == ',')
                entryEnd++;

            json = json.Remove(entryStart, entryEnd - entryStart);
            return true;
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

                if (c == '{')
                    depth++;
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

                if (c == '[')
                    depth++;
                else if (c == ']')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }
    }
}
