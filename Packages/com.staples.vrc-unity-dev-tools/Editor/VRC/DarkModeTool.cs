using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Staples.DevTools.Editor.VRC
{
    internal class DarkModeTool : EditorWindow
    {
        private enum RemovalKind
        {
            SceneLight,
            VrcfurySocketLight,
            VrcfuryPlugTipLight,
        }

        private sealed class LightRow
        {
            public Object DisplayObject;
            public Light Light;
            public Component SourceComponent;
            public string Path;
            public string Details;
            public RemovalKind Kind;
            public bool Selected;
        }

        private const string MenuPath = "Tools/.Staples./Dev Tools/Dark Mode";
        private const string VrcfuryHapticSocketTypeName = "VF.Component.VRCFuryHapticSocket";
        private const string VrcfuryHapticPlugTypeName = "VF.Component.VRCFuryHapticPlug";

        private VRCAvatarDescriptor _selectedAvatar;
        private Vector2 _scroll;
        private readonly List<LightRow> _dynamicLights = new List<LightRow>(32);

        [MenuItem(MenuPath)]
        private static void Open()
        {
            var window = GetWindow<DarkModeTool>(title: "Dark Mode");
            window.minSize = new Vector2(620f, 320f);
            window.TrySyncSelectedAvatar(forceRefresh: true);
            window.Show();
        }

        private void OnSelectionChange()
        {
            if (TrySyncSelectedAvatar(forceRefresh: false))
                Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Dark Mode", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Scan the selected avatar for dynamic scene lights and VRCFury-generated socket or tip lights, choose which ones to disable, and apply only the selected entries.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(8f);

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
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.HelpBox(
                        "Select a VRCAvatarDescriptor in the scene to scan for dynamic lights.",
                        MessageType.Info);
                    return;
                }
            }

            EditorGUILayout.Space(8f);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawMetricBox("Detected Entries", _dynamicLights.Count.ToString());
                DrawMetricBox("Selected", GetSelectedLightCount().ToString());
            }

            EditorGUILayout.Space(6f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh Scan"))
                    Refresh();

                using (new EditorGUI.DisabledScope(_dynamicLights.Count == 0))
                {
                    if (GUILayout.Button("Select All"))
                        SetAllRowsSelected(true);

                    if (GUILayout.Button("Select None"))
                        SetAllRowsSelected(false);
                }
            }

            EditorGUILayout.Space(8f);
            DrawSectionHeader(
                "Light Entries",
                _dynamicLights.Count == 0
                    ? "No removable dynamic or VRCFury-generated lights were found under the selected avatar."
                    : $"{_dynamicLights.Count} removable light entr{(_dynamicLights.Count == 1 ? "y" : "ies")} found.");

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (_dynamicLights.Count == 0)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("No light entries to display.", EditorStyles.wordWrappedMiniLabel);
                }
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Apply", EditorStyles.miniBoldLabel, GUILayout.Width(52f));
                    EditorGUILayout.LabelField("Object", EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField("Source", EditorStyles.miniBoldLabel, GUILayout.Width(160f));
                    EditorGUILayout.LabelField("Hierarchy Path", EditorStyles.miniBoldLabel, GUILayout.Width(220f));
                }

                for (int i = 0; i < _dynamicLights.Count; i++)
                {
                    var row = _dynamicLights[i];
                    if (!IsRowValid(row))
                        continue;

                    using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                    {
                        row.Selected = EditorGUILayout.Toggle(row.Selected, GUILayout.Width(52f));

                        using (new EditorGUI.DisabledScope(true))
                        {
                            EditorGUILayout.ObjectField(row.DisplayObject, typeof(Object), allowSceneObjects: true);
                        }

                        EditorGUILayout.LabelField(row.Details, EditorStyles.miniLabel, GUILayout.Width(160f));
                        EditorGUILayout.SelectableLabel(
                            row.Path,
                            EditorStyles.miniLabel,
                            GUILayout.Width(220f),
                            GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    }
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(GetSelectedLightCount() == 0))
            {
                if (GUILayout.Button(GetApplyButtonLabel(), GUILayout.Height(28f)))
                    RemoveSelectedLights();
            }
        }

        private static void DrawMetricBox(string label, string value)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinHeight(56f)))
            {
                EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(value, EditorStyles.boldLabel);
            }
        }

        private static void DrawSectionHeader(string title, string description)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4f);
        }

        private bool TrySyncSelectedAvatar(bool forceRefresh)
        {
            var avatar = FindSelectedAvatarDescriptor();
            if (avatar == _selectedAvatar && !forceRefresh)
                return false;

            _selectedAvatar = avatar;
            Refresh();
            return true;
        }

        private void Refresh()
        {
            _dynamicLights.Clear();

            if (_selectedAvatar == null)
                return;

            var avatarRoot = _selectedAvatar.gameObject;
            AddSceneLightRows(avatarRoot.transform);
            AddVrcfuryGeneratedLightRows(avatarRoot.transform);
            _dynamicLights.Sort((a, b) =>
            {
                int pathCompare = string.CompareOrdinal(a.Path, b.Path);
                return pathCompare != 0 ? pathCompare : string.CompareOrdinal(a.Details, b.Details);
            });
        }

        private void AddSceneLightRows(Transform avatarRoot)
        {
            var lights = avatarRoot.GetComponentsInChildren<Light>(includeInactive: true);
            for (int i = 0; i < lights.Length; i++)
            {
                var light = lights[i];
                if (!IsDynamicLight(light))
                    continue;

                _dynamicLights.Add(new LightRow
                {
                    DisplayObject = light,
                    Light = light,
                    Path = GetHierarchyPath(light.transform, avatarRoot),
                    Details = GetSceneLightDetails(light),
                    Kind = RemovalKind.SceneLight,
                    Selected = true,
                });
            }
        }

        private void AddVrcfuryGeneratedLightRows(Transform avatarRoot)
        {
            var components = avatarRoot.GetComponentsInChildren<Component>(includeInactive: true);
            for (int i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (TryGetVrcfurySocketLightInfo(component, out string socketModeLabel))
                {
                    _dynamicLights.Add(new LightRow
                    {
                        DisplayObject = component.gameObject,
                        SourceComponent = component,
                        Path = GetHierarchyPath(component.transform, avatarRoot),
                        Details = $"VRCFury Socket ({socketModeLabel})",
                        Kind = RemovalKind.VrcfurySocketLight,
                        Selected = true,
                    });
                    continue;
                }

                if (!TryGetVrcfuryPlugTipLightInfo(component))
                    continue;

                _dynamicLights.Add(new LightRow
                {
                    DisplayObject = component.gameObject,
                    SourceComponent = component,
                    Path = GetHierarchyPath(component.transform, avatarRoot),
                    Details = "VRCFury DPS Tip Light",
                    Kind = RemovalKind.VrcfuryPlugTipLight,
                    Selected = true,
                });
            }
        }

        private void SetAllRowsSelected(bool selected)
        {
            for (int i = 0; i < _dynamicLights.Count; i++)
                _dynamicLights[i].Selected = selected;
        }

        private int GetSelectedLightCount()
        {
            int count = 0;
            for (int i = 0; i < _dynamicLights.Count; i++)
            {
                var row = _dynamicLights[i];
                if (row.Selected && IsRowValid(row))
                    count++;
            }

            return count;
        }

        private string GetApplyButtonLabel()
        {
            int selectedCount = GetSelectedLightCount();
            if (selectedCount == 1)
                return "Remove from 1 Selected Entry";

            return $"Remove from {selectedCount} Selected Entries";
        }

        private void RemoveSelectedLights()
        {
            int selectedCount = GetSelectedLightCount();
            if (selectedCount == 0 || _selectedAvatar == null)
                return;

            string avatarName = _selectedAvatar.gameObject.name;
            string entryCountLabel = selectedCount == 1 ? "1 selected entry" : $"{selectedCount} selected entries";
            string prompt =
                $"Dark Mode will process {entryCountLabel} on '{avatarName}'.\n\n" +
                "Dynamic Lights will be removed.\n\n" +
                "Do you want to continue?";

            if (!EditorUtility.DisplayDialog("Dark Mode", prompt, "Continue", "Cancel"))
                return;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Dark Mode ({avatarName})");

            int removedSceneLights = 0;
            int disabledVrcfurySocketLights = 0;
            int disabledVrcfuryTipLights = 0;

            for (int i = 0; i < _dynamicLights.Count; i++)
            {
                var row = _dynamicLights[i];
                if (!row.Selected || !IsRowValid(row))
                    continue;

                switch (row.Kind)
                {
                    case RemovalKind.SceneLight:
                        Undo.DestroyObjectImmediate(row.Light);
                        removedSceneLights++;
                        break;
                    case RemovalKind.VrcfurySocketLight:
                        if (DisableVrcfurySocketLight(row.SourceComponent))
                            disabledVrcfurySocketLights++;
                        break;
                    case RemovalKind.VrcfuryPlugTipLight:
                        if (DisableVrcfuryPlugTipLight(row.SourceComponent))
                            disabledVrcfuryTipLights++;
                        break;
                }
            }

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log(
                $"[Dev Tools] Dark Mode processed avatar '{avatarName}': removed {removedSceneLights} scene light(s), disabled {disabledVrcfurySocketLights} VRCFury socket light entr{(disabledVrcfurySocketLights == 1 ? "y" : "ies")}, and disabled {disabledVrcfuryTipLights} VRCFury DPS tip light entr{(disabledVrcfuryTipLights == 1 ? "y" : "ies")}.");
            Refresh();
        }

        private static bool DisableVrcfurySocketLight(Component component)
        {
            if (component == null)
                return false;

            var serializedObject = new SerializedObject(component);
            var addLightProperty = serializedObject.FindProperty("addLight");
            if (addLightProperty == null || addLightProperty.propertyType != SerializedPropertyType.Enum)
                return false;
            if (addLightProperty.enumValueIndex == 0)
                return false;

            Undo.RecordObject(component, "Disable VRCFury socket light");
            addLightProperty.enumValueIndex = 0;
            serializedObject.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            EditorUtility.SetDirty(component);
            return true;
        }

        private static bool DisableVrcfuryPlugTipLight(Component component)
        {
            if (component == null)
                return false;

            var serializedObject = new SerializedObject(component);
            var addDpsTipLightProperty = serializedObject.FindProperty("addDpsTipLight");
            if (addDpsTipLightProperty == null || addDpsTipLightProperty.propertyType != SerializedPropertyType.Boolean)
                return false;
            if (!addDpsTipLightProperty.boolValue)
                return false;

            Undo.RecordObject(component, "Disable VRCFury DPS tip light");
            addDpsTipLightProperty.boolValue = false;
            serializedObject.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            EditorUtility.SetDirty(component);
            return true;
        }

        private static bool TryGetVrcfurySocketLightInfo(Component component, out string modeLabel)
        {
            modeLabel = string.Empty;
            if (component == null)
                return false;
            if (component.GetType().FullName != VrcfuryHapticSocketTypeName)
                return false;

            var serializedObject = new SerializedObject(component);
            var addLightProperty = serializedObject.FindProperty("addLight");
            if (addLightProperty == null || addLightProperty.propertyType != SerializedPropertyType.Enum)
                return false;
            if (addLightProperty.enumValueIndex == 0)
                return false;

            modeLabel = GetEnumDisplayName(addLightProperty);
            return true;
        }

        private static bool TryGetVrcfuryPlugTipLightInfo(Component component)
        {
            if (component == null)
                return false;
            if (component.GetType().FullName != VrcfuryHapticPlugTypeName)
                return false;

            var serializedObject = new SerializedObject(component);
            var addDpsTipLightProperty = serializedObject.FindProperty("addDpsTipLight");
            if (addDpsTipLightProperty == null || addDpsTipLightProperty.propertyType != SerializedPropertyType.Boolean)
                return false;

            return addDpsTipLightProperty.boolValue;
        }

        private static string GetEnumDisplayName(SerializedProperty property)
        {
            int index = property.enumValueIndex;
            if (property.enumDisplayNames != null && index >= 0 && index < property.enumDisplayNames.Length)
                return property.enumDisplayNames[index];
            if (property.enumNames != null && index >= 0 && index < property.enumNames.Length)
                return property.enumNames[index];
            return "Enabled";
        }

        private static bool IsRowValid(LightRow row)
        {
            if (row == null)
                return false;

            switch (row.Kind)
            {
                case RemovalKind.SceneLight:
                    return row.Light != null;
                case RemovalKind.VrcfurySocketLight:
                case RemovalKind.VrcfuryPlugTipLight:
                    return row.SourceComponent != null;
                default:
                    return false;
            }
        }

        private static VRCAvatarDescriptor FindSelectedAvatarDescriptor()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
                return null;

            return selected.GetComponentInParent<VRCAvatarDescriptor>(includeInactive: true)
                ?? selected.GetComponent<VRCAvatarDescriptor>();
        }

        private static string GetHierarchyPath(Transform current, Transform root)
        {
            if (current == null)
                return string.Empty;

            if (current == root)
                return root.name;

            var segments = new List<string>(8);
            var node = current;
            while (node != null)
            {
                segments.Add(node.name);
                if (node == root)
                    break;

                node = node.parent;
            }

            segments.Reverse();
            return string.Join("/", segments);
        }

        private static string GetSceneLightDetails(Light light)
        {
            return $"Scene Light ({light.type}, {light.lightmapBakeType})";
        }

        private static bool IsDynamicLight(Light light)
        {
            return light != null && light.lightmapBakeType != LightmapBakeType.Baked;
        }
    }
}
