using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Core
{
    public static class WorldSaveSystem
    {
    
        public static string GetChunkPath(Vector3Int coord)
        {
            return Application.persistentDataPath + $"/chunks/{coord.x}_{coord.y}_{coord.z}.chunk";
        }

        public static bool ChunkSaveExist(Vector3Int coord)
        {
            return File.Exists(GetChunkPath(coord));
        }

        public static void SaveChunk(Vector3Int coord, Dictionary<int,byte> changedBlocks)
        {
            Directory.CreateDirectory(Application.persistentDataPath + "/chunks/");

            ChunkSaveData data = new ChunkSaveData();
            foreach (var kv in changedBlocks)
            {
                data.changedBlocks.Add(new SerializableBlockChange { index = kv.Key, id = kv.Value });
            }

            string json = JsonUtility.ToJson(data);
            File.WriteAllText(GetChunkPath(coord), json);
            Debug.Log("World saved successfully!");
        }

        public static Dictionary<int,byte> LoadChunk(Vector3Int coord)
        {
            string json = File.ReadAllText(GetChunkPath(coord));
            ChunkSaveData data = JsonUtility.FromJson<ChunkSaveData>(json);
        
            Dictionary<int, byte> dict = new Dictionary<int, byte>();
            foreach (var change in data.changedBlocks)
                dict[change.index] = change.id;
        
            return dict;
        }

        public static string GetInventoryPath(string ownerName)
        {
            return Application.persistentDataPath + $"/inventories/{ownerName}.inventory";
        }
        
        public static void SaveInventory(string ownerName, Inventory inventory)
        {
            if (string.IsNullOrEmpty(ownerName))
            {
                const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
                ownerName = new string(
                    System.Linq.Enumerable.Repeat(chars, 8)
                        .Select(s => s[UnityEngine.Random.Range(0, s.Length)]).ToArray()
                );
            }
            
            Directory.CreateDirectory(Application.persistentDataPath + "/inventories/");

            InventorySaveData data = new InventorySaveData();
            foreach (var stack in inventory.slots)
            {
                data.slots.Add(new SerializableItemStack
                {
                    itemId = stack.itemId,
                    count = stack.count,
                    displayName = stack.displayName
                });
            }

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(GetInventoryPath(ownerName), json);
            Debug.Log($"Inventory for {ownerName} saved!");
        }
        
        public static void LoadInventory(string ownerName, Inventory inventory)
        {
            string path = GetInventoryPath(ownerName);
            if (!File.Exists(path)) return;

            string json = File.ReadAllText(path);
            InventorySaveData data = JsonUtility.FromJson<InventorySaveData>(json);

            for (int i = 0; i < data.slots.Count && i < inventory.slots.Length; i++)
            {
                var savedStack = data.slots[i];
                inventory.slots[i].itemId = savedStack.itemId;
                inventory.slots[i].count = savedStack.count;
                inventory.slots[i].displayName = savedStack.displayName;
            }

            inventory.InventoryChanged();
            Debug.Log($"Inventory for {ownerName} loaded!");
        }
    }
}
