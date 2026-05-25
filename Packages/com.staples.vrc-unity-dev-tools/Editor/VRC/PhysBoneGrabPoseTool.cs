using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace Staples.DevTools.Editor.VRC
{
    internal class PhysBoneGrabPoseTool : EditorWindow
    {
        private enum TargetState
        {
            LeaveUnchanged,
            Enabled,
            Disabled,
        }

        private sealed class PhysBoneRow
        {
            public VRCPhysBone PhysBone;
            public string Path;
            public bool Selected;
        }

        private const string MenuPath = "Tools/.Staples./Dev Tools/PhysBone Grab & Pose";
        private const string GrabPropertyName = "allowGrabbing";
        private const string PosePropertyName = "allowPosing";
        private static readonly string[] GrabPropertyCandidates = { GrabPropertyName };
        private static readonly string[] PosePropertyCandidates = { PosePropertyName, "allowPose" };

        private VRCAvatarDescriptor _selectedAvatar;
        private Vector2 _scroll;
        private TargetState _targetGrabState = TargetState.LeaveUnchanged;
        private TargetState _targetPoseState = TargetState.LeaveUnchanged;
        private readonly List<PhysBoneRow> _physBones = new List<PhysBoneRow>(64);

        [MenuItem(MenuPath)]
        private static void Open()
        {
            var window = GetWindow<PhysBoneGrabPoseTool>(title: "PhysBone Grab & Pose");
            window.minSize = new Vector2(680f, 360f);
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
            EditorGUILayout.LabelField("PhysBone Grab & Pose", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Scan the selected avatar for VRC PhysBones, choose the rows to update, and apply a consistent Grab and Pose state to only the selected entries.",
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
                        "Select a VRCAvatarDescriptor in the scene to scan for PhysBones.",
                        MessageType.Info);
                    return;
                }
            }

            EditorGUILayout.Space(8f);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawMetricBox("Detected PhysBones", _physBones.Count.ToString());
                DrawMetricBox("Selected", GetSelectedPhysBoneCount().ToString());
                DrawMetricBox("Would Change", GetChangeCount().ToString());
            }

            EditorGUILayout.Space(6f);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Target States", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "Leave a setting unchanged when you only want to change the other setting.",
                    EditorStyles.wordWrappedMiniLabel);

                _targetGrabState = (TargetState)EditorGUILayout.EnumPopup("Grab", _targetGrabState);
                _targetPoseState = (TargetState)EditorGUILayout.EnumPopup("Pose", _targetPoseState);
            }

            EditorGUILayout.Space(6f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh Scan"))
                    Refresh();

                using (new EditorGUI.DisabledScope(_physBones.Count == 0))
                {
                    if (GUILayout.Button("Select All"))
                        SetAllRowsSelected(true);

                    if (GUILayout.Button("Select None"))
                        SetAllRowsSelected(false);
                }
            }

            EditorGUILayout.Space(8f);
            DrawSectionHeader(
                "PhysBone Entries",
                _physBones.Count == 0
                    ? "No VRCPhysBone components were found under the selected avatar."
                    : $"{_physBones.Count} PhysBone entr{(_physBones.Count == 1 ? "y" : "ies")} found.");

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (_physBones.Count == 0)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("No PhysBones to display.", EditorStyles.wordWrappedMiniLabel);
                }
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Apply", EditorStyles.miniBoldLabel, GUILayout.Width(52f));
                    EditorGUILayout.LabelField("PhysBone", EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField("Grab", EditorStyles.miniBoldLabel, GUILayout.Width(72f));
                    EditorGUILayout.LabelField("Pose", EditorStyles.miniBoldLabel, GUILayout.Width(72f));
                    EditorGUILayout.LabelField("Hierarchy Path", EditorStyles.miniBoldLabel, GUILayout.Width(240f));
                }

                for (int i = 0; i < _physBones.Count; i++)
                {
                    var row = _physBones[i];
                    if (!IsRowValid(row))
                        continue;

                    using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                    {
                        row.Selected = EditorGUILayout.Toggle(row.Selected, GUILayout.Width(52f));

                        using (new EditorGUI.DisabledScope(true))
                        {
                            EditorGUILayout.ObjectField(row.PhysBone, typeof(VRCPhysBone), allowSceneObjects: true);
                        }

                        EditorGUILayout.LabelField(GetStateLabel(row.PhysBone, GrabPropertyCandidates), EditorStyles.miniLabel, GUILayout.Width(72f));
                        EditorGUILayout.LabelField(GetStateLabel(row.PhysBone, PosePropertyCandidates), EditorStyles.miniLabel, GUILayout.Width(72f));
                        EditorGUILayout.SelectableLabel(
                            row.Path,
                            EditorStyles.miniLabel,
                            GUILayout.Width(240f),
                            GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    }
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(!CanApply()))
            {
                if (GUILayout.Button(GetApplyButtonLabel(), GUILayout.Height(28f)))
                    ApplySelectedStates();
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
            _physBones.Clear();

            if (_selectedAvatar == null)
                return;

            var avatarRoot = _selectedAvatar.transform;
            var physBones = avatarRoot.GetComponentsInChildren<VRCPhysBone>(includeInactive: true);
            for (int i = 0; i < physBones.Length; i++)
            {
                var physBone = physBones[i];
                if (physBone == null)
                    continue;

                _physBones.Add(new PhysBoneRow
                {
                    PhysBone = physBone,
                    Path = GetHierarchyPath(physBone.transform, avatarRoot),
                    Selected = true,
                });
            }

            _physBones.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
        }

        private void SetAllRowsSelected(bool selected)
        {
            for (int i = 0; i < _physBones.Count; i++)
                _physBones[i].Selected = selected;
        }

        private int GetSelectedPhysBoneCount()
        {
            int count = 0;
            for (int i = 0; i < _physBones.Count; i++)
            {
                var row = _physBones[i];
                if (row.Selected && IsRowValid(row))
                    count++;
            }

            return count;
        }

        private int GetChangeCount()
        {
            int count = 0;
            for (int i = 0; i < _physBones.Count; i++)
            {
                var row = _physBones[i];
                if (row.Selected && IsRowValid(row) && WouldChange(row.PhysBone))
                    count++;
            }

            return count;
        }

        private bool CanApply()
        {
            return _selectedAvatar != null
                && (_targetGrabState != TargetState.LeaveUnchanged || _targetPoseState != TargetState.LeaveUnchanged)
                && GetChangeCount() > 0;
        }

        private string GetApplyButtonLabel()
        {
            int selectedCount = GetSelectedPhysBoneCount();
            if (selectedCount == 1)
                return "Apply to 1 Selected PhysBone";

            return $"Apply to {selectedCount} Selected PhysBones";
        }

        private void ApplySelectedStates()
        {
            int selectedCount = GetSelectedPhysBoneCount();
            if (!CanApply())
                return;

            string avatarName = _selectedAvatar.gameObject.name;
            string entryCountLabel = selectedCount == 1 ? "1 selected PhysBone" : $"{selectedCount} selected PhysBones";
            string prompt =
                $"PhysBone Grab & Pose will update {entryCountLabel} on '{avatarName}'.\n\n" +
                $"Grab: {GetTargetStateLabel(_targetGrabState)}\n" +
                $"Pose: {GetTargetStateLabel(_targetPoseState)}\n\n" +
                "Do you want to continue?";

            if (!EditorUtility.DisplayDialog("PhysBone Grab & Pose", prompt, "Continue", "Cancel"))
                return;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"PhysBone Grab & Pose ({avatarName})");

            int changedCount = 0;
            for (int i = 0; i < _physBones.Count; i++)
            {
                var row = _physBones[i];
                if (!row.Selected || !IsRowValid(row))
                    continue;

                if (ApplyState(row.PhysBone))
                    changedCount++;
            }

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log($"[Dev Tools] PhysBone Grab & Pose processed avatar '{avatarName}': updated {changedCount} of {selectedCount} selected PhysBone(s).");
            Refresh();
        }

        private bool ApplyState(VRCPhysBone physBone)
        {
            if (physBone == null)
                return false;

            var serializedObject = new SerializedObject(physBone);
            bool changed = false;

            bool shouldChangeGrab = TryGetTargetBool(_targetGrabState, out bool targetGrab)
                && WouldChangeStateProperty(serializedObject, GrabPropertyCandidates, targetGrab);
            bool shouldChangePose = TryGetTargetBool(_targetPoseState, out bool targetPose)
                && WouldChangeStateProperty(serializedObject, PosePropertyCandidates, targetPose);

            if (!shouldChangeGrab && !shouldChangePose)
                return false;

            Undo.RecordObject(physBone, "Set PhysBone Grab & Pose state");

            if (shouldChangeGrab)
                changed |= TrySetStateProperty(serializedObject, GrabPropertyCandidates, targetGrab);
            if (shouldChangePose)
                changed |= TrySetStateProperty(serializedObject, PosePropertyCandidates, targetPose);

            if (!changed)
                return false;

            serializedObject.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(physBone);
            EditorUtility.SetDirty(physBone);
            return true;
        }

        private bool WouldChange(VRCPhysBone physBone)
        {
            if (physBone == null)
                return false;

            var serializedObject = new SerializedObject(physBone);
            return TryGetTargetBool(_targetGrabState, out bool targetGrab)
                    && WouldChangeStateProperty(serializedObject, GrabPropertyCandidates, targetGrab)
                || TryGetTargetBool(_targetPoseState, out bool targetPose)
                    && WouldChangeStateProperty(serializedObject, PosePropertyCandidates, targetPose);
        }

        private static bool TryGetTargetBool(TargetState targetState, out bool value)
        {
            switch (targetState)
            {
                case TargetState.Enabled:
                    value = true;
                    return true;
                case TargetState.Disabled:
                    value = false;
                    return true;
                default:
                    value = false;
                    return false;
            }
        }

        private static bool WouldChangeStateProperty(SerializedObject serializedObject, string[] candidates, bool targetValue)
        {
            var property = FindStateProperty(serializedObject, candidates);
            if (property == null)
                return false;

            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    return property.boolValue != targetValue;
                case SerializedPropertyType.Enum:
                    return TryFindEnumIndex(property, targetValue ? "True" : "False", out int targetIndex)
                        && property.enumValueIndex != targetIndex;
                default:
                    return false;
            }
        }

        private static bool TrySetStateProperty(SerializedObject serializedObject, string[] candidates, bool targetValue)
        {
            var property = FindStateProperty(serializedObject, candidates);
            if (property == null)
                return false;

            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    if (property.boolValue == targetValue)
                        return false;

                    property.boolValue = targetValue;
                    return true;
                case SerializedPropertyType.Enum:
                    if (!TryFindEnumIndex(property, targetValue ? "True" : "False", out int targetIndex)
                        || property.enumValueIndex == targetIndex)
                    {
                        return false;
                    }

                    property.enumValueIndex = targetIndex;
                    return true;
                default:
                    return false;
            }
        }

        private static SerializedProperty FindStateProperty(SerializedObject serializedObject, string[] candidates)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                var property = serializedObject.FindProperty(candidates[i]);
                if (property == null)
                    continue;

                if (property.propertyType == SerializedPropertyType.Boolean
                    || property.propertyType == SerializedPropertyType.Enum)
                {
                    return property;
                }
            }

            return null;
        }

        private static string GetStateLabel(VRCPhysBone physBone, string[] candidates)
        {
            if (physBone == null)
                return "Missing";

            var serializedObject = new SerializedObject(physBone);
            var property = FindStateProperty(serializedObject, candidates);
            if (property == null)
                return "Unknown";

            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    return property.boolValue ? "Enabled" : "Disabled";
                case SerializedPropertyType.Enum:
                    return GetEnumStateLabel(property);
                default:
                    return "Unknown";
            }
        }

        private static string GetEnumStateLabel(SerializedProperty property)
        {
            string enumName = GetEnumName(property, property.enumValueIndex);
            switch (enumName)
            {
                case "True":
                    return "Enabled";
                case "False":
                    return "Disabled";
                case "Other":
                    return "Filtered";
                default:
                    return string.IsNullOrEmpty(enumName) ? "Unknown" : enumName;
            }
        }

        private static bool TryFindEnumIndex(SerializedProperty property, string targetName, out int index)
        {
            if (property.enumNames != null)
            {
                for (int i = 0; i < property.enumNames.Length; i++)
                {
                    if (property.enumNames[i] == targetName)
                    {
                        index = i;
                        return true;
                    }
                }
            }

            if (property.enumDisplayNames != null)
            {
                for (int i = 0; i < property.enumDisplayNames.Length; i++)
                {
                    if (property.enumDisplayNames[i] == targetName)
                    {
                        index = i;
                        return true;
                    }
                }
            }

            index = -1;
            return false;
        }

        private static string GetEnumName(SerializedProperty property, int index)
        {
            if (property.enumNames != null && index >= 0 && index < property.enumNames.Length)
                return property.enumNames[index];
            if (property.enumDisplayNames != null && index >= 0 && index < property.enumDisplayNames.Length)
                return property.enumDisplayNames[index];
            return string.Empty;
        }

        private static string GetTargetStateLabel(TargetState targetState)
        {
            switch (targetState)
            {
                case TargetState.Enabled:
                    return "Enabled";
                case TargetState.Disabled:
                    return "Disabled";
                default:
                    return "Leave unchanged";
            }
        }

        private static bool IsRowValid(PhysBoneRow row)
        {
            return row != null && row.PhysBone != null;
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
    }
}
