namespace Core.Blocks
{
    public class CornerRampBlock : Block.Block
    {
        public CornerRampBlock(byte id, string name, int top, int side, int bottom, int front = -1) : base(id, name, top, side, bottom, front)
        {
        }
    }
}