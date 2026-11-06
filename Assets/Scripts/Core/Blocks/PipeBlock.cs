using Core.Block;
using Core.Item;
using Player;
using UnityEngine;

namespace Core.Blocks
{
    public class PipeBlock : Block.Block
    {
        public PipeBlock(byte id, string name, MaterialProperties material,int top, int side, int bottom, int front = -1) : base(id, name, material,top, side, bottom, front)
        {
            SetDefaultState(new BlockStateContainer().With(BlockStateKeys.WidthState, "0.5").
                With(BlockStateKeys.DirectionalFacing, DirectionalFacing.North));
            isStrechy = true;
            isTransparent = true;
        }

        public override BlockStateContainer GetStateForPlacement(BlockPlacementContext context)
        {
            return CreateDefaultState().With(BlockStateKeys.WidthState, "0.5").
                With(BlockStateKeys.DirectionalFacing, GetHorizontalFacingTowardPlayer(context.Player));
        }

        public override bool OnActivated(Vector3Int position, BlockStateContainer state, Block.Block block, Transform player, ChunkManager cm)
        {
            PlayerEntity playerE = player.GetComponent<PlayerEntity>();
            ItemStack heldStack = playerE != null
                ? playerE.GetHeldItemStack()
                : ItemStack.Empty;

            if (heldStack != null && !heldStack.IsEmpty) return false;
            
            string facing = state.GetState(BlockStateKeys.DirectionalFacing);
            
            string newFacing = facing switch
            {
                "north" => DirectionalFacing.East,
                "east"  => DirectionalFacing.South,
                "south" => DirectionalFacing.West,
                "west"  => DirectionalFacing.Up,
                "up"    => DirectionalFacing.Down,
                "down"  => DirectionalFacing.North,
                _       => DirectionalFacing.North
            };

            cm.SetBlockState(position, BlockStateKeys.DirectionalFacing, newFacing);

            return true;
        }
    }
}
