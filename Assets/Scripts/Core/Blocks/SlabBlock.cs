using Core.Block;
using UnityEngine;

public class SlabBlock : Block
{
    public const string HeightState = BlockStateKeys.HeightState;
    public const string OrientationState = BlockStateKeys.DirectionalFacing;

    private readonly float slabHeight;
    
    public SlabBlock(byte id, string name, int top, int side, int bottom, int front = -1) : base(id, name, top, side, bottom, front)
    {
        slabHeight = 0.5f;
        AddState(HeightState, slabHeight.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        AddState(OrientationState, "up");
    }
    
    public override void OnPlaced(Vector3Int position, BlockStateContainer state, Transform player)
    {
        //Vector3 look = -player.forward;

        //string orientation;
        //if (Mathf.Abs(look.y) > Mathf.Abs(look.x) && Mathf.Abs(look.y) > Mathf.Abs(look.z))
        {
            //orientation = look.y >= 0f ? "up" : "down";
        }
        //else if (Mathf.Abs(look.x) > Mathf.Abs(look.z))
        {
            //orientation = look.x >= 0f ? "east" : "west";
        }
        //else
        {
            //orientation = look.z >= 0f ? "north" : "south";
        }

        //state.SetState(OrientationState, orientation);
        state.SetState(OrientationState, "up");
        state.SetState(HeightState, slabHeight.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
    }


}
