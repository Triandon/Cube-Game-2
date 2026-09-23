using Core.Block;
using UnityEngine;

public class SlabBlock : Block
{
    public const string OrientationState = BlockStateKeys.DirectionalFacing;
    public static bool PlaceVertical { get; private set; }

    private readonly float slabHeight;
    
    public SlabBlock(byte id, string name, int top, int side, int bottom, int front = -1) : base(id, name, top, side, bottom, front)
    {
        slabHeight = 0.5f;
        isTransparent = true;
        SetDefaultState(new BlockStateContainer().With(BlockStateKeys.HeightState,
                slabHeight.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).
            With(BlockStateKeys.DirectionalFacing, "up"));
    }
    
    public static void SetPlacementMode(bool placementMode)
    {
        PlaceVertical = placementMode;
    }

    public override BlockStateContainer GetStateForPlacement(BlockPlacementContext context)
    {
        string directionalFacing =
            context.PlacementFace == Vector3Int.down ? 
                DirectionalFacing.Down : 
                DirectionalFacing.Up;

        if (PlaceVertical && context.Player != null)
        {
            Vector3 forward = -context.Player.transform.forward;
            
            if (Mathf.Abs(forward.x) > Mathf.Abs(forward.z))
                directionalFacing = forward.x > 0 ? "east" : "west";
            else
                directionalFacing = forward.z > 0 ? "north" : "south";
        }

        return CreateDefaultState().With(BlockStateKeys.DirectionalFacing, directionalFacing);
    }

    public override bool OnActivated(Vector3Int position, BlockStateContainer state, Block block, Transform player)
    {
        Debug.Log("Clicked a slab with state: " + state.GetState(BlockStateKeys.DirectionalFacing));
        
        return false;
    }
}
