using Core.Item;
using Crafting;

namespace Core.Blocks.BlockLogic
{
    public static class CrushingLogic
    {
        public const int InputSlot = 0;
        public const int SlotCount = 1;

        public static ProcessContext BuildContext(ItemStack input, int currentCrushingTime = 0,
            int totalCrushingTime = 0)
        {
            ItemStack[] slots = { input ?? ItemStack.Empty };
            return new ProcessContext(
                ProcessType.Crushing,
                inputSlots: slots,
                currentProcessTime: currentCrushingTime,
                totalProcessTime: totalCrushingTime);
        }

        public static CrushingRecipe FindRecipe(ItemStack input)
        {
            ProcessContext context = BuildContext(input);
            return RecipeManager.FindMatch(context) as CrushingRecipe;
        }
    }
}