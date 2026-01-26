using System;
using System.Collections.Generic;
using Core.Item;
using Unity.VisualScripting;
using UnityEngine;

public class InventoryViewManager : MonoBehaviour
{
    [SerializeField] private InventorySlotUI slotPrefab;
    [SerializeField] private Transform slotParent;
    [SerializeField] public InventoryCursor cursor;
    private TooltipUI tooltipUI;

    protected Inventory inventory;
    protected readonly List<InventorySlotUI> slots = new();
    protected readonly Dictionary<int, InventorySlotUI> slotByIndex = new Dictionary<int, InventorySlotUI>();

    protected ItemStack cursorStack = ItemStack.Empty;

    protected void OnEnable()
    {
        InventoryHolder.OnInventoryDisplayRequested += OnInventoryRequested;
    }

    protected void OnDisable()
    {
        InventoryHolder.OnInventoryDisplayRequested -= OnInventoryRequested;

        if (inventory != null)
            inventory.OnInventoryResized -= RebuildSlots;
        
        cursor.ClearCursor();
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.OnInventoryResized -= RebuildSlots;
    }

    protected virtual void OnInventoryRequested(Inventory inv, InventoryHolder holder)
    {
        if(this is HotBarUI && holder is not IPlayerInventory)
            return;

        if (inventory != null)
            inventory.OnInventoryResized -= RebuildSlots;
        
        inventory = inv;

        inventory.OnInventoryResized += RebuildSlots;
        BuildSlots();
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
        tooltipUI?.Hide();
    }
    
    public void SlotRightClicked(InventorySlotUI clickedSlot)
    {
        cursor.HandleSlotRightClick(inventory, clickedSlot.SlotIndex);
        tooltipUI?.Hide();
    }

    public void ShowTooltip(ItemStack stack)
    {
        if(!cursor.CursorStack.IsEmpty)
            return;
        
        tooltipUI?.Show(stack);
    }

    public void HideToolTip()
    {
        tooltipUI?.Hide();
    }

    private void Awake()
    {
        if (tooltipUI == null)
            tooltipUI = FindObjectOfType<TooltipUI>(true);
    }
}
