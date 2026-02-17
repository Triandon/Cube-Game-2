using Core.Item;
using UnityEngine;

public class InventoryCursor : MonoBehaviour
{
    public ItemStack CursorStack { get; private set; } = ItemStack.Empty;

    public bool HasItem => !CursorStack.IsEmpty;

    public void HandleSlotClick(Inventory inventory, int slotIndex)
    {
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
            if (slotStack.IsEmpty) return;

            CursorStack = new ItemStack(
                slotStack.itemId,
                slotStack.count,
                slotStack.displayName,
                slotStack.composition?.Clone()
            );

            inventory.slots[slotIndex] = ItemStack.Empty;
            inventory.InventoryChanged();
            return;
        }

        // Cursor has item → drop
        HandleDrop(inventory, slotIndex);
    }

    public void HandleSlotRightClick(Inventory inventory, int slotIndex)
    {
        ItemStack stack = inventory.slots[slotIndex];
        
        //Case 1 -> take half
        if (CursorStack.IsEmpty)
        {
            if(stack.IsEmpty) return;

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
        else if (stack.itemId == CursorStack.itemId && stack.count < stack.MaxStack)
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
}
