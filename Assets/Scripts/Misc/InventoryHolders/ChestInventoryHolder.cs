using Core;
using Core.Item;
using UnityEngine;

public class ChestInventoryHolder : InventoryHolder, IChestInventory
{
    public override InventoryHolderType HolderType => InventoryHolderType.Chest;
    
    private Vector3Int blockPos;

    [Header("Chest settings")] 
    [SerializeField] private int chestSize = 10;
    
    //Debug only!
    [SerializeField, Tooltip("Debug Only!")]
    private ItemStack[] debugSlots;

    public void Init(Vector3Int worldBlockPos)
    {
        blockPos = worldBlockPos;
        ownerName = GenerateChestId(blockPos);
        gameObject.name = ownerName;
        
        //Inv
        inventorySize = chestSize;
        inventory = new Inventory(inventorySize);
        
        WorldSaveSystem.LoadInventory(ownerName, inventory);
        inventory.InventoryChanged();
        
        //Debug only
        inventory.OnInventoryChanged += SyncDebugSlots;
        SyncDebugSlots();
    }

    private string GenerateChestId(Vector3Int p)
    {
        return $"chest_{p.x}_{p.y}_{p.z}";
    }
    
    private void SyncDebugSlots()
    {
        debugSlots = inventory?.slots;
    }
}
