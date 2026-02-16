using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.Block;
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

        public static void SaveChunk(Vector3Int coord, Chunk chunk)
        {
            Directory.CreateDirectory(Application.persistentDataPath + "/chunks/");
            ChunkSaveDataNEW data = new ChunkSaveDataNEW()
            {
                baseBlocks = EncodeRLE(chunk.blocks)
            };

            int S = Chunk.CHUNK_SIZE;

            for (int x = 0; x < S; x++)
            for (int y = 0; y < S; y++)
            for (int z = 0; z < S; z++)
            {
                BlockStateContainer state = chunk.states[x, y, z];
                if (state == null || state.IsStateless())
                    continue;

                int index = ChunkManager.PosToIndex(x, y, z);
                var list = new List<SerializableBlockState>();

                foreach (var kv in state.GetAllStates)
                {
                    list.Add(new SerializableBlockState
                    {
                        name = kv.Key,
                        value = kv.Value.value
                    });
                }

                data.blockStates.Add(new SerializableBlockStateEntry
                {
                    index = index,
                    states = list
                });
            }

            string json = JsonUtility.ToJson(data);
            File.WriteAllText(GetChunkPath(coord), json);
            Debug.Log("World saved successfully!");
        }

        public static void LoadChunk(Vector3Int coord, Chunk chunk)
        {
            string path = GetChunkPath(coord);
            if (!File.Exists(path)) return;
            
            string json = File.ReadAllText(path);
            
            ChunkSaveDataNEW data = JsonUtility.FromJson<ChunkSaveDataNEW>(json);

            if (data.baseBlocks != null && data.baseBlocks.Count > 0)
            {
                int S = Chunk.CHUNK_SIZE;
                
                chunk.blocks = DecodeRLE(data.baseBlocks, coord);
                chunk.states = new BlockStateContainer[S, S, S];

                if (data.baseBlocks != null)
                {
                    foreach (var entry in data.blockStates)
                    {
                        Vector3Int pos = ChunkManager.IndexToPos(entry.index);
                        var container = new BlockStateContainer();

                        foreach (var s in entry.states)
                            container.SetState(s.name, s.value);

                        chunk.states[pos.x, pos.y, pos.z] = container;
                    }
                }

                chunk.isDirty = false;
                return;
            }

            SaveChunk(coord, chunk);
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
        
        public static List<RLEBlockRun> EncodeRLE(byte[,,] blocks)
        {
            int S = Chunk.CHUNK_SIZE;
            var runs = new List<RLEBlockRun>();

            byte current = blocks[0,0,0];
            int count = 0;

            for (int y = 0; y < S; y++)
            for (int z = 0; z < S; z++)
            for (int x = 0; x < S; x++)
            {
                byte id = blocks[x,y,z];

                if (id == current)
                {
                    count++;
                }
                else
                {
                    runs.Add(new RLEBlockRun { id = current, count = count });
                    current = id;
                    count = 1;
                }
            }

            runs.Add(new RLEBlockRun { id = current, count = count });
            return runs;
        }
        
        public static byte[,,] DecodeRLE(List<RLEBlockRun> runs, Vector3Int coord)
        {
            int S = Chunk.CHUNK_SIZE;
            var blocks = new byte[S,S,S];
            int max = S * S * S;
            int index = 0;

            string path = GetChunkPath(coord);

            foreach (var run in runs)
            {
                if (run.count <= 0)
                {
                    Debug.LogError(
                        $"Invalid RLE run count in chunk {coord} | File: {path}");
                    continue;
                }
                
                for (int i = 0; i < run.count; i++)
                {
                    if (index >= max)
                    {
                        Debug.LogError(
                            $"RLE overflow in chunk {coord} | File: {path}\n" +
                            $"index={index}, max={max}, runCount={run.count}");
                        return blocks;
                    }
                    
                    int x = index % S;
                    int z = (index / S) % S;
                    int y = index / (S * S);

                    blocks[x,y,z] = run.id;
                    index++;
                }
            }
            
            if (index != max)
            {
                Debug.LogWarning(
                    $"Decoded {index} blocks but expected {max} | Chunk {coord} | File: {path}"
                );
            }
            
            return blocks;
        }


    }
}
