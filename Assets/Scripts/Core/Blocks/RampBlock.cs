using Core.Block;
using UnityEngine;

namespace Core.Blocks
{
    public class RampBlock : Block.Block
    {
        public RampBlock(byte id, string name, int top, int side, int bottom, int front = -1) : base(id, name, top, side, bottom, front)
        {
            shapeIndex = (int)BlockShapes.Triangle;
            AddState(BlockStateKeys.DirectionalFacing, DirectionalFacing.North);
        }

        public override void OnPlaced(Vector3Int position, BlockStateContainer state, Transform player, Vector3Int? placementFace)
        {
            if (state == null)
                return;

            state.SetState(BlockStateKeys.DirectionalFacing, GetHorizontalFacingTowardPlayer(player));
        }
    }
}