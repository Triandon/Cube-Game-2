namespace Core.Blocks
{
    public class TreeTrunkBlock : Block.Block
    {
        public TreeTrunkBlock(byte id, string name, int top, int side, int bottom, int front = -1) : base(id, name, top, side, bottom, front)
        {
            SetDefaultState(TileEntities.TreeTrunkTile.CreateTrunkState());
            isTransparent = true;
            isStrechy = true;
            isCentered = true;
        }
    }
}
