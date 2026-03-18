using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class LabelActiveWindow : EditorWindow
{
    private Vector2 _scrollPos;
    private string _searchFilter = "";

    [MenuItem("Window/Label Active")]
    public static void ShowWindow()
    {
        var window = GetWindow<LabelActiveWindow>("Label Active");
        window.minSize = new Vector2(450, 200);
    }

    private void OnFocus()
    {
        LabelSettings.EnsureLoaded();
    }

    private void OnGUI()
    {
        LabelSettings.EnsureLoaded();
        var defined = LabelSettings.GetDefinedSlots();
        var allLabels = FindObjectsOfType<ObjectLabels>(true);

        // Toolbar
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("Activate All", EditorStyles.toolbarButton))
        {
            SetAllActive(defined, allLabels, true);
        }
        if (GUILayout.Button("Deactivate All", EditorStyles.toolbarButton))
        {
            SetAllActive(defined, allLabels, false);
        }
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            LabelSettings.Reload();
            Repaint();
        }
        EditorGUILayout.EndHorizontal();

        // Search
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
        _searchFilter = EditorGUILayout.TextField(_searchFilter);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);

        if (defined.Count == 0)
        {
            EditorGUILayout.HelpBox("No labels defined. Open Window > Label Manager to define labels.", MessageType.Info);
            return;
        }

        var filtered = string.IsNullOrEmpty(_searchFilter)
            ? defined
            : defined.Where(kvp =>
                kvp.Value.ToLower().Contains(_searchFilter.ToLower()) ||
                kvp.Key.ToString().Contains(_searchFilter)).ToList();

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        foreach (var kvp in filtered)
        {
            var objects = GetObjectsWithLabel(allLabels, kvp.Key);
            int count = objects.Count;
            int activeCount = objects.Count(go => go.activeSelf);

            // Determine toggle state: all active = true, otherwise false
            bool allActive = count > 0 && activeCount == count;

            EditorGUILayout.BeginHorizontal();

            // Active toggle
            EditorGUI.BeginDisabledGroup(count == 0);
            bool newAllActive = EditorGUILayout.Toggle(allActive, GUILayout.Width(20));
            if (newAllActive != allActive)
            {
                SetActive(objects, newAllActive);
            }
            EditorGUI.EndDisabledGroup();

            // Label name
            EditorGUI.BeginDisabledGroup(count == 0);
            EditorGUILayout.LabelField($"[{kvp.Key}] {kvp.Value}", GUILayout.ExpandWidth(true));
            EditorGUI.EndDisabledGroup();

            // Active count
            string countLabel = count == 0 ? "(0)" : $"({activeCount}/{count})";
            EditorGUILayout.LabelField(countLabel, GUILayout.Width(50));

            // Select button
            EditorGUI.BeginDisabledGroup(count == 0);
            if (GUILayout.Button("Select", GUILayout.Width(52)))
            {
                Selection.objects = objects.Cast<Object>().ToArray();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    private List<GameObject> GetObjectsWithLabel(ObjectLabels[] allLabels, int slot)
    {
        return allLabels
            .Where(ol => ol.HasLabel(slot))
            .Select(ol => ol.gameObject)
            .ToList();
    }

    private void SetActive(List<GameObject> objects, bool active)
    {
        foreach (var go in objects)
        {
            Undo.RecordObject(go, active ? "Activate Objects" : "Deactivate Objects");
            go.SetActive(active);
        }
    }

    private void SetAllActive(List<KeyValuePair<int, string>> defined, ObjectLabels[] allLabels, bool active)
    {
        foreach (var kvp in defined)
        {
            SetActive(GetObjectsWithLabel(allLabels, kvp.Key), active);
        }
    }
}
