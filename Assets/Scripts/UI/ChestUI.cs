using Misc.InventoryHolders;
using Unity.VisualScripting;
using UnityEngine;

public class ChestUI : InventoryViewManager
{
    [SerializeField] private GameObject root;
    private IInventoryOwner currentHolder;

    protected override void OnEnable()
    {
        base.OnEnable();
        InventoryEvents.OnInventoryClosed += OnInventoryClose;
    }
    
    protected override void OnDisable()
    {
        base.OnDisable();
        InventoryEvents.OnInventoryClosed -= OnInventoryClose;
    }
    
    protected override void OnInventoryRequested(Inventory inventory, IInventoryOwner holder)
    {
        if(holder.HolderType != InventoryHolderType.Chest)
            return;
        
        //Toggle behavior
        if (currentHolder == holder)
        {
            Close();
            return;
        }
        
        //Switch for another chest
        if(currentHolder != null)
            currentHolder.SaveInventory();

        currentHolder = holder;
        root.SetActive(true);
        
        base.OnInventoryRequested(inventory, holder);
    }

    private void OnInventoryClose(IInventoryOwner holder)
    {
        if (holder == currentHolder)
        {
            Close();
        }
    }

    public void Close()
    {
        currentHolder?.SaveInventory();
        currentHolder = null;
        root.SetActive(false);
    }
}
