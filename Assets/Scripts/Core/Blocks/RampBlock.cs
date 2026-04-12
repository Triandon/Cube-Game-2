using Core.Block;

namespace Core.Blocks
{
    public class RampBlock : Block.Block
    {
        public RampBlock(byte id, string name, int top, int side, int bottom, int front = -1) : base(id, name, top, side, bottom, front)
        {
            shapeIndex = (int)BlockShapes.Triangle;
            //AddState(BlockStateKeys.DirectionalFacing, DirectionalFacing.North);
        }
    }
}