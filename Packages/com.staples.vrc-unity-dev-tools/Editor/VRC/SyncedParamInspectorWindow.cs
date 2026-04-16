using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Staples.DevTools.Editor.VRC
{
    /// <summary>
    /// Inspector window that shows the true synced expression parameter usage
    /// for any VRCAvatarDescriptor in the scene. This is intended as a source
    /// of truth when debugging tools that display aggregate synced counts.
    /// </summary>
    public class SyncedParamInspectorWindow : EditorWindow
    {
        private sealed class SyncedParamRow
        {
            public string Name;
            public string TypeLabel;
        }

        private VRCAvatarDescriptor _selectedAvatar;
        private Vector2 _scroll;

        private int _totalParams;
        private int _syncedParams;
        private float _totalCost;
        private float _syncedCost;
        private string _assetPath;
        private readonly List<SyncedParamRow> _syncedParamsRows = new List<SyncedParamRow>(32);

        [MenuItem("Tools/.Staples./Dev Tools/Synced Param Inspector")]
        public static void Open()
        {
            var win = GetWindow<SyncedParamInspectorWindow>(title: "Synced Expression Parameters");
            win.minSize = new Vector2(420, 320);
            win.Show();
        }

        private void OnSelectionChange()
        {
            if (Selection.activeGameObject == null)
                return;

            var descriptor = Selection.activeGameObject
                .GetComponentInParent<VRCAvatarDescriptor>(includeInactive: true)
                ?? Selection.activeGameObject.GetComponent<VRCAvatarDescriptor>();

            if (descriptor != null && descriptor != _selectedAvatar)
            {
                _selectedAvatar = descriptor;
                Refresh();
                Repaint();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Synced Expression Parameters", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Inspect the selected avatar's expression parameters asset and confirm which entries are actually network synced.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(8);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var newAvatar = (VRCAvatarDescriptor)EditorGUILayout.ObjectField(
                    "Avatar Root",
                    _selectedAvatar,
                    typeof(VRCAvatarDescriptor),
                    allowSceneObjects: true);

                if (newAvatar != _selectedAvatar)
                {
                    _selectedAvatar = newAvatar;
                    Refresh();
                }

                EditorGUILayout.LabelField(
                    "Tip: selecting any object under an avatar will auto-pick its descriptor.",
                    EditorStyles.wordWrappedMiniLabel);

                if (_selectedAvatar == null)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.HelpBox(
                        "Select a VRC Avatar Descriptor in the scene to inspect its synced parameter usage.",
                        MessageType.Info);
                    return;
                }
            }

            EditorGUILayout.Space(8);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Expression Parameters Asset", EditorStyles.boldLabel);

                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField(
                        GUIContent.none,
                        _selectedAvatar.expressionParameters,
                        typeof(VRCExpressionParameters),
                        allowSceneObjects: false);
                }

                EditorGUILayout.LabelField("Asset Path", EditorStyles.miniBoldLabel);
                EditorGUILayout.SelectableLabel(
                    string.IsNullOrEmpty(_assetPath) ? "<none>" : _assetPath,
                    EditorStyles.wordWrappedMiniLabel,
                    GUILayout.MinHeight(EditorGUIUtility.singleLineHeight * 2f));
            }

            EditorGUILayout.Space(8);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawMetricBox("Total Entries", _totalParams.ToString());
                DrawMetricBox("Synced Entries", _syncedParams.ToString());
                DrawMetricBox("Sync Usage", GetSyncUsageSummaryLabel());
            }

            EditorGUILayout.Space(6);
            if (IsHighSyncUsage())
            {
                EditorGUILayout.HelpBox(
                    "This avatar is using a large share of the VRChat synced parameter memory budget. Review synced parameters carefully before adding more.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "These numbers come directly from the VRCExpressionParameters asset. If another tool reports a different synced count, trust this window as the project-level source of truth.",
                    MessageType.None);
            }

            EditorGUILayout.Space(8);
            DrawSectionHeader(
                "Synced Parameter Names",
                _syncedParamsRows.Count == 0
                    ? "No synced parameters were found in the selected asset."
                    : $"{_syncedParamsRows.Count} synced parameter{(_syncedParamsRows.Count == 1 ? string.Empty : "s")} found.");

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (_syncedParamsRows.Count == 0)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("No synced parameter names to display.", EditorStyles.wordWrappedMiniLabel);
                }
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("#", EditorStyles.miniBoldLabel, GUILayout.Width(28f));
                    EditorGUILayout.LabelField("Parameter Name", EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField("Type", EditorStyles.miniBoldLabel, GUILayout.Width(72f));
                }

                for (int i = 0; i < _syncedParamsRows.Count; i++)
                {
                    var row = _syncedParamsRows[i];
                    using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField((i + 1).ToString(), EditorStyles.miniLabel, GUILayout.Width(28f));
                        EditorGUILayout.SelectableLabel(
                            row.Name,
                            EditorStyles.label,
                            GUILayout.Height(EditorGUIUtility.singleLineHeight));
                        EditorGUILayout.LabelField(row.TypeLabel, EditorStyles.miniLabel, GUILayout.Width(72f));
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private static void DrawMetricBox(string label, string value)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinHeight(56f)))
            {
                EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(value, EditorStyles.boldLabel);
            }
        }

        private string GetSyncUsageSummaryLabel()
        {
            float percent = GetSyncUsagePercent();
            return $"{percent:0.#}% ({_syncedCost:0.#}/256)";
        }

        private float GetSyncUsagePercent()
        {
            return Mathf.Clamp((_syncedCost / 256f) * 100f, 0f, 100f);
        }

        private bool IsHighSyncUsage()
        {
            return _syncedCost >= 192f;
        }

        private static string GetParameterTypeLabel(VRCExpressionParameters.Parameter parameter)
        {
            switch (parameter.valueType)
            {
                case VRCExpressionParameters.ValueType.Bool:
                    return "Bool";
                case VRCExpressionParameters.ValueType.Int:
                    return "Int";
                case VRCExpressionParameters.ValueType.Float:
                    return "Float";
                default:
                    return parameter.valueType.ToString();
            }
        }

        private static float GetParameterCost(VRCExpressionParameters.Parameter parameter)
        {
            switch (parameter.valueType)
            {
                case VRCExpressionParameters.ValueType.Bool:
                    return 1f;
                case VRCExpressionParameters.ValueType.Int:
                case VRCExpressionParameters.ValueType.Float:
                    return 8f;
                default:
                    return 0f;
            }
        }

        private static void DrawSectionHeader(string title, string description)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4);
        }

        private void Refresh()
        {
            _totalParams = 0;
            _syncedParams = 0;
            _totalCost = 0f;
            _syncedCost = 0f;
            _assetPath = string.Empty;
            _syncedParamsRows.Clear();

            if (_selectedAvatar == null)
                return;

            var expr = _selectedAvatar.expressionParameters;
            if (expr == null)
                return;

            _assetPath = AssetDatabase.GetAssetPath(expr);

            var parameters = expr.parameters;
            if (parameters == null)
                return;

            foreach (var p in parameters)
            {
                if (p == null || string.IsNullOrEmpty(p.name))
                    continue;

                float parameterCost = GetParameterCost(p);
                _totalParams++;
                _totalCost += parameterCost;
                if (p.networkSynced)
                {
                    _syncedParams++;
                    _syncedCost += parameterCost;
                    _syncedParamsRows.Add(new SyncedParamRow
                    {
                        Name = p.name,
                        TypeLabel = GetParameterTypeLabel(p),
                    });
                }
            }

            _syncedParamsRows.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        }
    }
}
