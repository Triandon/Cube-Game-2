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
            Texture2D grassTex = Resources.Load<Texture2D>("Textures/Dirt 1");
            ItemRegistry.RegisterItem(new Item(id:1,itemName:"Grass_Item",isBlock:true,blockId:1,grassTex));
        
            //Dirt Item
            Texture2D dirtTex = Resources.Load<Texture2D>("Textures/FirtilSoil");
            ItemRegistry.RegisterItem(new Item(id:2,itemName:"Dirt_Item",isBlock:true,blockId:2,dirtTex));
        
            //Stone item
            Texture2D stoneTex = Resources.Load<Texture2D>("Textures/SteinTekstur");
            ItemRegistry.RegisterItem(new Item(id:3,itemName:"Stone_Item",isBlock:true,blockId:3,stoneTex));
        }
    
        public static void Init(){}
    }
}
