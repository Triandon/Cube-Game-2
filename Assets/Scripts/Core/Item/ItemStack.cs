using System;
using System.Collections.Generic;
using System.Linq;
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

        public bool CanMergeWith(ItemStack other)
        {
            if (other == null) return false;
            if (itemId != other.itemId) return false;
            
            // Items with no composition can stack!
            if (composition == null && other.composition == null) return true;
            
            // If only one stack has composition, the separate!
            if (composition == null || other.composition == null) return false;

            var dictA = composition.contents;
            var dictB = other.composition.contents;
            
            //Union of all material Ids
            var allMat = new HashSet<int>(dictA.Keys);
            allMat.UnionWith(dictB.Keys);

            foreach (var id in allMat)
            {
                var mat = MaterialRegistry.GetMaterial(id);
                if (mat == null) continue;

                float aVal = dictA.ContainsKey(id) ? dictA[id] : 0f;
                float bVal = dictB.ContainsKey(id) ? dictB[id] : 0;
                
                //If any mat is continuous, skip tolerance check
                if (mat.isContinuous)
                    continue;
                
                // Per mat tolerance check
                if (Mathf.Abs(aVal - bVal) > mat.mergeTolerance)
                    return false;
            }

            return true;
        }
    }
}
