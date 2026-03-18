using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Utility/Object Labels")]
public class ObjectLabels : MonoBehaviour
{
    [SerializeField]
    private List<int> labelSlots = new List<int>();

    public IReadOnlyList<int> LabelSlots => labelSlots;

    public bool HasLabel(int slot)
    {
        return labelSlots.Contains(slot);
    }

    public void AddLabel(int slot)
    {
        if (!labelSlots.Contains(slot))
        {
            labelSlots.Add(slot);
        }
    }

    public void RemoveLabel(int slot)
    {
        labelSlots.Remove(slot);
    }

    public void ClearLabels()
    {
        labelSlots.Clear();
    }
}
