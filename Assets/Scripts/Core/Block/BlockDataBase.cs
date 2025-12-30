using UnityEngine;

namespace Core.Block
{
    public static class BlockDataBase
    {
        
        //Static block refrences
        public static Block GrassBlock;
        public static Block DirtBlock;
        public static Block StoneBlock;
        public static Block Sigma;
        public static Block ChestBlock;
        
        static BlockDataBase()
        {
            RegisterBlocks();
        }

        private static void RegisterBlocks()
        {
            // Grass block
            GrassBlock = new Block(1, "Grass_Block", top: 2, side: 1, bottom: 0);
            BlockRegistry.RegisterBlock(GrassBlock);

            // Dirt block
            DirtBlock = new Block(2, "Dirt_Block", top: 0, side: 0, bottom: 0);
            BlockRegistry.RegisterBlock(DirtBlock);
        
            //Stone Block
            StoneBlock = new Block(3, "Stone_Block", top: 3, side: 3, bottom: 3);
            BlockRegistry.RegisterBlock(StoneBlock);
        
            //Sigma Block 
            Sigma = new Block(4, "Sigma_Block", top: 4, side: 4, bottom: 4);
            BlockRegistry.RegisterBlock(Sigma);
            
            //Chest Block 
            ChestBlock = new Block(5, "Chest_Block", top: 5, side: 5, bottom: 5);
            BlockRegistry.RegisterBlock(ChestBlock);

            Debug.Log("Blocks registered (static)");
        }

        public static byte GetBlock(Block block) => block.id;

        public static void Init()
        {
        
        }
    }
}
