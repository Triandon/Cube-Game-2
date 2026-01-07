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
            ItemRegistry.RegisterItem(new Item(id:2,itemName:"Dirt_Item",isBlock:true,blockId:2,textureIndex:0,12));
        
            //Stone item
            ItemRegistry.RegisterItem(new Item(id:3,itemName:"Stone_Item",isBlock:true,blockId:3,textureIndex:3,999));
            
            //Chest Item
            ItemRegistry.RegisterItem(new Item(id:5, itemName:"Chest", isBlock:true, blockId:5, textureIndex:5,1));
            
            //Wood Item
            ItemRegistry.RegisterItem(new Item(id: 6, itemName:"Wood_Item", isBlock:true, blockId:6, textureIndex: 6, 64));
        }
    
        public static void Init(){}
    }
}
