using Core.Item;
using Crafting;
using UnityEngine;

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

        public static ItemStack AdvanceProcess(ItemStack input, ref float currentCrushingProgress,
            float progressDelta, out bool completed)
        {
            completed = false;
            
            if (input == null || input.IsEmpty)
                return ItemStack.Empty;

            CrushingRecipe recipe = FindRecipe(input);
            if (recipe == null)
                return ItemStack.Empty;

            currentCrushingProgress += Mathf.Max(0f, progressDelta);
            
            int currentTime = Mathf.FloorToInt(currentCrushingProgress);
            if (!recipe.IsCompleted(currentTime))
                return ItemStack.Empty;

            ProcessContext context = BuildContext(input, currentTime, recipe.TotalCrushingTime);
            ItemStack output = recipe.CreateOutput(context);

            completed = true;
            return output == null ? ItemStack.Empty : output;
        }
    }
}