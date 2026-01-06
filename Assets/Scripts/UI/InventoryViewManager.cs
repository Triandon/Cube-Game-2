using System;
using System.Collections.Generic;
using UnityEngine;

public class InventoryViewManager : MonoBehaviour
{
    [SerializeField] private InventoryHolder inventoryHolder;
    [SerializeField] private InvenotrySlotUI slotPrefab;
    [SerializeField] private Transform slotParent;

    protected Inventory inventory;
    protected readonly List<InvenotrySlotUI> slots = new();

    protected virtual void Start()
    {
        inventory = inventoryHolder.Inventory;

        inventory.OnInventoryResized += RebuildSlots;
        BuildSlots();
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.OnInventoryResized -= RebuildSlots;
    }

    protected void BuildSlots()
    {
        foreach (Transform child in slotParent)
        {
            Destroy(child.gameObject);
        }
        
        slots.Clear();

            
        for (int i = 0; i < inventory.Size; i++)
        {
            var slot = Instantiate(slotPrefab, slotParent);
            slot.Init(inventory, i);
            slots.Add(slot);
        }
    }

    protected void RebuildSlots()
    {
        BuildSlots();
    }
}
