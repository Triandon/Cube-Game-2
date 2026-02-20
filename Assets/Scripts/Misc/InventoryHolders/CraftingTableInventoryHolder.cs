using System;
using Core.Blocks.BlockLogic;
using UnityEngine;

public class CraftingTableInventoryHolder : InventoryHolder
{
    public override InventoryHolderType HolderType => InventoryHolderType.CraftingTable;

    public static readonly int[] InputSlots =
    {
        0, 1, 2, 
        3, 4, 5,
        6, 7, 8
    };

    public static int OutputSlot => CraftingTableLogic.OutputSlot;
    
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

    public int GetCraftedCount()
    {
        return inventory == null ? 0 : CraftingTableLogic.GetCraftableCount(inventory);
    }

    public bool IsInputSlot(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < CraftingTableLogic.InputSlotCount;
    }

    public bool IsOutputSlot(int slotIndex)
    {
        return slotIndex == OutputSlot;
    }

    public int GetCraftableCount()
    {
        return inventory == null ? 0 : CraftingTableLogic.GetCraftableCount(inventory);
    }
}
