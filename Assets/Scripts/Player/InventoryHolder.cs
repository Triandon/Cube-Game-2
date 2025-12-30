using System;
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
    private void Awake()
    {
        inventory = new Inventory(inventorySize);
        WorldSaveSystem.LoadInventory(ownerName,inventory);
        inventory.InventoryChanged();
    }

    public void OpenInvenotry()
    {
        OnInventoryDisplayRequested?.Invoke(inventory);
    }

    public void SaveInventory()
    {
        WorldSaveSystem.SaveInventory(ownerName, inventory);
    }
}
