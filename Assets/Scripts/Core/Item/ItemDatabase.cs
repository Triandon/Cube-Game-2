using UnityEngine;

namespace Core.Item
{
    public static class ItemDatabase
    {
        static ItemDatabase()
        {
            RegisterItems();
        }

        private static void RegisterItems()
        {
            //Grass item
            ItemRegistry.RegisterItem(new Item(id:1,itemName:"Grass_Item",isBlock:true,blockId:1,textureIndex:1,64));
        
            //Dirt Item
            ItemRegistry.RegisterItem(new Item(id:2,itemName:"Dirt_Item",isBlock:true,blockId:2,textureIndex:0,64));
        
            //Stone item
            ItemRegistry.RegisterItem(new Item(id:3,itemName:"Stone_Item",isBlock:true,blockId:3,textureIndex:3,120));
            
            //Chest Item
            ItemRegistry.RegisterItem(new Item(id:5, itemName:"Chest", isBlock:true, blockId:5, textureIndex:5,1));
            
            //Wood Item
            ItemRegistry.RegisterItem(new Item(id: 6, itemName:"Wood_Item", isBlock:true, blockId:6, textureIndex: 6, 64));
            
            //Snow Item
            ItemRegistry.RegisterItem(new Item(id: 7, itemName:"SnowBlock_Item", isBlock:true, blockId:7, textureIndex: 8, 64));
            
            //Sandstone Item
            ItemRegistry.RegisterItem(new Item(id: 8, itemName:"SandStoneBlock_Item", isBlock:true, blockId:8, textureIndex: 7, 64));
            
            //DeadGrass Item
            ItemRegistry.RegisterItem(new Item(id: 9, itemName:"DeadGrassBlock_Item", isBlock:true, blockId:9, textureIndex: 9, 64));
        }
    
        public static void Init(){}
    }
}
