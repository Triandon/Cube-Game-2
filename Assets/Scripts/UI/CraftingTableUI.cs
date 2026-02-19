using Core.Blocks.BlockLogic;
using Core.Item;
using Unity.VisualScripting;
using UnityEngine;

public class CraftingTableUI : InventoryViewManager
{ 
    [SerializeField] private GameObject root;
    private CraftingTableInventoryHolder currentHolder;

    protected override void OnInventoryRequested(Inventory inv, InventoryHolder holder)
    {
        if (holder.HolderType != InventoryHolderType.CraftingTable)
            return;

        if (currentHolder == holder)
        {
            Close();
            return;
        }
        
        if (currentHolder != null)
            currentHolder.SaveInventory();
        
        currentHolder = holder as CraftingTableInventoryHolder;
        root.SetActive(true);
        
        base.OnInventoryRequested(inv, holder);
    }

    public override void SlotClicked(InventorySlotUI clickedSlot)
    {
        if (inventory == null)
            return;

        if (clickedSlot.SlotIndex == CraftingTableLogic.OutputSlot)
        {
            HandleOutputClick();
            return;
        }
        
        base.SlotClicked(clickedSlot);
    }

    public override void SlotRightClicked(InventorySlotUI clickedSlot)
    {
        if (clickedSlot.SlotIndex == CraftingTableLogic.OutputSlot)
            return;
        
        base.SlotRightClicked(clickedSlot);
    }

    public void Close()
    {
        currentHolder?.SaveInventory();
        currentHolder = null;
        root.SetActive(false);
        cursor.ClearCursor();
    }

    private void HandleOutputClick()
    {
        ItemStack output = inventory.slots[CraftingTableLogic.OutputSlot];
        
        if (output.IsEmpty || currentHolder == null)
            return;
        
        if (!cursor.CanAcceptStack(output))
            return;

        ItemStack craftedOutput = output.Clone();
        if (!currentHolder.CraftOnce())
            return;

        cursor.TryAddToCursor(craftedOutput);
        inventory.InventoryChanged();
    }

    private void OnEnable()
    {
        base.OnEnable();
        InventoryHolder.OnInventoryClosed += OnInventoryClose;
    }

    private void OnDisable()
    {
        base.OnDisable();
        InventoryHolder.OnInventoryClosed -= OnInventoryClose;
    }
    
    private void OnInventoryClose(InventoryHolder holder)
    {
        if (holder == currentHolder)
        {
            Close();
        }
    }
}
