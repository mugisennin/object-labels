using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class LabelVisibilityWindow : EditorWindow
{
    private Vector2 _scrollPos;
    private string _searchFilter = "";

    // key: slot index, value: true = visible, false = hidden
    private Dictionary<int, bool> _visibilityState = new Dictionary<int, bool>();

    [MenuItem("Window/Label Visibility")]
    public static void ShowWindow()
    {
        var window = GetWindow<LabelVisibilityWindow>("Label Visibility");
        window.minSize = new Vector2(450, 200);
    }

    private void OnEnable()
    {
        LabelSettings.Reload();
        InitVisibilityState();
    }

    private void OnFocus()
    {
        LabelSettings.EnsureLoaded();
        SyncVisibilityState();
    }

    private void InitVisibilityState()
    {
        _visibilityState.Clear();
        foreach (var kvp in LabelSettings.GetDefinedSlots())
        {
            _visibilityState[kvp.Key] = true;
        }
    }

    // Sync state: if all objects of a label are hidden, reflect that
    private void SyncVisibilityState()
    {
        var defined = LabelSettings.GetDefinedSlots();
        var allLabels = FindObjectsOfType<ObjectLabels>();

        // Ensure new labels are represented
        foreach (var kvp in defined)
        {
            if (!_visibilityState.ContainsKey(kvp.Key))
                _visibilityState[kvp.Key] = true;
        }

        var svm = SceneVisibilityManager.instance;
        foreach (var kvp in defined)
        {
            var objects = GetObjectsWithLabel(allLabels, kvp.Key);
            if (objects.Count == 0) continue;

            bool anyVisible = objects.Any(go => !svm.IsHidden(go));
            _visibilityState[kvp.Key] = anyVisible;
        }
    }

    private List<GameObject> GetObjectsWithLabel(ObjectLabels[] allLabels, int slot)
    {
        return allLabels
            .Where(ol => ol.HasLabel(slot))
            .Select(ol => ol.gameObject)
            .ToList();
    }

    private void OnGUI()
    {
        LabelSettings.EnsureLoaded();
        var defined = LabelSettings.GetDefinedSlots();
        var allLabels = FindObjectsOfType<ObjectLabels>();

        // Toolbar
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("Show All", EditorStyles.toolbarButton))
        {
            SetAllVisibility(defined, allLabels, true);
        }
        if (GUILayout.Button("Hide All", EditorStyles.toolbarButton))
        {
            SetAllVisibility(defined, allLabels, false);
        }
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            LabelSettings.Reload();
            SyncVisibilityState();
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

            if (!_visibilityState.ContainsKey(kvp.Key))
                _visibilityState[kvp.Key] = true;

            bool isVisible = _visibilityState[kvp.Key];

            EditorGUILayout.BeginHorizontal();

            // Visibility toggle button
            string icon = isVisible ? "d_scenevis_visible_hover" : "d_scenevis_hidden_hover";
            var iconContent = EditorGUIUtility.IconContent(icon);
            if (GUILayout.Button(iconContent, GUILayout.Width(28), GUILayout.Height(20)))
            {
                bool newState = !isVisible;
                SetLabelVisibility(objects, newState);
                EditorApplication.RepaintHierarchyWindow();
                EditorApplication.delayCall += () =>
                {
                    SyncVisibilityState();
                    Repaint();
                };
            }

            // Label name and count
            EditorGUI.BeginDisabledGroup(count == 0);
            EditorGUILayout.LabelField($"[{kvp.Key}] {kvp.Value}", GUILayout.ExpandWidth(true));
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.LabelField($"({count})", GUILayout.Width(40));

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

    private void SetLabelVisibility(List<GameObject> objects, bool visible)
    {
        if (objects.Count == 0) return;

        var svm = SceneVisibilityManager.instance;
        foreach (var go in objects)
        {
            if (visible)
                svm.Show(go, false);
            else
                svm.Hide(go, false);
        }
    }

    private void SetAllVisibility(List<KeyValuePair<int, string>> defined, ObjectLabels[] allLabels, bool visible)
    {
        foreach (var kvp in defined)
        {
            var objects = GetObjectsWithLabel(allLabels, kvp.Key);
            SetLabelVisibility(objects, visible);
        }
        EditorApplication.RepaintHierarchyWindow();
        EditorApplication.delayCall += () =>
        {
            SyncVisibilityState();
            Repaint();
        };
    }
}
