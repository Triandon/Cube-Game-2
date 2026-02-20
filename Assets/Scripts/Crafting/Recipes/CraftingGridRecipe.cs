using System;
using Core.Item;
using Crafting;

namespace Core.Crafting
{
    [Serializable]
    public class CraftingGridRecipe : IProcessRecipe
    {
        public string Id { get; }
        public ProcessType ProcessType => ProcessType.Crafting;

        public int PatternWidth { get; }
        public int PatternHeight { get; }

        private readonly RecipeIngredient[] pattern;
        private readonly Func<ProcessContext, ItemStack> outputFactory;

        public CraftingGridRecipe(
            string id,
            int patternWidth,
            int patternHeight,
            RecipeIngredient[] pattern,
            Func<ProcessContext, ItemStack> outputFactory)
        {
            Id = id;
            PatternWidth = patternWidth;
            PatternHeight = patternHeight;
            this.pattern = pattern;
            this.outputFactory = outputFactory;
        }

        public bool Matches(ProcessContext context)
        {
            return TryFindMatch(context, out _, out _);
        }

        public ItemStack CreateOutput(ProcessContext context)
        {
            return outputFactory != null ? outputFactory(context) : ItemStack.Empty; 
        }
        
        public int GetMaxCraftCount(ProcessContext context, ItemStack[] inputSlots)
        {
            if (inputSlots == null || !TryFindMatch(context, out int offsetX, out int offsetY))
            {
                return 0;
            }

            int maxCrafts = int.MaxValue;
            bool hasConsumableIngredient = false;

            for (int y = 0; y < PatternHeight; y++)
            {
                for (int x = 0; x < PatternWidth; x++)
                {
                    int patternIndex = y * PatternWidth + x;
                    RecipeIngredient ingredient = pattern[patternIndex];
                    if (ingredient == null || !ingredient.consume)
                    {
                        continue;
                    }

                    int gridX = offsetX + x;
                    int gridY = offsetY + y;
                    int slotIndex = gridY * context.CraftingGrid.Width + gridX;
                    ItemStack stack = inputSlots[slotIndex];

                    if (stack == null || stack.IsEmpty || ingredient.consumeCount <= 0)
                    {
                        return 0;
                    }

                    int craftsFromThisSlot = stack.count / ingredient.consumeCount;
                    maxCrafts = Math.Min(maxCrafts, craftsFromThisSlot);
                    hasConsumableIngredient = true;
                }
            }

            if (!hasConsumableIngredient || maxCrafts == int.MaxValue)
            {
                return 0;
            }

            return Math.Max(0, maxCrafts);
        }


        public bool TryConsumeInputs(ProcessContext context, ItemStack[] inputSlots)
        {
            if (inputSlots == null || !TryFindMatch(context, out int offsetX, out int offsetY))
            {
                return false;
            }

            for (int y = 0; y < PatternHeight; y++)
            {
                for (int x = 0; x < PatternWidth; x++)
                {
                    int patternIndex = y * PatternWidth + x;
                    RecipeIngredient ingredient = pattern[patternIndex];
                    if (ingredient == null || !ingredient.consume)
                    {
                        continue;
                    }

                    int gridX = offsetX + x;
                    int gridY = offsetY + y;
                    int slotIndex = gridY * context.CraftingGrid.Width + gridX;

                    inputSlots[slotIndex].RemoveItemToStack(ingredient.consumeCount);
                }
            }

            return true;
        }

        private bool TryFindMatch(ProcessContext context, out int matchX, out int matchY)
        {
            matchX = -1;
            matchY = -1;

            if (context == null || context.ProcessType != ProcessType.Crafting || context.CraftingGrid == null)
            {
                return false;
            }

            if (PatternWidth > context.CraftingGrid.Width || PatternHeight > context.CraftingGrid.Height)
            {
                return false;
            }

            for (int offsetY = 0; offsetY <= context.CraftingGrid.Height - PatternHeight; offsetY++)
            {
                for (int offsetX = 0; offsetX <= context.CraftingGrid.Width - PatternWidth; offsetX++)
                {
                    if (MatchesAt(context.CraftingGrid, offsetX, offsetY))
                    {
                        matchX = offsetX;
                        matchY = offsetY;
                        return true;
                    }
                }
            }

            return false;
        }

        private bool MatchesAt(CraftingGrid grid, int offsetX, int offsetY)
        {
            for (int gridY = 0; gridY < grid.Height; gridY++)
            {
                for (int gridX = 0; gridX < grid.Width; gridX++)
                {
                    int localX = gridX - offsetX;
                    int localY = gridY - offsetY;
                    bool inPattern = localX >= 0 && localX < PatternWidth && localY >= 0 && localY < PatternHeight;

                    ItemStack stack = grid.GetItemStack(gridX, gridY);

                    if (!inPattern)
                    {
                        if (!stack.IsEmpty)
                        {
                            return false;
                        }

                        continue;
                    }

                    int patternIndex = localY * PatternWidth + localX;
                    RecipeIngredient ingredient = pattern[patternIndex];

                    if (ingredient == null)
                    {
                        if (!stack.IsEmpty)
                        {
                            return false;
                        }

                        continue;
                    }

                    if (!ingredient.Matches(stack))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
