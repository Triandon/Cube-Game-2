using Core.Blocks;
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
        public static Block WoodBlock;
        public static Block SnowBlock;
        public static Block SandStoneBlock;
        public static Block DeadGrassBlock;
        public static Block CraftingTableBlock;
        public static Block CrusherBlock;
        public static Block SlabBlock;
        public static Block ScaffoldingBlock;
        public static Block PoleBlock;
        public static Block HumanHeadBlock;
        
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
            DirtBlock = new DirtBlock(2, "Dirt_Block", top: 0, side: 0, bottom: 0);
            BlockRegistry.RegisterBlock(DirtBlock);
        
            //Stone Block
            StoneBlock = new Block(3, "Stone_Block", top: 3, side: 3, bottom: 3);
            BlockRegistry.RegisterBlock(StoneBlock);
        
            //Sigma Block 
            Sigma = new Block(4, "Sigma_Block", top: 16, side: 16, bottom: 16);
            BlockRegistry.RegisterBlock(Sigma);
            
            //Chest Block 
            ChestBlock = new ChestBlock(5, "Chest_Block", top: 4, side: 4, bottom: 4);
            ChestBlock.frontIndex = 5;
            BlockRegistry.RegisterBlock(ChestBlock);
            
            //Wood Block
            WoodBlock = new Block(6, "Wood_Block", top: 6, side: 6, bottom: 6);
            BlockRegistry.RegisterBlock(WoodBlock);
            
            //Snow block
            SnowBlock = new Block(7, "Snow_Block", top: 8, side: 8, bottom: 8);
            BlockRegistry.RegisterBlock(SnowBlock);
            
            //SandStone Block
            SandStoneBlock = new Block(8, "SandStone_Block", top: 7, side: 7, bottom: 7);
            BlockRegistry.RegisterBlock(SandStoneBlock);
            
            //DeadGrass Block
            DeadGrassBlock = new Block(9, "DeadGrass_Block", top: 9, side: 15, bottom: 0);
            BlockRegistry.RegisterBlock(DeadGrassBlock);
            
            //Crafting table block
            CraftingTableBlock = new CraftingTableBlock(10, "CraftingTable_Block", top: 11, side: 10, bottom: 11);
            BlockRegistry.RegisterBlock(CraftingTableBlock);
            
            // Crusher
            CrusherBlock = new CrusherBlock(11, "CrusherBlock", top: 13, side: 12, bottom: 12);
            BlockRegistry.RegisterBlock(CrusherBlock);
            
            // SlabBlock
            // Slab block (fixed half-height slab with orientation)
            SlabBlock = new SlabBlock(12, "SlabBlock", top: 12, side: 12, bottom: 12);
            BlockRegistry.RegisterBlock(SlabBlock);
            
            // ScaffoldingBlock
            ScaffoldingBlock = new ScaffoldingBlock(13, "ScaffoldingBlock", top: 14, side: 14, bottom: 14);
            BlockRegistry.RegisterBlock(ScaffoldingBlock);
            
            // Pole block
            PoleBlock = new PoleBlock(14, "PoleBlock", top: 16, side: 16, bottom: 16);
            BlockRegistry.RegisterBlock(PoleBlock);
            
            // Human Head block
            HumanHeadBlock = new HumanHeadBlock(15, "HumanHeadBlock", top: 18, side: 19, bottom: 20);
            HumanHeadBlock.frontIndex = 17;
            BlockRegistry.RegisterBlock(HumanHeadBlock);
            
            Debug.Log("Blocks registered (static)");
        }

        public static byte GetBlock(Block block) => block.id;

        public static void Init()
        {
        
        }
    }
}
