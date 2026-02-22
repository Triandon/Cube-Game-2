using Core;
using Core.Block;
using Core.Item;
using Misc.InventoryHolders;
using UnityEngine;

public class CrusherBlock : Block
{
    public override bool HasBlockEntity => true;

    public CrusherBlock(byte id, string name, int top, int side, int bottom, int front = -1) : base(id, name, top, side, bottom, front)
    {
        
    }

    public override void OnActivated(Vector3Int position, BlockStateContainer state, Block block, Transform player)
    {
        ChunkManager cm = Object.FindAnyObjectByType<ChunkManager>();
        if (cm == null)
            return;

        Chunk chunk = cm.GetChunkFromWorldPos(position);
        if (chunk == null)
            return;

        Vector3Int local = chunk.WorldToLocal(position);

        if (!chunk.blockEntities.TryGetValue(local, out InventoryHolder genericHolder))
        {
            return;
        }

        if (genericHolder is not CrushingInventoryHolder holder)
        {
            return;
        }

        HotBarUI hotBar = Object.FindAnyObjectByType<HotBarUI>();
        InventoryHolder playerHolder = player != null ? player.GetComponent<InventoryHolder>() : null;
        
        if (hotBar == null || playerHolder == null || playerHolder.Inventory == null)
            return;

        if (!holder.HasInputItem())
        {
            int selectedSlot = hotBar.GetSelectedSlot();
            ItemStack selectedStack = hotBar.GetSelectedStack();

            bool inserted = holder.TryInsertInput(selectedStack);
            if (inserted)
            {
                playerHolder.Inventory.RemoveItemFromSlot(selectedSlot, 1);
            }
            
            return;
        }

        ItemStack output = holder.RegisterCrushClick();
        if (output == null || output.IsEmpty)
            return;
        
        ItemDropper.Instance.DropItemStack(output, position + new Vector3(0.5f, 1.05f, 0.5f));
    }

    public override void OnMined(Vector3Int position, BlockStateContainer state, Transform player)
    {
        ChunkManager cm = Object.FindAnyObjectByType<ChunkManager>();
        if (cm == null)
            return;

        Chunk chunk = cm.GetChunkFromWorldPos(position);
        if (chunk == null)
            return;

        Vector3Int local = chunk.WorldToLocal(position);

        if (chunk.blockEntities.TryGetValue(local, out InventoryHolder holder))
        {
            holder.CloseInventory();
            holder.DropAllItems(position + Vector3.one * 0.5f);
            holder.SaveInventory();

            chunk.blockEntities.Remove(local);
            Object.Destroy(holder.gameObject);
        }
    }
}
