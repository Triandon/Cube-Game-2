namespace Misc.InventoryHolders
{
    public interface IInventoryOwner
    {
        Inventory Inventory { get; }
        InventoryHolderType HolderType { get; }

        void SaveInventory();
    }
}
