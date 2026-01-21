using System.Collections.Generic;
using Core;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public abstract class InventoryHolder : MonoBehaviour
{
    public string ownerName;
    public int inventorySize;

    protected Inventory inventory;
    public Inventory Inventory => inventory;
    
    public abstract InventoryHolderType HolderType { get; }
    
    public static UnityAction<Inventory, InventoryHolder> OnInventoryDisplayRequested;
    public static UnityAction<InventoryHolder> OnInventoryClosed;

    private Settings settings;
    protected virtual void Awake()
    {
        inventory = new Inventory(inventorySize);
        WorldSaveSystem.LoadInventory(ownerName,inventory);
        inventory.InventoryChanged();
    }

    private void Start()
    {
        
    }

    public void OpenInventory()
    {
        OnInventoryDisplayRequested?.Invoke(inventory, this);
    }

    public void CloseInventory()
    {
        OnInventoryClosed.Invoke(this);
    }

    public void SaveInventory()
    {
        WorldSaveSystem.SaveInventory(ownerName, inventory);
    }

    public string GetInventoryName()
    {
        return ownerName;
    }
}
