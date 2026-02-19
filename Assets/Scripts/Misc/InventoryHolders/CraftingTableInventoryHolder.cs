using System;
using Core.Blocks.BlockLogic;
using UnityEngine;

public class CraftingTableInventoryHolder : InventoryHolder
{
    public override InventoryHolderType HolderType => InventoryHolderType.CraftingTable;

    private bool suppressRebuild;

    public void Init(UnityEngine.Vector3Int worldBlockPos)
    {
        ownerName = $"crafting_table_{worldBlockPos.x}_{worldBlockPos.y}_{worldBlockPos.z}";
        gameObject.name = ownerName;

        inventorySize = CraftingTableLogic.TotalSlotCount;
        inventory = new Inventory(inventorySize);

        Core.WorldSaveSystem.LoadInventory(ownerName, inventory);
        inventory.OnInventoryChanged += OnInventoryChanged;
        RebuildOutput();
    }

    private void OnDestroy()
    {
        if (inventory != null)
        {
            inventory.OnInventoryChanged -= OnInventoryChanged;
        }
    }

    public bool CraftOnce()
    {
        bool crafted = CraftingTableLogic.TryCraftOnce(inventory);
        if (crafted)
        {
            inventory.InventoryChanged();
        }

        return crafted;
    }

    private void OnInventoryChanged()
    {
        if (suppressRebuild)
        {
            return;
        }
        
        RebuildOutput();
    }

    private void RebuildOutput()
    {
        suppressRebuild = true;
        CraftingTableLogic.RecalculateOutput(inventory);
        suppressRebuild = false;
    }
}
