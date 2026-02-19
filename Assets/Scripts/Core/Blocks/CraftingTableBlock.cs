using Core;
using Core.Block;
using UnityEngine;

public class CraftingTableBlock : Block
{
    public override bool HasBlockEntity => true;

    public CraftingTableBlock(byte id, string name, int top, int side, int bottom, int front = -1) : base(id, name, top, side, bottom, front)
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

        if (chunk.blockEntities.TryGetValue(local, out InventoryHolder holder))
        {
            holder.OpenInventory();
        }
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
