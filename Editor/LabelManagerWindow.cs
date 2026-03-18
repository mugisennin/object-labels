using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class LabelManagerWindow : EditorWindow
{
    // Main view
    private Vector2 _scrollPos;
    private string _searchFilter = "";
    private string _newLabelName = "";
    private int _newLabelSlot = -1;

    // Import state
    private enum ImportState { None, Loaded, Resolved }
    private ImportState _importState = ImportState.None;
    private string _importPath;
    private Dictionary<int, string> _importData;
    private List<SlotConflict> _conflicts;
    private List<string> _duplicateNameWarnings;
    private Vector2 _importScrollPos;

    // Orphan warnings
    private List<int> _orphanedSlots;

    private class SlotConflict
    {
        public int slot;
        public string currentName;
        public string importName;
        public bool useImport; // true = use import, false = keep current
        public bool resolved;
    }

    [MenuItem("Window/Label Manager")]
    public static void ShowWindow()
    {
        var window = GetWindow<LabelManagerWindow>("Label Manager");
        window.minSize = new Vector2(400, 300);
    }

    private void OnEnable()
    {
        LabelSettings.Reload();
        RefreshOrphans();
    }

    private void OnFocus()
    {
        LabelSettings.EnsureLoaded();
        RefreshOrphans();
    }

    private void RefreshOrphans()
    {
        _orphanedSlots = LabelSettings.FindOrphanedSlots();
    }

    private void OnGUI()
    {
        if (_importState != ImportState.None)
        {
            DrawImportView();
            return;
        }

        DrawMainView();
    }

    // =========================================================================
    // Main View
    // =========================================================================
    private void DrawMainView()
    {
        // Toolbar
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("Import", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            BeginImport();
        }
        if (GUILayout.Button("Export", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            DoExport();
        }
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            LabelSettings.Reload();
            RefreshOrphans();
        }
        EditorGUILayout.EndHorizontal();

        // Orphan warnings
        if (_orphanedSlots != null && _orphanedSlots.Count > 0)
        {
            EditorGUILayout.HelpBox(
                $"Warning: {_orphanedSlots.Count} undefined slot(s) are used by objects in the scene: [{string.Join(", ", _orphanedSlots)}]\n" +
                "These objects have label indices with no corresponding name. Consider importing label definitions or assigning names to these slots.",
                MessageType.Warning);
        }

        // Search
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
        _searchFilter = EditorGUILayout.TextField(_searchFilter);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);

        // Slot list
        var defined = LabelSettings.GetDefinedSlots();
        var filtered = string.IsNullOrEmpty(_searchFilter)
            ? defined
            : defined.Where(kvp =>
                kvp.Value.ToLower().Contains(_searchFilter.ToLower()) ||
                kvp.Key.ToString().Contains(_searchFilter)).ToList();

        EditorGUILayout.LabelField($"Defined Labels: {defined.Count} / {LabelSettings.MaxSlots}", EditorStyles.boldLabel);

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        foreach (var kvp in filtered)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField($"[{kvp.Key}]", GUILayout.Width(50));

            EditorGUI.BeginChangeCheck();
            string newName = EditorGUILayout.TextField(kvp.Value);
            if (EditorGUI.EndChangeCheck() && !string.IsNullOrEmpty(newName))
            {
                LabelSettings.SetName(kvp.Key, newName);
                LabelSettings.Save();
            }

            if (GUILayout.Button("×", GUILayout.Width(24)))
            {
                if (EditorUtility.DisplayDialog("Remove Label",
                    $"Remove label [{kvp.Key}] \"{kvp.Value}\"?\n\nObjects using this slot will retain the index but it will show as (undefined).",
                    "Remove", "Cancel"))
                {
                    LabelSettings.RemoveName(kvp.Key);
                    LabelSettings.Save();
                    RefreshOrphans();
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        // Add new label
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Add New Label", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        if (_newLabelSlot < 0)
        {
            _newLabelSlot = LabelSettings.GetFirstEmptySlot();
        }

        _newLabelSlot = EditorGUILayout.IntField("Slot:", _newLabelSlot, GUILayout.Width(200));
        _newLabelName = EditorGUILayout.TextField(_newLabelName);

        bool canAdd = _newLabelSlot >= 0 && _newLabelSlot < LabelSettings.MaxSlots
            && !string.IsNullOrEmpty(_newLabelName)
            && LabelSettings.GetName(_newLabelSlot) == null;

        EditorGUI.BeginDisabledGroup(!canAdd);
        if (GUILayout.Button("Add", GUILayout.Width(50)))
        {
            LabelSettings.SetName(_newLabelSlot, _newLabelName);
            LabelSettings.Save();
            _newLabelName = "";
            _newLabelSlot = LabelSettings.GetFirstEmptySlot();
            RefreshOrphans();
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndHorizontal();

        if (_newLabelSlot >= 0 && LabelSettings.GetName(_newLabelSlot) != null)
        {
            EditorGUILayout.HelpBox($"Slot {_newLabelSlot} is already in use (\"{LabelSettings.GetName(_newLabelSlot)}\").", MessageType.Warning);
        }
    }

    // =========================================================================
    // Export
    // =========================================================================
    private void DoExport()
    {
        string path = EditorUtility.SaveFilePanel("Export Labels", "", "LabelSettings", "json");
        if (string.IsNullOrEmpty(path)) return;

        LabelSettings.ExportToFile(path);
        EditorUtility.DisplayDialog("Export Complete", $"Labels exported to:\n{path}", "OK");
    }

    // =========================================================================
    // Import
    // =========================================================================
    private void BeginImport()
    {
        string path = EditorUtility.OpenFilePanel("Import Labels", "", "json");
        if (string.IsNullOrEmpty(path)) return;

        _importPath = path;
        _importData = LabelSettings.ParseImportFile(path);

        if (_importData.Count == 0)
        {
            EditorUtility.DisplayDialog("インポート", "有効なラベルデータが見つかりませんでした。", "OK");
            return;
        }

        AnalyzeImport();
        _importState = ImportState.Loaded;
    }

    private void AnalyzeImport()
    {
        _conflicts = new List<SlotConflict>();
        _duplicateNameWarnings = new List<string>();

        var currentSlots = LabelSettings.SlotNames;

        // Detect slot conflicts (same slot, different name)
        foreach (var kvp in _importData)
        {
            if (currentSlots.TryGetValue(kvp.Key, out string currentName))
            {
                if (currentName != kvp.Value)
                {
                    _conflicts.Add(new SlotConflict
                    {
                        slot = kvp.Key,
                        currentName = currentName,
                        importName = kvp.Value,
                        useImport = false,
                        resolved = true,
                    });
                }
                // Same name on same slot → no conflict, just skip
            }
        }

        // Detect duplicate name warnings (same name on different slots)
        var allNames = new Dictionary<string, List<int>>();
        // Merge current + import
        foreach (var kvp in currentSlots)
        {
            if (!allNames.ContainsKey(kvp.Value))
                allNames[kvp.Value] = new List<int>();
            allNames[kvp.Value].Add(kvp.Key);
        }
        foreach (var kvp in _importData)
        {
            // Skip entries that will be replaced by conflict resolution
            bool isConflictSlot = _conflicts.Any(c => c.slot == kvp.Key);
            if (isConflictSlot) continue;

            if (!allNames.ContainsKey(kvp.Value))
                allNames[kvp.Value] = new List<int>();
            if (!allNames[kvp.Value].Contains(kvp.Key))
                allNames[kvp.Value].Add(kvp.Key);
        }

        foreach (var kvp in allNames)
        {
            if (kvp.Value.Count > 1)
            {
                string slots = string.Join(", ", kvp.Value);
                _duplicateNameWarnings.Add($"\"{kvp.Key}\" exists on slots: [{slots}]");
            }
        }

        // If no conflicts, mark as resolved
        if (_conflicts.Count == 0)
        {
            _importState = ImportState.Resolved;
        }
    }

    private void DrawImportView()
    {
        EditorGUILayout.LabelField("ラベルのインポート", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"ファイル: {_importPath}");
        EditorGUILayout.LabelField($"エントリ数: {_importData.Count}");
        EditorGUILayout.Space(4);

        _importScrollPos = EditorGUILayout.BeginScrollView(_importScrollPos);

        // Show conflicts
        if (_conflicts.Count > 0)
        {
            EditorGUILayout.LabelField("スロットの競合", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "以下のスロットは、現在のプロジェクトとインポートファイルで異なる名前が設定されています。それぞれどちらを使用するか選択してください。",
                MessageType.Warning);

            foreach (var conflict in _conflicts)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"スロット [{conflict.slot}]", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();

                bool keepCurrent = !conflict.useImport;
                bool newKeepCurrent = EditorGUILayout.ToggleLeft(
                    $"現在の名前を維持: \"{conflict.currentName}\"", keepCurrent);

                bool useImport = conflict.useImport;
                bool newUseImport = EditorGUILayout.ToggleLeft(
                    $"インポート側を使用: \"{conflict.importName}\"", useImport);

                // Toggle logic: clicking one deselects the other
                if (newKeepCurrent != keepCurrent && newKeepCurrent)
                {
                    conflict.useImport = false;
                    conflict.resolved = true;
                }
                else if (newUseImport != useImport && newUseImport)
                {
                    conflict.useImport = true;
                    conflict.resolved = true;
                }

                EditorGUILayout.EndHorizontal();

                if (!conflict.resolved)
                {
                    EditorGUILayout.LabelField("← どちらかを選択してください", EditorStyles.miniLabel);
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
        }

        // Show duplicate name warnings
        if (_duplicateNameWarnings.Count > 0)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("名前の重複", EditorStyles.boldLabel);
            foreach (var warning in _duplicateNameWarnings)
            {
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }
        }

        // Preview non-conflict additions
        var additions = _importData
            .Where(kvp => !_conflicts.Any(c => c.slot == kvp.Key) && LabelSettings.GetName(kvp.Key) == null)
            .ToList();
        if (additions.Count > 0)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"新規追加 ({additions.Count})", EditorStyles.boldLabel);
            foreach (var kvp in additions)
            {
                EditorGUILayout.LabelField($"  [{kvp.Key}] {kvp.Value}");
            }
        }

        // Already matching
        var matching = _importData
            .Where(kvp => !_conflicts.Any(c => c.slot == kvp.Key) && LabelSettings.GetName(kvp.Key) == kvp.Value)
            .ToList();
        if (matching.Count > 0)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"一致済み ({matching.Count})", EditorStyles.boldLabel);
            foreach (var kvp in matching)
            {
                EditorGUILayout.LabelField($"  [{kvp.Key}] {kvp.Value}");
            }
        }

        EditorGUILayout.EndScrollView();

        // Buttons
        EditorGUILayout.Space(8);
        EditorGUILayout.BeginHorizontal();

        bool allResolved = _conflicts.All(c => c.resolved);

        EditorGUI.BeginDisabledGroup(!allResolved);
        if (GUILayout.Button("インポートを適用", GUILayout.Height(30)))
        {
            ApplyImport();
        }
        EditorGUI.EndDisabledGroup();

        if (!allResolved)
        {
            EditorGUILayout.HelpBox("すべての競合を解決してください。", MessageType.Info);
        }

        if (GUILayout.Button("キャンセル", GUILayout.Height(30), GUILayout.Width(80)))
        {
            CancelImport();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void ApplyImport()
    {
        // Apply non-conflict new entries
        foreach (var kvp in _importData)
        {
            bool isConflict = _conflicts.Any(c => c.slot == kvp.Key);
            if (isConflict) continue;

            // Only add if slot is empty (don't overwrite matching ones, but SetName is safe)
            if (LabelSettings.GetName(kvp.Key) == null)
            {
                LabelSettings.SetName(kvp.Key, kvp.Value);
            }
        }

        // Apply conflict resolutions
        foreach (var conflict in _conflicts)
        {
            if (conflict.useImport)
            {
                LabelSettings.SetName(conflict.slot, conflict.importName);
            }
            // else: keep current, do nothing
        }

        LabelSettings.Save();
        RefreshOrphans();

        int added = _importData.Count(kvp =>
            !_conflicts.Any(c => c.slot == kvp.Key) && LabelSettings.GetName(kvp.Key) == kvp.Value);
        int conflictsResolved = _conflicts.Count;

        EditorUtility.DisplayDialog("インポート完了",
            $"インポートを適用しました。\n新規追加: {_importData.Count(kvp => !_conflicts.Any(c => c.slot == kvp.Key))} 件\n競合解決: {conflictsResolved} 件",
            "OK");

        CancelImport();
    }

    private void CancelImport()
    {
        _importState = ImportState.None;
        _importData = null;
        _conflicts = null;
        _duplicateNameWarnings = null;
        _importPath = null;
    }
}
