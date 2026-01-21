using Core.Item;
using UnityEngine;

public class PlayerInventoryHolder : InventoryHolder, IPlayerInventory
{
    public override InventoryHolderType HolderType => InventoryHolderType.Player;
    
    //Debug only!
    [SerializeField, Tooltip("Debug Only!")]
    private ItemStack[] debugSlots;

    protected override void Awake()
    {
        ownerName = GetOrCreatePlayerId();
        inventorySize = 5;
        
        base.Awake();
        //What runs after is, AFTER the inventory is created
        
        //Debug only
        inventory.OnInventoryChanged += SyncDebugSlots;
        SyncDebugSlots();
    }

    private string GetOrCreatePlayerId()
    {
        if (Settings.Instance == null)
            return "noob";

        if (string.IsNullOrWhiteSpace(Settings.Instance.userName))
            return "noob";

        return Settings.Instance.userName;
    }

    private void SyncDebugSlots()
    {
        debugSlots = inventory?.slots;
    }
}
