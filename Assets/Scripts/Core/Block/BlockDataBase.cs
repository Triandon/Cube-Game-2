using UnityEngine;

namespace Core.Block
{
    public static class BlockDataBase
    {
        static BlockDataBase()
        {
            RegisterBlocks();
        }

        private static void RegisterBlocks()
        {
            // Grass block
            BlockRegistry.RegisterBlock(new Block(1, "Grass_Block", top:2, side:1, bottom:0));

            // Dirt block
            BlockRegistry.RegisterBlock(new Block(2, "Dirt_Block", top:0, side:0, bottom:0));
        
            //Stone Block
            BlockRegistry.RegisterBlock(new Block(3, "Stone_Block", top:3, side:3, bottom:3));
        
            //Sigma Block
            BlockRegistry.RegisterBlock(new Block(4,"Sigma_Block",top:4,side:4,bottom:4));

            Debug.Log("Blocks registered (static)");
        }

        public static void Init()
        {
        
        }
    }
}
