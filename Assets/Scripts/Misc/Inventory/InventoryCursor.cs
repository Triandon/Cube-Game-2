using Core.Item;
using UnityEngine;

public class InventoryCursor : MonoBehaviour
{
    public ItemStack CursorStack { get; private set; } = ItemStack.Empty;

    public bool HasItem => !CursorStack.IsEmpty;

    public void HandleSlotClick(Inventory inventory, int slotIndex)
    {
        if (inventory == null || slotIndex < 0 || slotIndex >= inventory.Size)
            return;
        
        ItemStack slotStack = inventory.slots[slotIndex];

        // SHIFT → fast move
        if (Input.GetKey(KeyCode.LeftShift))
        {
            HandleShiftClick(inventory, slotIndex);
            return;
        }

        // Cursor empty → pick up
        if (CursorStack.IsEmpty)
        {
            TryPickUpSlot(inventory, slotIndex);
            return;
        }

        // Cursor has item → drop
        HandleDrop(inventory, slotIndex);
    }

    public void HandleSlotRightClick(Inventory inventory, int slotIndex)
    {
        if (inventory == null || slotIndex < 0 || slotIndex >= inventory.Size)
            return;
        
        ItemStack stack = inventory.slots[slotIndex];
        
        // Case 1 -> take half
        if (CursorStack.IsEmpty)
        {
            if (stack.IsEmpty) return;

            int half = Mathf.CeilToInt(stack.count / 2f);

            CursorStack = new ItemStack(stack.itemId, half, stack.displayName,
                stack.composition?.Clone());
            stack.count -= half;

            if (stack.count <= 0)
                inventory.slots[slotIndex] = ItemStack.Empty;
            
            inventory.InventoryChanged();
            return;
        }
        
        //Case 2 -> Cursor has item, take one :3
        
        HandleRightClickPlaceOne(inventory, slotIndex);
    }

    private void HandleRightClickPlaceOne(Inventory inventory, int targetIndex)
    {
        ItemStack stack = inventory.slots[targetIndex];

        //Create slot -> new slot with 1
        if (stack.IsEmpty)
        {
            inventory.slots[targetIndex] = new ItemStack(
                CursorStack.itemId, 1, CursorStack.displayName, CursorStack.composition?.Clone());
            CursorStack.count--;
        }
        //Same but fill with 1 to existing stack
        else if (stack.itemId == CursorStack.itemId && stack.count < stack.MaxStack &&
                 stack.CanMergeWith(CursorStack))
        {
            stack.MergeComposition(CursorStack.composition, 1);
            stack.count++;
            CursorStack.count--;
        }
        
        //Clean up
        if (CursorStack.count <= 0)
            CursorStack = ItemStack.Empty;
        
        inventory.InventoryChanged();
    }

    private void HandleDrop(Inventory inventory, int targetIndex)
    {
        ItemStack target = inventory.slots[targetIndex];

        // Empty → move
        if (target.IsEmpty)
        {
            inventory.slots[targetIndex] = CursorStack.Clone();
            CursorStack = ItemStack.Empty;
            inventory.InventoryChanged();
            return;
        }

        // Same item → merge
        if (target.itemId == CursorStack.itemId &&
            target.CanMergeWith(CursorStack))
        {
            int before = target.count;
            int remaining = target.AddItemToStack(CursorStack.count);
            int added = target.count - before;

            if (added > 0)
            {
                target.MergeComposition(CursorStack.composition, added);
            }
            
            CursorStack.count = remaining;

            if (added == 0)
            {
                ItemStack temp = target.Clone();
                inventory.slots[targetIndex] = CursorStack.Clone();
                CursorStack = temp;
            }
            else if(CursorStack.count <= 0)
            {
                CursorStack = ItemStack.Empty;
            }

            inventory.InventoryChanged();
            return;
        }

        // Different item → swap
        ItemStack temp2 = target.Clone();
        inventory.slots[targetIndex] = CursorStack.Clone();
        CursorStack = temp2;

        inventory.InventoryChanged();
    }

    private void HandleShiftClick(Inventory inventory, int fromIndex)
    {
        ItemStack source = inventory.slots[fromIndex];
        if (source.IsEmpty) return;

        // Fill non-full stacks first
        for (int i = 0; i < inventory.Size && source.count > 0; i++)
        {
            if (i == fromIndex) continue;

            ItemStack target = inventory.slots[i];
            if (target.itemId == source.itemId && target.count < target.MaxStack &&
                target.CanMergeWith(source))
            {
                int before = target.count;
                source.count = target.AddItemToStack(source.count);
                int added = target.count - before;

                if (added > 0)
                {
                    target.MergeComposition(source.composition, added);
                }
            }
        }

        // Then use empty slots
        for (int i = 0; i < inventory.Size && source.count > 0; i++)
        {
            if (i == fromIndex) continue;

            if (inventory.slots[i].IsEmpty)
            {
                inventory.slots[i] = new ItemStack(source.itemId, 0, source.displayName,
                    source.composition?.Clone());
                source.count = inventory.slots[i].AddItemToStack(source.count);
            }
        }

        // Clear source if empty
        if (source.count <= 0)
            inventory.slots[fromIndex] = ItemStack.Empty;

        inventory.InventoryChanged();
    }


    public void ClearCursor()
    {
        CursorStack = ItemStack.Empty;
    }

    private void TryPickUpSlot(Inventory inventory, int slotIndex)
    {
        ItemStack slotStack = inventory.slots[slotIndex];
        if (slotStack.IsEmpty)
            return;

        ItemStack picked = slotStack.Clone();
        if (picked.IsEmpty)
            return;

        CursorStack = picked;
        inventory.slots[slotIndex] = ItemStack.Empty;
        inventory.InventoryChanged();
    }

    public bool CanAcceptStack(ItemStack stack)
    {
        if (stack == null || stack.IsEmpty)
            return false;

        if (CursorStack.IsEmpty)
            return true;

        if (CursorStack.itemId != stack.itemId)
            return false;

        if (!CursorStack.CanMergeWith(stack))
            return false;

        return CursorStack.count + stack.count <= CursorStack.MaxStack;
    }

    public bool TryAddToCursor(ItemStack stack)
    {
        if (stack == null || stack.IsEmpty)
            return false;

        if (CursorStack.IsEmpty)
        {
            CursorStack = stack.Clone();
            return true;
        }

        if (CursorStack.itemId != stack.itemId || !CursorStack.CanMergeWith(stack))
            return false;

        int remaining = CursorStack.AddItemToStack(stack.count);
        int added = stack.count - remaining;

        if (added > 0)
            CursorStack.MergeComposition(stack.composition, added);

        return remaining == 0;
    }


}
