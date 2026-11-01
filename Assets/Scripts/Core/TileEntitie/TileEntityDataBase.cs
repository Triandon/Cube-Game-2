using Core.Block;

namespace Core.TileEntities
{
    public static class TileEntityDataBase
    {
        public static readonly TileEntityType<SapplingTile> SapplingTile;
        public static readonly TileEntityType<Chest2Tile> Chest2;
        public static readonly TileEntityType<TreeTrunkTile> TreeTunkTile;

        static TileEntityDataBase()
        {
            SapplingTile = TileEntityRegistry.Register(
                "SapplingTile", () => new SapplingTile(), BlockDataBase.Sigma);

            Chest2 = TileEntityRegistry.Register(
                "chest2", () => new Chest2Tile(), BlockDataBase.Chest2);

            TreeTunkTile = TileEntityRegistry.Register(
                "tree_trunk", () => new TreeTrunkTile(), BlockDataBase.TreeTrunk);
        }
        
        public static void Init() { }
        
        // Later make it more simmilar to block Data base
    }
}
