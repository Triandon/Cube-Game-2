using Core.Block;
using UnityEngine;

public class ScaffoldingBlock : Block
{
    public static float PlacementHeight { get; private set; } = 1f;
    public static bool PlaceVertical { get; private set; }
    
    public ScaffoldingBlock(byte id, string name, int top, int side, int bottom, int front = -1) : base(id, name, top, side, bottom, front)
    {
    }

    public static void SetPlacementHeight(float height)
    {
        PlacementHeight = Mathf.Clamp(height, 0.1f, 1f);
    }

    public static void SetPlacementMode(bool placementMode)
    {
        PlaceVertical = placementMode;
    }

    public override void OnPlaced(Vector3Int position, BlockStateContainer state, Transform player)
    {
        base.OnPlaced(position, state, player);
        
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
        state.SetState(BlockStateKeys.HeightState, PlacementHeight.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
    }
}
