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
            isCentered = true;
            AddState(BlockStateKeys.WidthState, "0.5");
            AddState(BlockStateKeys.HeightState, "0.5");
            AddState(BlockStateKeys.DirectionalFacing, DirectionalFacing.North);
        }

        public override void OnPlaced(Vector3Int position, BlockStateContainer state, Transform player, Vector3Int? placementFace)
        {
            base.OnPlaced(position, state, player, placementFace);
            
            if (state == null)
                return;
            
            state.SetState(BlockStateKeys.WidthState, "0.5");
            state.SetState(BlockStateKeys.HeightState, "0.5");
        }

        public override bool OnActivated(Vector3Int position, BlockStateContainer state, Block.Block block, Transform player)
        {
            Debug.Log("Clicked with state: " + state.GetState(BlockStateKeys.DirectionalFacing));
            
            return false;
        }
    }
}