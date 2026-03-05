using Unity.VisualScripting;
using UnityEngine;

public class ChestUI : InventoryViewManager
{
    [SerializeField] private GameObject root;
    private InventoryHolder currentHolder;

    protected override void OnEnable()
    {
        base.OnEnable();
        InventoryHolder.OnInventoryClosed += OnInventoryClose;
    }
    
    protected override void OnDisable()
    {
        base.OnDisable();
        InventoryHolder.OnInventoryClosed -= OnInventoryClose;
    }
    
    protected override void OnInventoryRequested(Inventory inventory, InventoryHolder holder)
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

    private void OnInventoryClose(InventoryHolder holder)
    {
        if (holder == currentHolder)
        {
            Close();
        }
    }

    public void Close()
    {
        if(currentHolder != null)
            currentHolder.SaveInventory();
        
        currentHolder?.SaveInventory();
        currentHolder = null;
        root.SetActive(false);
    }
}
