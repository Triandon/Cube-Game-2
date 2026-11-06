namespace Core.Blocks
{
    public class TreeLeaf : Block.Block
    {
        public TreeLeaf(byte id, string name, MaterialProperties material, int top, int side, int bottom, int front = -1) : 
            base(id, name, material, top, side, bottom, front)
        {
            
        }
    }
}
