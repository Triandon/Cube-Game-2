using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SerializableItemStack
{
    public int itemId;
    public int count;
    public string displayName;
    public CompositionLogic composition;
}

[System.Serializable]
public class InventorySaveData
{
    public List<SerializableItemStack> slots = new List<SerializableItemStack>();
}
