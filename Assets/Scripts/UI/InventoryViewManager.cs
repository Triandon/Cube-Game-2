using System;
using System.Collections.Generic;
using Core.Item;
using UnityEngine;

public class InventoryViewManager : MonoBehaviour
{
    [SerializeField] private InventoryHolder inventoryHolder;
    [SerializeField] private InventorySlotUI slotPrefab;
    [SerializeField] private Transform slotParent;
    [SerializeField] public InventoryCursor cursor;

    protected Inventory inventory;
    protected readonly List<InventorySlotUI> slots = new();
    protected readonly Dictionary<int, InventorySlotUI> slotByIndex = new Dictionary<int, InventorySlotUI>();

    protected ItemStack cursorStack = ItemStack.Empty;

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
        slotByIndex.Clear();
            
        for (int i = 0; i < inventory.Size; i++)
        {
            var slot = Instantiate(slotPrefab, slotParent);
            //slot.name = $"Slot_{i}";
            slot.Init(inventory, i, this);
            
            slots.Add(slot);
            slotByIndex.Add(i, slot);
        }
    }

    protected void RebuildSlots()
    {
        BuildSlots();
    }

    public void SlotClicked(InventorySlotUI clickedSlot)
    {
        cursor.HandleSlotClick(inventory, clickedSlot.SlotIndex);
    }
    
    public void SlotRightClicked(InventorySlotUI clickedSlot)
    {
        cursor.HandleSlotRightClick(inventory, clickedSlot.SlotIndex);
    }

    private void OnDisable()
    {
        cursor.ClearCursor();
    }
}
