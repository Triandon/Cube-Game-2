using UnityEngine;

namespace Core.Item
{
    public class Item
    {
        public int id;
        public string itemName;
        public bool isBlock;
        public byte blockId;
        public int textureIndex;
        public int maxStackSize;

        public Item(int id, string itemName, bool isBlock, byte blockId,int textureIndex, int maxStackSize)
        {
            this.id = id;
            this.itemName = itemName;
            this.isBlock = isBlock;
            this.blockId = blockId;
            this.textureIndex = textureIndex;
            this.maxStackSize = maxStackSize;
        }
    }
}
