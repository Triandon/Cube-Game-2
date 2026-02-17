using UnityEngine;

namespace Core.Item
{
    [System.Serializable]
    public class ItemStack
    {
        public int itemId;
        public int count;
        public string displayName;
        public CompositionLogic composition;

        public Item Item => ItemRegistry.GetItem(itemId);

        public bool IsEmpty => itemId == 0 || count <= 0 || Item == null;

        public static ItemStack Empty => new ItemStack(0, 0, "");

        public ItemStack(int itemId, int count, string displayName, CompositionLogic composition = null)
        {
            this.itemId = itemId;
            this.count = count;
            this.displayName = displayName;
            this.composition = composition;
        }

        public int MaxStack => Item != null ? Item.maxStackSize : 1;

        public int AddItemToStack(int amount)
        {
            int spaceLeft = MaxStack - count;
            int toAdd = Mathf.Min(spaceLeft, amount);

            count += toAdd;
            return amount - toAdd;
        }

        public int RemoveItemToStack(int amount)
        {
            int removed = Mathf.Min(amount, count);
            count -= removed;

            if (count <= 0)
            {
                itemId = 0;
                count = 0;
                displayName = "";
            }

            return amount - removed;
        }

        public ItemStack Clone()
        {
            return new ItemStack(itemId, count, displayName, composition?.Clone());
        }

        public void MergeComposition(CompositionLogic other, int otherAmount)
        {
            if (other == null)
                return;

            if (composition == null)
            {
                composition = other;
                return;
            }

            composition = CompositionLogic.Combine(
                composition, count, other, otherAmount);
        }
    }
}
