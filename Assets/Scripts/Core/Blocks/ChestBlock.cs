using Core.Block;
using Unity.VisualScripting;
using UnityEngine;

public class ChestBlock : Block
{
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
}
