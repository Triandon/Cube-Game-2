using Core.Block;
using UnityEngine;

namespace Core.Blocks
{
    public class PipeBlock : Block.Block
    {
        public PipeBlock(byte id, string name, int top, int side, int bottom, int front = -1) : base(id, name, top, side, bottom, front)
        {
            SetDefaultState(new BlockStateContainer().With(BlockStateKeys.WidthState, "0.5").
                With(BlockStateKeys.DirectionalFacing, DirectionalFacing.North));
            isCentered = true;
            isStrechy = true;
            isTransparent = true;
        }

        public override BlockStateContainer GetStateForPlacement(BlockPlacementContext context)
        {
            return CreateDefaultState().With(BlockStateKeys.WidthState, "0.5").
                With(BlockStateKeys.DirectionalFacing, GetHorizontalFacingTowardPlayer(context.Player));
        }

        public override bool OnActivated(Vector3Int position, BlockStateContainer state, Block.Block block, Transform player)
        {
            string facing = state.GetState(BlockStateKeys.DirectionalFacing);
            
            string newFacing = facing switch
            {
                "north" => DirectionalFacing.East,
                "east"  => DirectionalFacing.South,
                "south" => DirectionalFacing.West,
                "west"  => DirectionalFacing.North,
                _       => DirectionalFacing.North
            };
            
            state.SetState(BlockStateKeys.DirectionalFacing, newFacing);

            return false;
        }
    }
}
