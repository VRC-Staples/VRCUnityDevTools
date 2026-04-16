using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Staples.DevTools.Editor
{
    internal class VccPackageSwitcherWindow : EditorWindow
    {
        private const float StatusColumnWidth = 260f;
        private const float PackageListHeight = 156f;

        private sealed class ProjectRow
        {
            public string ProjectName;
            public string ProjectRoot;
            public string Status;
            public bool Selected;
        }

        private List<VccLocalPackage> _packages;
        private int _selectedPkg = -1;
        private List<ProjectRow> _rows;
        private Vector2 _pkgScroll;
        private Vector2 _projScroll;
        private bool _initialized;

        internal static void Open()
        {
            var win = GetWindow<VccPackageSwitcherWindow>(false, "Local or Embedded Package Switcher");
            win.minSize = new Vector2(580, 440);
            win.Scan();
            win.Show();
        }

        private void Scan()
        {
            string currentRoot = Directory.GetParent(Application.dataPath)?.FullName;
            _packages = string.IsNullOrEmpty(currentRoot)
                ? new List<VccLocalPackage>()
                : VccPackageDiscovery.DiscoverSwitchablePackages(currentRoot);
            _selectedPkg = _packages.Count == 1 ? 0 : -1;
            BuildRows();
            _initialized = true;
            Repaint();
        }

        private void BuildRows()
        {
            if (_selectedPkg < 0 || _packages == null || _selectedPkg >= _packages.Count)
            {
                _rows = new List<ProjectRow>();
                return;
            }

            var pkg = _packages[_selectedPkg];
            string currentRoot = Directory.GetParent(Application.dataPath)?.FullName;
            var roots = VccPackageDiscovery.FindVccProjectRoots().ToList();
            if (!string.IsNullOrEmpty(currentRoot)
                && !roots.Any(r => string.Equals(r, currentRoot, StringComparison.OrdinalIgnoreCase)))
            {
                roots.Insert(0, currentRoot);
            }

            _rows = roots.Select(root => new ProjectRow
            {
                ProjectName = Path.GetFileName(root)
                    + (string.Equals(root, currentRoot, StringComparison.OrdinalIgnoreCase) ? " (current)" : string.Empty),
                ProjectRoot = root,
                Status = VccPackageDiscovery.GetProjectPackageStatus(root, pkg.PackageName),
                Selected = false,
            }).ToList();
        }

        private void OnGUI()
        {
            if (!_initialized)
            {
                EditorGUILayout.LabelField("Scanning local package bindings...");
                return;
            }

            bool pkgPicked = _selectedPkg >= 0 && _packages != null && _selectedPkg < _packages.Count;
            int selectedProjectCount = _rows?.Count(r => r.Selected) ?? 0;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Local or Embedded Package Switcher", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Pick a package from your VCC local package library, then choose which projects should use the local repo or fall back to the embedded package.",
                EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(8);
            DrawSectionHeader("Packages in this project", "Only packages that are installed in this project and also exist in your VCC local package folders are shown here.");

            _pkgScroll = EditorGUILayout.BeginScrollView(_pkgScroll, GUILayout.MaxHeight(PackageListHeight));
            if (_packages == null || _packages.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No matching local packages were found. Add the package folder in VCC Settings → Packages, then make sure this project already has that package installed via manifest or embedded package.",
                    MessageType.Info);
            }
            else
            {
                for (int i = 0; i < _packages.Count; i++)
                {
                    var pkg = _packages[i];
                    bool isSelected = i == _selectedPkg;
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.BeginHorizontal();
                        bool newSel = GUILayout.Toggle(isSelected, GUIContent.none, GUILayout.Width(18));
                        EditorGUILayout.BeginVertical();
                        EditorGUILayout.LabelField($"{pkg.DisplayName}  {FormatPackageVersion(pkg.Version)}", EditorStyles.boldLabel);
                        EditorGUILayout.LabelField(pkg.PackageName, EditorStyles.miniLabel);
                        EditorGUILayout.LabelField($"Local source: {FormatCompactPath(pkg.LocalPath)}", EditorStyles.wordWrappedMiniLabel);
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.EndHorizontal();

                        if (newSel && !isSelected)
                        {
                            _selectedPkg = i;
                            BuildRows();
                        }
                    }
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);
            using (new EditorGUI.DisabledScope(!pkgPicked))
            {
                string selectedPackageSummary = pkgPicked
                    ? $"Selected package: {_packages[_selectedPkg].DisplayName} ({_packages[_selectedPkg].PackageName})"
                    : "Selected package: none";
                DrawSectionHeader("Projects", selectedPackageSummary);

                if (!pkgPicked)
                {
                    EditorGUILayout.HelpBox("Select a package above to review project bindings.", MessageType.None);
                }
                else
                {
                    EditorGUILayout.LabelField("Choose the projects you want to update.", EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.Space(4);

                    _projScroll = EditorGUILayout.BeginScrollView(_projScroll, GUILayout.ExpandHeight(true));
                    if (_rows != null)
                    {
                        foreach (var row in _rows)
                        {
                            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                            {
                                row.Selected = GUILayout.Toggle(row.Selected, GUIContent.none, GUILayout.Width(18));
                                EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                                EditorGUILayout.LabelField(row.ProjectName, EditorStyles.boldLabel);
                                EditorGUILayout.LabelField(FormatCompactPath(row.ProjectRoot), EditorStyles.wordWrappedMiniLabel);
                                EditorGUILayout.EndVertical();
                                EditorGUILayout.SelectableLabel(row.Status, EditorStyles.miniLabel, GUILayout.Width(StatusColumnWidth), GUILayout.Height(EditorGUIUtility.singleLineHeight));
                            }
                        }
                    }
                    EditorGUILayout.EndScrollView();

                    EditorGUILayout.Space(2);
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Select All") && _rows != null)
                    {
                        foreach (var r in _rows)
                            r.Selected = true;
                    }

                    if (GUILayout.Button("Clear Selection") && _rows != null)
                    {
                        foreach (var r in _rows)
                            r.Selected = false;
                    }

                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField($"{selectedProjectCount} selected", EditorStyles.miniLabel, GUILayout.Width(80));
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh"))
                Scan();

            bool hasSelection = pkgPicked && _rows != null && _rows.Any(r => r.Selected);
            using (new EditorGUI.DisabledScope(!hasSelection))
            {
                if (GUILayout.Button("Use Local Repo"))
                    Apply(toLocal: true);
                if (GUILayout.Button("Use Embedded"))
                    Apply(toLocal: false);
            }

            if (GUILayout.Button("Close"))
                Close();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(6);
        }

        private static void DrawSectionHeader(string title, string description)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (!string.IsNullOrWhiteSpace(description))
                EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4);
        }

        private static string FormatCompactPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "unknown";

            string normalized = path.Replace('/', Path.DirectorySeparatorChar);
            string currentRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (!string.IsNullOrWhiteSpace(currentRoot)
                && normalized.StartsWith(currentRoot, StringComparison.OrdinalIgnoreCase))
            {
                string relative = normalized.Substring(currentRoot.Length).TrimStart(Path.DirectorySeparatorChar);
                return string.IsNullOrEmpty(relative) ? "." : $".\\{relative}";
            }

            return normalized;
        }

        private static string FormatPackageVersion(string version)
        {
            return string.IsNullOrWhiteSpace(version) ? "(version unknown)" : $"v{version}";
        }

        private void Apply(bool toLocal)
        {
            if (_packages == null || _rows == null || _selectedPkg < 0)
                return;

            var pkg = _packages[_selectedPkg];
            foreach (var row in _rows.Where(r => r.Selected).ToList())
            {
                if (toLocal)
                    VccPackageBindingService.ApplySwitchToFileLocal(row.ProjectRoot, pkg.LocalPath, pkg.PackageName);
                else
                    VccPackageBindingService.ApplySwitchToEmbedded(row.ProjectRoot, pkg.PackageName);
            }

            BuildRows();
            Repaint();
        }
    }
}
