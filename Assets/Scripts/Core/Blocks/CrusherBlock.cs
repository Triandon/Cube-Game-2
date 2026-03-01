using Core;
using Core.Block;
using Core.Blocks.BlockLogic;
using Core.Item;
using Misc.InventoryHolders;
using UnityEngine;

public class CrusherBlock : Block
{
    public override bool HasBlockEntity => true;
    public override bool HasScheduledTick => true;

    public CrusherBlock(byte id, string name, int top, int side, int bottom, int front = -1) : base(id, name, top, side, bottom, front)
    {
        
    }

    public override bool OnActivated(Vector3Int position, BlockStateContainer state, Block block, Transform player)
    {
        ChunkManager cm = Object.FindAnyObjectByType<ChunkManager>();
        if (cm == null)
            return false;

        Chunk chunk = cm.GetChunkFromWorldPos(position);
        if (chunk == null)
            return false;

        Vector3Int local = chunk.WorldToLocal(position);

        if (!chunk.blockEntities.TryGetValue(local, out InventoryHolder genericHolder))
        {
            return false;
        }

        if (genericHolder is not CrusherInventoryHolder holder)
        {
            return false;
        }

        HotBarUI hotBar = Object.FindAnyObjectByType<HotBarUI>();
        InventoryHolder playerHolder = player != null ? player.GetComponent<InventoryHolder>() : null;
        
        if (hotBar == null || playerHolder == null || playerHolder.Inventory == null)
            return false;

        if (!holder.HasInputItem())
        {
            int selectedSlot = hotBar.GetSelectedSlot();
            ItemStack selectedStack = hotBar.GetSelectedStack();

            bool inserted = holder.TryInsertInput(selectedStack);
            if (inserted)
            {
                playerHolder.Inventory.RemoveItemFromSlot(selectedSlot, 1);
            }
            
            return true;
        }

        ItemStack output = ProcessCrushing(holder, 1f);
        if (output == null || output.IsEmpty)
            return true;
        
        ItemDropper.Instance.DropItemStack(output, position + new Vector3(0.5f, 1.05f, 0.5f));

        return true;
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

    public override void OnScheduledTick(Vector3Int position, float deltaTime, ChunkManager chunkManager)
    {
        if (chunkManager == null)
            return;
        
        Chunk chunk = chunkManager.GetChunkFromWorldPos(position);
        if (chunk == null)
            return;

        Vector3Int local = chunk.WorldToLocal(position);
        if (!chunk.blockEntities.TryGetValue(local, out InventoryHolder genericHolder))
            return;

        if (genericHolder is not CrusherInventoryHolder holder || !holder.HasInputItem())
            return;

        float progressDelta = Mathf.Max(0f, deltaTime) * 1f;
        if (progressDelta <= 0f)
            return;
        
        ItemStack output = ProcessCrushing(holder, progressDelta);
        if (output == null || output.IsEmpty)
            return;

        ItemDropper.Instance.DropItemStack(output, position + new Vector3(0.5f, 1.05f, 0.5f));
    }
    
    private static ItemStack ProcessCrushing(CrusherInventoryHolder holder, float progressDelta)
    {
        if (holder == null || !holder.HasInputItem())
            return ItemStack.Empty;

        ItemStack input = holder.GetInputItem();
        float progress = holder.CurrentCrushingProgress;

        ItemStack output = CrushingLogic.AdvanceProcess(input, ref progress, progressDelta, out bool completed);

        holder.CurrentCrushingProgress = progress;

        if (!completed)
        {
            holder.Inventory?.InventoryChanged();
            return ItemStack.Empty;
        }

        holder.ClearInputItem();
        holder.Inventory?.InventoryChanged();

        return output;
    }

}
