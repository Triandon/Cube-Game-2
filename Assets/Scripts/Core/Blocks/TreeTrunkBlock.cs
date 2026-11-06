namespace Core.Blocks
{
    public class TreeTrunkBlock : Block.Block
    {
        public TreeTrunkBlock(byte id, string name, MaterialProperties material, int top, int side, int bottom, int front = -1, byte dropItemId = 0) : 
            base(id, name, material, top, side, bottom, front, dropItemId)
        {
            SetDefaultState(TileEntities.TreeTrunkTile.CreateTrunkState());
            isTransparent = true;
            isStrechy = true;
            isCentered = true;
        }
    }
}
