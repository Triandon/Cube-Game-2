using Core.Blocks.BlockLogic;
using Core.Item;
using Crafting;
using UnityEngine;

namespace Misc.InventoryHolders
{
    public class CrusherInventoryHolder : InventoryHolder
    {
        public override InventoryHolderType HolderType => InventoryHolderType.CrushingBlock;

        [SerializeField] private int currentCrushingTime;

        public int CurrentCrushingTime => currentCrushingTime;

        public void Init(Vector3Int worldBlockPos)
        {
            ownerName = $"crusher_{worldBlockPos.x}_{worldBlockPos.y}_{worldBlockPos.z}";
            gameObject.name = ownerName;

            inventorySize = CrushingLogic.SlotCount;
            inventory = new Inventory(inventorySize);

            Core.WorldSaveSystem.LoadInventory(ownerName, inventory);

        }

        public bool HasInputItem()
        {
            return inventory != null
                   && inventory.slots.Length > CrushingLogic.InputSlot
                   && !inventory.slots[CrushingLogic.InputSlot].IsEmpty;
        }

        public bool TryInsertInput(ItemStack itemStack)
        {
            if (itemStack == null || itemStack.IsEmpty || HasInputItem())
                return false;

            CrushingRecipe recipe = CrushingLogic.FindRecipe(itemStack);
            if (recipe == null)
                return false;

            inventory.slots[CrushingLogic.InputSlot] = new ItemStack(
                itemStack.itemId, 1, itemStack.displayName,
                itemStack.composition?.Clone());
            currentCrushingTime = 0;
            inventory.InventoryChanged();
            return true;
        }

        public ItemStack RegisterCrushClick()
        {
            if (!HasInputItem())
                return ItemStack.Empty;

            ItemStack input = inventory.slots[CrushingLogic.InputSlot];
            CrushingRecipe recipe = CrushingLogic.FindRecipe(input);

            if (recipe == null)
                return ItemStack.Empty;

            currentCrushingTime++;

            if (!recipe.IsCompleted(currentCrushingTime))
            {
                inventory.InventoryChanged();
                return ItemStack.Empty;
            }
            
            ProcessContext context = CrushingLogic.BuildContext(input, currentCrushingTime, recipe.TotalCrushingTime);
            ItemStack output = recipe.CreateOutput(context);

            inventory.slots[CrushingLogic.InputSlot] = ItemStack.Empty;
            currentCrushingTime = 0;
            inventory.InventoryChanged();

            return output == null ? ItemStack.Empty : output;
        }
    }
}