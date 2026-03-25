using Core.Block;
using UnityEngine;

namespace Core.Blocks
{
    public class HumanHeadBlock : Block.Block
    {
        public HumanHeadBlock(byte id, string name, int top, int side, int bottom, int front = -1) : base(id, name, top, side, bottom, front)
        {
            isTransparent = true;
            isStrechy = true;
            AddState(BlockStateKeys.WidthState, "0.5");
            AddState(BlockStateKeys.HeightState, "0.5");
            AddState(BlockStateKeys.DirectionalFacing, DirectionalFacing.Up);
        }

        public override void OnPlaced(Vector3Int position, BlockStateContainer state, Transform player, Vector3Int? placementFace)
        {
            base.OnPlaced(position, state, player, placementFace);
            
            Vector3 forward = -player.transform.forward;

            string facing;

            if (Mathf.Abs(forward.x) > Mathf.Abs(forward.z))
                facing = forward.x > 0 ? DirectionalFacing.East : DirectionalFacing.West;
            else
                facing = forward.z > 0 ? DirectionalFacing.North : DirectionalFacing.Sought;
        
            state.SetState("facing", facing);
            state.SetState(BlockStateKeys.WidthState, "0.5");
            state.SetState(BlockStateKeys.HeightState, "0.5");
            //state.SetState(BlockStateKeys.DirectionalFacing, DirectionalFacing.Up);
        }
    }
}