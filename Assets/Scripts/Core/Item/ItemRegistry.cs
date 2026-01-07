using System.Collections.Generic;
using UnityEngine;

namespace Core.Item
{
    public static class ItemRegistry
    {
        private static Dictionary<int, Item> itemsById = new Dictionary<int, Item>();
        private static Dictionary<string, Item> itemsByName = new Dictionary<string, Item>();
        
        //Textures
        private static Texture2D atlasTexture;
        private static int atlasSize = 256;
        private static int tileSize = 16;
        private static Dictionary<int, Sprite> spirteIndex = new Dictionary<int, Sprite>();

        public static void RegisterItem(Item item)
        {
            if (!itemsById.ContainsKey(item.id))
            {
                itemsById.Add(item.id, item);
                itemsByName.Add(item.itemName, item);

                Debug.Log($"Registered item: {item.itemName} (ID: {item.id})");
            }
            else
            {
                Debug.LogWarning($"Item ID {item.id} already registered!");
            }
        }

        public static Item GetItem(int id)
        {
            itemsById.TryGetValue(id, out Item item);
            return item;
        }

        public static Item GetItem(string name)
        {
            itemsByName.TryGetValue(name, out Item item);
            return item;
        }
        
        // Get all registered items!
        public static IReadOnlyCollection<Item> getAllItems()
        {
            return itemsById.Values;
        }
        
        public static void InitAtlas(Material atlasMaterial, int atlasSize, int tileSize)
        {
            atlasTexture = atlasMaterial.mainTexture as Texture2D;
            ItemRegistry.atlasSize = atlasSize;
            ItemRegistry.tileSize = tileSize;

            spirteIndex.Clear();

            Debug.Log("ItemRegistry: Atlas initialized");
        }

        public static Sprite GetSprite(int textureIndex)
        {
            if (atlasTexture == null || textureIndex < 0) return null;

            if (spirteIndex.TryGetValue(textureIndex, out var sprite)) return sprite;
            
            int tilesPerRow = atlasSize / tileSize;

            int x = textureIndex % tilesPerRow;
            int y = textureIndex / tilesPerRow;
            
            Rect rect = new Rect(
                x * tileSize,
                atlasSize - tileSize - y * tileSize,
                tileSize,
                tileSize
            );

            sprite = Sprite.Create(
                atlasTexture,
                rect,
                new Vector2(0.5f, 0.5f),
                tileSize
            );

            spirteIndex[textureIndex] = sprite;
            return sprite;
        }

        public static Sprite GetItemSprite(int itemId)
        {
            var item = GetItem(itemId);
            if (item == null || item.textureIndex < 0) return null;
            return GetSprite(item.textureIndex);
        }
    }
}