using Core.Block;
using UnityEngine;

namespace Core.Blocks
{
    public class PoleBlock : Block.Block
    {
        public PoleBlock(byte id, string name, int top, int side, int bottom, int front = -1) : base(id, name, top, side, bottom, front)
        {
            isTransparent = true;
            isStrechy = true;
            AddState(BlockStateKeys.DirectionalFacing, DirectionalFacing.Up);
            AddState(BlockStateKeys.WidthState, 0.5f.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        }

        public override void OnPlaced(Vector3Int position, BlockStateContainer state, Transform player, Vector3Int? placementFace)
        {
            base.OnPlaced(position, state, player, placementFace);
            
            if (state == null)
                return;
            
            state.SetState(BlockStateKeys.DirectionalFacing, DirectionalFacing.Up);
            state.SetState(BlockStateKeys.WidthState, 0.5f.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        }

        public override bool OnActivated(Vector3Int position, BlockStateContainer state, Block.Block block, Transform player)
        {
            Debug.Log("Width: " + state.GetState(BlockStateKeys.WidthState));
            
            return false;
        }
    }
}