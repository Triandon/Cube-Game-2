using System;
using System.Collections.Generic;
using Core;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class InventoryHolder : MonoBehaviour
{
    [SerializeField]private string ownerName;
    [SerializeField]private int inventorySize;
    [SerializeField]protected Inventory inventory;

    public Inventory Inventory => inventory;

    public static UnityAction<Inventory> OnInventoryDisplayRequested;

    public static Dictionary<string, InventoryHolder> Holders =
        new Dictionary<string, InventoryHolder>();

    private Settings settings;
    private void Awake()
    {
        inventory = new Inventory(inventorySize);
        WorldSaveSystem.LoadInventory(ownerName,inventory);
        inventory.InventoryChanged();

        Holders[ownerName] = this;
    }

    private void Start()
    {
        settings = Settings.Instance;
        if (settings != null)
        {
            ownerName = settings.userName;
            Holders[ownerName] = this;
            WorldSaveSystem.LoadInventory(ownerName,inventory);
        }
    }

    public void OpenInvenotry()
    {
        OnInventoryDisplayRequested?.Invoke(inventory);
    }

    public void SaveInventory()
    {
        WorldSaveSystem.SaveInventory(ownerName, inventory);
    }

    private void OnDestroy()
    {
        if (Holders.ContainsKey(ownerName))
            Holders.Remove(ownerName);
    }
}
