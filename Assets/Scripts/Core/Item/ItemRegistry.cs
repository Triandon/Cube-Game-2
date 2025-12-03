using System.Collections.Generic;
using UnityEngine;

namespace Core.Item
{
    public static class ItemRegistry
    {
        private static Dictionary<int, Item> itemsById = new Dictionary<int, Item>();
        private static Dictionary<string, Item> itemsByName = new Dictionary<string, Item>();

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
    }
}