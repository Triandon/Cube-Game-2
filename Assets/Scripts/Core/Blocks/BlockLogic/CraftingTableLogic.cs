using Core.Crafting;
using Core.Item;
using Crafting;

namespace Core.Blocks.BlockLogic
{
    public static class CraftingTableLogic
    {
        public const int GridWidth = 3;
        public const int GridHeight = 3;
        public const int InputSlotCount = GridWidth * GridHeight;
        public const int OutputSlot = InputSlotCount;
        public const int TotalSlotCount = InputSlotCount + 1;

        public static ProcessContext BuildContext(Inventory inventory)
        {
            ItemStack[] gridItems = new ItemStack[InputSlotCount];
            for (int i = 0; i < InputSlotCount; i++)
            {
                gridItems[i] = inventory.slots[i];
            }

            CraftingGrid grid = new CraftingGrid(GridWidth, GridHeight, gridItems);

            return new ProcessContext(ProcessType.Crafting, craftingGrid: grid);
        }

        public static ItemStack RecalculateOutput(Inventory inventory)
        {
            ProcessContext context = BuildContext(inventory);
            ItemStack output = RecipeManager.TryCreateOutput(context);
            inventory.slots[OutputSlot] = output == null ? ItemStack.Empty : output.Clone();
            return inventory.slots[OutputSlot];
        }

        public static int GetCraftableCount(Inventory inventory)
        {
            ProcessContext context = BuildContext(inventory);
            IProcessRecipe recipe = RecipeManager.FindMatch(context);

            if (recipe is not CraftingGridRecipe craftingGridRecipe)
            {
                return 0;
            }

            return craftingGridRecipe.GetMaxCraftCount(context, inventory.slots);
        }

        public static bool TryCraftOnce(Inventory inventory)
        {
            ProcessContext context = BuildContext(inventory);
            IProcessRecipe recipe = RecipeManager.FindMatch(context);
            if (recipe is not CraftingGridRecipe craftingGridRecipe)
            {
                return false;
            }

            bool consumed = craftingGridRecipe.TryConsumeInputs(context, inventory.slots);
            if (!consumed)
            {
                return false;
            }

            RecalculateOutput(inventory);
            return true;
        }
    }
}
