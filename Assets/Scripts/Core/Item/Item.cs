using UnityEngine;

namespace Core.Item
{
    public class Item
    {
        public int id;
        public string itemName;
        public Texture2D icon;
        public bool isBlock;
        public byte blockId;

        public Sprite iconSprite;

        public int maxStackSize = 64;

        public Item(int id, string itemName, bool isBlock, byte blockId,Texture2D icon = null, int maxStackSize = 64)
        {
            this.id = id;
            this.itemName = itemName;
            this.isBlock = isBlock;
            this.blockId = blockId;
            this.icon = icon;
            this.maxStackSize = maxStackSize;

            if (icon != null)
            {
                iconSprite = Sprite.Create(icon, new Rect(0, 0, icon.width, icon.height), new Vector2(0.5f, 0.5f));
            }
        }
    }
}
