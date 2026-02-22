using Core.Item;

namespace Crafting
{
    [System.Serializable]
    public class ProcessContext
    {
        public ProcessType ProcessType;
        public CraftingGrid CraftingGrid;
        public ItemStack[] InputSlots;
        public int CurrentProcessTime;
        public int TotalProcessTime;

        public ProcessContext(ProcessType processType, CraftingGrid craftingGrid = null,
            ItemStack[] inputSlots = null, int currentProcessTime = 0,
            int totalProcessTime = 0)
        {
            ProcessType = processType;
            CraftingGrid = craftingGrid;
            InputSlots = inputSlots;
            CurrentProcessTime = currentProcessTime;
            TotalProcessTime = totalProcessTime;
        }

    }
}
