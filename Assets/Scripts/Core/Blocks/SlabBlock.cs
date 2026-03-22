using Core.Block;
using UnityEngine;

public class SlabBlock : Block
{
    public const string HeightState = BlockStateKeys.HeightState;
    public const string OrientationState = BlockStateKeys.DirectionalFacing;
    public static bool PlaceVertical { get; private set; }

    private readonly float slabHeight;
    
    public SlabBlock(byte id, string name, int top, int side, int bottom, int front = -1) : base(id, name, top, side, bottom, front)
    {
        slabHeight = 0.5f;
        isTransparent = true;
        AddState(HeightState, slabHeight.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        AddState(OrientationState, "up");
    }
    
    public static void SetPlacementMode(bool placementMode)
    {
        PlaceVertical = placementMode;
    }
    
    public override void OnPlaced(Vector3Int position, BlockStateContainer state, Transform player)
    {
        if (state == null)
            return;

        string directionalFacing = "up";

        if (PlaceVertical && player != null)
        {
            Vector3 forward = -player.transform.forward;
            
            if (Mathf.Abs(forward.x) > Mathf.Abs(forward.z))
                directionalFacing = forward.x > 0 ? "east" : "west";
            else
                directionalFacing = forward.z > 0 ? "north" : "south";
        }
        
        state.SetState(BlockStateKeys.DirectionalFacing, directionalFacing);
        state.SetState(HeightState, slabHeight.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
    }


}
