using Core;
using Core.TileEntities;
using Misc.InventoryHolders;
using UnityEngine;

public class Chest2Tile : TileEntity, IInventoryOwner, IChestInventory
{
    public const int SlotCount = 10;

    public Inventory Inventory { get; } = new Inventory(SlotCount);
    
    // Holder type deems what type of ui should we use and other stuff
    public InventoryHolderType HolderType => InventoryHolderType.Chest;

    private string saveId;

    public override void OnLoad()
    {
        // Separate from the original chest's saves, even at the same position.
        saveId = $"chest2_{Position.x}_{Position.y}_{Position.z}";
        WorldSaveSystem.LoadInventory(saveId, Inventory);
        Inventory.InventoryChanged();
    }

    public void SaveInventory()
    {
        if (saveId != null)
            WorldSaveSystem.SaveInventory(saveId, Inventory);
    }

    public override void OnBlockRemoved()
    {
        // Unloading retains contents; block removal has already emptied them.
        InventoryEvents.Close(this);
        SaveInventory();
    }
}
