using Core;
using Core.Block;
using Unity.VisualScripting;
using UnityEngine;

public class ChestBlock : Block
{
    public override bool HasBlockEntity => true;

    public ChestBlock(byte id, string name, int top, int side, int bottom, int front = -1) : base(id, name, top, side, bottom, front)
    {
        AddState("facing","north");
    }

    public override void OnPlaced(Vector3Int position, BlockStateContainer state, Transform player)
    {
        Vector3 forward = -player.transform.forward;

        string facing;

        if (Mathf.Abs(forward.x) > Mathf.Abs(forward.z))
            facing = forward.x > 0 ? "east" : "west";
        else
            facing = forward.z > 0 ? "north" : "south";
        
        state.SetState("facing", facing);
    }

    public override void OnActivated(Vector3Int position, BlockStateContainer state, Block block, Transform player)
    {
        Debug.Log("Chest clicked: " + position);
        
        ChunkManager cm = Object.FindAnyObjectByType<ChunkManager>();
        
        if(cm == null) return;

        Chunk chunk = cm.GetChunkFromWorldPos(position);
        if(chunk == null) return;

        Vector3Int local = chunk.WorldToLocal(position);

        if (chunk.blockEntities.TryGetValue(local, out InventoryHolder holder))
        {
            holder.OpenInventory();
        }
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
            holder.SaveInventory();
        }
    }
}
