using Core;
using Core.Block;
using Unity.VisualScripting;
using UnityEngine;

public class ChestBlock : Block
{
    public override bool HasBlockEntity => true;

    public ChestBlock(byte id, string name, int top, int side, int bottom, int front = -1) : base(id, name, top, side, bottom, front)
    {
        AddState(BlockStateKeys.DirectionalFacing, DirectionalFacing.North);
    }
    public override bool OnActivated(Vector3Int position, BlockStateContainer state, Block block, Transform player)
    {
        ChunkManager cm = Object.FindAnyObjectByType<ChunkManager>();
        
        if(cm == null) return false;

        Chunk chunk = cm.GetChunkFromWorldPos(position);
        if(chunk == null) return false;

        Vector3Int local = chunk.WorldToLocal(position);

        if (chunk.blockEntities.TryGetValue(local, out InventoryHolder holder))
        {
            holder.OpenInventory();
            return true;
        }

        return false;
    }

    public override void OnMined(Vector3Int position, BlockStateContainer state, Transform player)
    {
        Debug.Log("Mined block at: " + position);
        
        ChunkManager cm = Object.FindAnyObjectByType<ChunkManager>();
        
        if(cm == null) return;

        Chunk chunk = cm.GetChunkFromWorldPos(position);
        if(chunk == null) return;

        Vector3Int local = chunk.WorldToLocal(position);

        if (chunk.blockEntities.TryGetValue(local, out InventoryHolder holder))
        {
            holder.CloseInventory();
            
            //Drops all items
            holder.DropAllItems(position + Vector3.one * 0.5f);
            
            holder.SaveInventory();
            
            //Cleanup removal
            chunk.blockEntities.Remove(local);
            Object.Destroy(holder.gameObject);
        }
    }
}
