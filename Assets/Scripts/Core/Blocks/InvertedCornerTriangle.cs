using Core.Block;
using UnityEngine;

namespace Core.Blocks
{
    public class InvertedCornerTriangle : Block.Block
    {
        public InvertedCornerTriangle(byte id, string name, int top, int side, int bottom, int front = -1) : base(id, name, top, side, bottom, front)
        {
            shapeIndex = (int)BlockShapes.InvertedCornerTriangle;
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