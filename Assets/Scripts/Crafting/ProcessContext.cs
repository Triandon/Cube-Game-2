using Core.Item;

namespace Crafting
{
    [System.Serializable]
    public class ProcessContext
    {
        public ProcessType ProcessType;
        public CraftingGrid CraftingGrid;
        public ItemStack[] InputSlots;

        public ProcessContext(ProcessType processType, CraftingGrid craftingGrid = null,
            ItemStack[] inputSlots = null)
        {
            ProcessType = processType;
            CraftingGrid = craftingGrid;
            InputSlots = inputSlots;
        }

    }
}
