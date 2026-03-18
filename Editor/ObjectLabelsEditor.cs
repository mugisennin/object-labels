using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ObjectLabels))]
public class ObjectLabelsEditor : Editor
{
    private SerializedProperty _labelSlotsProp;

    private void OnEnable()
    {
        _labelSlotsProp = serializedObject.FindProperty("labelSlots");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        LabelSettings.EnsureLoaded();

        var definedSlots = LabelSettings.GetDefinedSlots();

        // Collect (arrayIndex, slotValue) pairs and sort by slotValue for display
        var slotEntries = new List<(int arrayIndex, int slotValue)>();
        for (int i = 0; i < _labelSlotsProp.arraySize; i++)
        {
            slotEntries.Add((i, _labelSlotsProp.GetArrayElementAtIndex(i).intValue));
        }
        slotEntries.Sort((a, b) => a.slotValue.CompareTo(b.slotValue));

        // Show current labels (sorted)
        int removeAtArrayIndex = -1;
        foreach (var (arrayIndex, slotValue) in slotEntries)
        {
            string labelName = LabelSettings.GetName(slotValue);

            EditorGUILayout.BeginHorizontal();

            if (labelName != null)
            {
                EditorGUILayout.LabelField($"[{slotValue}] {labelName}");
            }
            else
            {
                // Orphaned label: name not defined
                EditorGUILayout.LabelField($"[{slotValue}] (undefined)", EditorStyles.boldLabel);
            }

            if (GUILayout.Button("×", GUILayout.Width(24)))
            {
                removeAtArrayIndex = arrayIndex;
            }

            EditorGUILayout.EndHorizontal();
        }

        if (removeAtArrayIndex >= 0)
        {
            _labelSlotsProp.DeleteArrayElementAtIndex(removeAtArrayIndex);
        }

        // Add label dropdown
        if (definedSlots.Count > 0)
        {
            // Collect already-assigned slots
            var assigned = new HashSet<int>();
            for (int i = 0; i < _labelSlotsProp.arraySize; i++)
            {
                assigned.Add(_labelSlotsProp.GetArrayElementAtIndex(i).intValue);
            }

            // Build list of available labels (defined and not yet assigned)
            var available = definedSlots.Where(kvp => !assigned.Contains(kvp.Key)).ToList();

            if (available.Count > 0)
            {
                EditorGUILayout.Space(4);

                // Build popup options
                var displayNames = new string[available.Count + 1];
                displayNames[0] = "— Add Label —";
                for (int i = 0; i < available.Count; i++)
                {
                    displayNames[i + 1] = $"[{available[i].Key}] {available[i].Value}";
                }

                int selected = EditorGUILayout.Popup(0, displayNames);
                if (selected > 0)
                {
                    int slotToAdd = available[selected - 1].Key;
                    int newIndex = _labelSlotsProp.arraySize;
                    _labelSlotsProp.InsertArrayElementAtIndex(newIndex);
                    _labelSlotsProp.GetArrayElementAtIndex(newIndex).intValue = slotToAdd;
                }
            }
            else
            {
                EditorGUILayout.HelpBox("All defined labels are assigned.", MessageType.Info);
            }
        }
        else
        {
            EditorGUILayout.HelpBox(
                "No labels defined. Open Window > Label Manager to define labels.",
                MessageType.Warning);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
