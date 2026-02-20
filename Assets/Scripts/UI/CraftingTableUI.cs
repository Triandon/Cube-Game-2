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

        if (currentHolder != null && currentHolder.IsOutputSlot(clickedSlot.SlotIndex))
        {
            HandleOutputClick();
            return;
        }
        
        base.SlotClicked(clickedSlot);
    }
    
    public override string GetSlotCountText(int slotIndex, ItemStack stack)
    {
        if (currentHolder != null && currentHolder.IsOutputSlot(slotIndex) && !stack.IsEmpty)
        {
            int craftableCount = currentHolder.GetCraftableCount();
            return craftableCount > 0 ? $"{craftableCount}x{stack.count}" : "";
        }

        return base.GetSlotCountText(slotIndex, stack);
    }


    public override void SlotRightClicked(InventorySlotUI clickedSlot)
    {
        if (currentHolder != null && currentHolder.IsOutputSlot(clickedSlot.SlotIndex))
            return;
        
        base.SlotRightClicked(clickedSlot);
    }

    public void Close()
    {
        currentHolder?.SaveInventory();
        currentHolder = null;
        root.SetActive(false);
    }

    private void HandleOutputClick()
    {
        ItemStack output = inventory.slots[CraftingTableInventoryHolder.OutputSlot];
        
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
