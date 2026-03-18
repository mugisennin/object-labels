using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Manages label slot-to-name mappings stored in ProjectSettings/LabelSettings.json.
/// Slot range: 0-999.
/// </summary>
public static class LabelSettings
{
    public const int MaxSlots = 1000;
    private const string SettingsPath = "ProjectSettings/LabelSettings.json";

    private static Dictionary<int, string> _slotNames;
    private static bool _loaded;

    [Serializable]
    private class SerializedData
    {
        public List<SlotEntry> slots = new List<SlotEntry>();
    }

    [Serializable]
    private class SlotEntry
    {
        public int index;
        public string name;
    }

    public static Dictionary<int, string> SlotNames
    {
        get
        {
            EnsureLoaded();
            return _slotNames;
        }
    }

    public static void EnsureLoaded()
    {
        if (!_loaded)
        {
            Load();
        }
    }

    public static void Load()
    {
        _slotNames = new Dictionary<int, string>();
        if (File.Exists(SettingsPath))
        {
            try
            {
                string json = File.ReadAllText(SettingsPath);
                var data = JsonUtility.FromJson<SerializedData>(json);
                if (data?.slots != null)
                {
                    foreach (var entry in data.slots)
                    {
                        if (entry.index >= 0 && entry.index < MaxSlots && !string.IsNullOrEmpty(entry.name))
                        {
                            _slotNames[entry.index] = entry.name;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[LabelSettings] Failed to load: {e.Message}");
            }
        }
        _loaded = true;
    }

    public static void Save()
    {
        var data = new SerializedData();
        foreach (var kvp in _slotNames.OrderBy(k => k.Key))
        {
            data.slots.Add(new SlotEntry { index = kvp.Key, name = kvp.Value });
        }
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SettingsPath, json);
        _loaded = true;
    }

    public static void Reload()
    {
        _loaded = false;
        EnsureLoaded();
    }

    public static string GetName(int slot)
    {
        EnsureLoaded();
        return _slotNames.TryGetValue(slot, out var name) ? name : null;
    }

    public static void SetName(int slot, string name)
    {
        if (slot < 0 || slot >= MaxSlots) return;
        EnsureLoaded();
        if (string.IsNullOrEmpty(name))
        {
            _slotNames.Remove(slot);
        }
        else
        {
            _slotNames[slot] = name;
        }
    }

    public static void RemoveName(int slot)
    {
        EnsureLoaded();
        _slotNames.Remove(slot);
    }

    public static List<KeyValuePair<int, string>> GetDefinedSlots()
    {
        EnsureLoaded();
        return _slotNames.OrderBy(k => k.Key).ToList();
    }

    public static int GetFirstEmptySlot()
    {
        EnsureLoaded();
        for (int i = 0; i < MaxSlots; i++)
        {
            if (!_slotNames.ContainsKey(i))
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Returns slot indices used by ObjectLabels in the scene but not defined in settings.
    /// </summary>
    public static List<int> FindOrphanedSlots()
    {
        var orphaned = new HashSet<int>();
        EnsureLoaded();

#if UNITY_EDITOR
        var allLabels = UnityEngine.Object.FindObjectsOfType<ObjectLabels>();
        foreach (var obj in allLabels)
        {
            foreach (var slot in obj.LabelSlots)
            {
                if (!_slotNames.ContainsKey(slot))
                {
                    orphaned.Add(slot);
                }
            }
        }
#endif
        return orphaned.OrderBy(s => s).ToList();
    }

    /// <summary>
    /// Exports current settings to a JSON file.
    /// </summary>
    public static void ExportToFile(string path)
    {
        var data = new SerializedData();
        EnsureLoaded();
        foreach (var kvp in _slotNames.OrderBy(k => k.Key))
        {
            data.slots.Add(new SlotEntry { index = kvp.Key, name = kvp.Value });
        }
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Parses an import file and returns its slot entries without applying them.
    /// </summary>
    public static Dictionary<int, string> ParseImportFile(string path)
    {
        var result = new Dictionary<int, string>();
        if (!File.Exists(path)) return result;

        string json = File.ReadAllText(path);
        var data = JsonUtility.FromJson<SerializedData>(json);
        if (data?.slots != null)
        {
            foreach (var entry in data.slots)
            {
                if (entry.index >= 0 && entry.index < MaxSlots && !string.IsNullOrEmpty(entry.name))
                {
                    result[entry.index] = entry.name;
                }
            }
        }
        return result;
    }
}
