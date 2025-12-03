using UnityEngine;

namespace Core.Item
{
    public class ItemStack
    {
        public int itemId;
        public int count;

        public Item Item => ItemRegistry.GetItem(itemId);

        public bool IsEmpty => itemId == 0 || count <= 0 || Item == null;

        public ItemStack(int itemId, int count)
        {
            this.itemId = itemId;
            this.count = count;
        }

        public int MaxStack => Item != null ? Item.maxStackSize : 1;

        public int AddItemToStack(int amount)
        {
            if (IsEmpty)
            {
                count = Mathf.Min(amount, MaxStack);
                itemId = itemId == 0 ? itemId : itemId;
                return amount - count;
            }

            int space = MaxStack - count;
            int added = Mathf.Min(space, amount);
            count += added;

            return amount - count;
        }

        public int RemoveItemToStack(int amount)
        {
            int removed = Mathf.Min(amount, count);
            count -= removed;

            if (count <= 0)
            {
                itemId = 0;
                count = 0;
            }

            return amount - removed;
        }
    }
}
