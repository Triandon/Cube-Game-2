using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.Block;
using UnityEngine;

namespace Core
{
    public static class WorldSaveSystem
    {
        private const int ChunkSaveVersion = 1;
        private const int SkyOcclusionSaveVersion = 1;
    
        private static string persistentDataPath;

        public static void Initialize(string applicationPersistentDataPath)
        {
            persistentDataPath = applicationPersistentDataPath;
        }

        private static string GetPersistentDataPath()
        {
            if (!string.IsNullOrEmpty(persistentDataPath))
                return persistentDataPath;

            persistentDataPath = Application.persistentDataPath;
            return persistentDataPath;
        }

        public static string GetChunkPath(Vector3Int coord)
        {
            return GetChunkDirectory() + $"/{coord.x}_{coord.y}_{coord.z}.chunk";
        }

        public static string GetChunkDirectory()
        {
            return GetPersistentDataPath() + "/chunks_2_system_test";
        }
        
        private static string GetSkyOcclusionPath()
        {
            return GetChunkDirectory() + "/sky_occlusion.dat";
        }

        public static void SaveSkyOcclusionMap(LightingSkyOcclusionMap map)
        {
            if (map == null)
                return;

            Directory.CreateDirectory(GetChunkDirectory());
            string path = GetSkyOcclusionPath();
            string temporaryPath = path + ".tmp";

            using (FileStream stream = File.Create(temporaryPath))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(SkyOcclusionSaveVersion);
                writer.Write(Chunk.CHUNK_SIZE);
                writer.Write(map.ChunkColumns.Count);
                foreach (KeyValuePair<Vector3Int, byte[]> entry in map.ChunkColumns)
                {
                    writer.Write(entry.Key.x);
                    writer.Write(entry.Key.y);
                    writer.Write(entry.Key.z);
                    writer.Write(entry.Value);
                }
            }

            if (File.Exists(path))
                File.Delete(path);
            File.Move(temporaryPath, path);
        }

        public static void LoadSkyOcclusionMap(LightingSkyOcclusionMap map)
        {
            if (map == null)
                return;

            string path = GetSkyOcclusionPath();
            if (!File.Exists(path))
                return;

            try
            {
                using FileStream stream = File.OpenRead(path);
                using BinaryReader reader = new BinaryReader(stream);
                int version = reader.ReadInt32();
                int chunkSize = reader.ReadInt32();
                int count = reader.ReadInt32();
                if (version != SkyOcclusionSaveVersion || chunkSize != Chunk.CHUNK_SIZE || count < 0)
                    throw new InvalidDataException("Sky occlusion header is invalid.");

                int columnCount = Chunk.CHUNK_SIZE * Chunk.CHUNK_SIZE;
                var entries = new List<KeyValuePair<Vector3Int, byte[]>>(count);
                for (int i = 0; i < count; i++)
                {
                    Vector3Int coord = new Vector3Int(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
                    byte[] columns = reader.ReadBytes(columnCount);
                    if (columns.Length != columnCount)
                        throw new EndOfStreamException("Sky occlusion data ended unexpectedly.");
                    entries.Add(new KeyValuePair<Vector3Int, byte[]>(coord, columns));
                }

                map.ReplaceWith(entries);
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException)
            {
                Debug.LogWarning($"Could not load sky occlusion data at {path}: {exception.Message}");
            }
        }

        public static bool ChunkSaveExist(Vector3Int coord)
        {
            string path = GetChunkPath(coord);
            if (File.Exists(path))
            {
                return true;
            }

            return false;

            // Old code, mabye usable later
            try
            {
                using FileStream stream = File.OpenRead(path);
                using BinaryReader reader = new BinaryReader(stream);

                return reader.ReadInt32() == ChunkSaveVersion &&
                       reader.ReadInt32() == Chunk.CHUNK_SIZE;

            }
            catch (IOException)
            {
                return false;
            }
        }

        public static void SaveChunk(Vector3Int coord, Chunk chunk)
        {
            Directory.CreateDirectory(GetChunkDirectory());

            using FileStream stream = File.Create(GetChunkPath(coord));
            using BinaryWriter writer = new BinaryWriter(stream);
            
            writer.Write(ChunkSaveVersion);
            writer.Write(Chunk.CHUNK_SIZE);

            List<RLEBlockRun> baseBlocks = EncodeRLE(chunk.blocks);
            writer.Write(baseBlocks.Count);
            foreach (RLEBlockRun run in baseBlocks)
            {
                writer.Write(run.id);
                writer.Write(run.count);
            }

            int S = Chunk.CHUNK_SIZE;
            
            List<SerializableBlockStateEntry> blockStates = new List<SerializableBlockStateEntry>();
            
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

                blockStates.Add(new SerializableBlockStateEntry
                {
                    index = index,
                    states = list
                });
            }

            writer.Write(blockStates.Count);
            foreach (SerializableBlockStateEntry entry in blockStates)
            {
                writer.Write(entry.index);
                writer.Write(entry.states.Count);

                foreach (SerializableBlockState state in entry.states)
                {
                    writer.Write(state.name ?? string.Empty);
                    writer.Write(state.value ?? string.Empty);
                }
            }
            
            Debug.Log("World saved successfully!");
        }

        public static bool LoadChunk(Vector3Int coord, Chunk chunk)
        {
            return LoadChunk(GetChunkPath(coord), coord, chunk);
        }

        public static bool LoadChunk(string path ,Vector3Int coord, Chunk chunk)
        {
            if (!File.Exists(path)) return false;

            using FileStream stream = File.OpenRead(path);
            using BinaryReader reader = new BinaryReader(stream);

            int version = reader.ReadInt32();
            int chunkSize = reader.ReadInt32();

            if (version != ChunkSaveVersion)
            {
                Debug.LogError($"Unsupported chunk save version {version} in chunk {coord} | File: {path}");
                return false;
            }

            if (chunkSize != Chunk.CHUNK_SIZE)
            {
                Debug.LogError(
                    $"Chunk save size mismatch in chunk {coord} | File: {path} | " +
                    $"save={chunkSize}, game={Chunk.CHUNK_SIZE}");
                return false;
            }

            int runCount = reader.ReadInt32();
            List<RLEBlockRun> baseBlocks = new List<RLEBlockRun>(runCount);

            for (int i = 0; i < runCount; i++)
            {
                baseBlocks.Add(new RLEBlockRun
                {
                    id = reader.ReadByte(),
                    count = reader.ReadInt32()
                });
            }

            int S = Chunk.CHUNK_SIZE;
            chunk.blocks = DecodeRLE(baseBlocks, coord, path);
            chunk.states = new BlockStateContainer[S, S, S];

            int blockStateCount = reader.ReadInt32();
            if (blockStateCount < 0)
            {
                Debug.LogError($"Invalid block state entry count {blockStateCount} in chunk {coord} | File: {path}");
                return false;
            }
            
            for (int i = 0; i < blockStateCount; i++)
            {
                int index = reader.ReadInt32();
                int stateCount = reader.ReadInt32();
                
                if (index < 0 || index >= S * S * S)
                {
                    Debug.LogError($"Invalid block state index {index} in chunk {coord} | File: {path}");
                    return false;
                }

                if (stateCount < 0)
                {
                    Debug.LogError($"Invalid state count {stateCount} in chunk {coord} | File: {path}");
                    return false;
                }
                
                Vector3Int pos = ChunkManager.IndexToPos(index);
                BlockStateContainer container = new BlockStateContainer();

                for (int s = 0; s < stateCount; s++)
                {
                    string name = reader.ReadString();
                    string value = reader.ReadString();
                    container.SetState(name, value);
                }

                chunk.states[pos.x, pos.y, pos.z] = container;
            }

            chunk.isDirty = false;
            return true;
        }

        public static string GetInventoryPath(string ownerName)
        {
            return GetPersistentDataPath() + $"/inventories/{ownerName}.inventory";
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
            
            Directory.CreateDirectory(GetPersistentDataPath() + "/inventories/");

            InventorySaveData data = new InventorySaveData();
            foreach (var stack in inventory.slots)
            {
                data.slots.Add(new SerializableItemStack
                {
                    itemId = stack.itemId,
                    count = stack.count,
                    displayName = stack.displayName,
                    composition = stack.composition != null ? stack.composition.Clone() : null
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
                inventory.slots[i].composition = savedStack.composition != null ? savedStack.composition.Clone() : null;
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
            return DecodeRLE(runs, coord, GetChunkPath(coord));
        }
        
        public static byte[,,] DecodeRLE(List<RLEBlockRun> runs, Vector3Int coord, string path)
        {
            int S = Chunk.CHUNK_SIZE;
            var blocks = new byte[S,S,S];
            int max = S * S * S;
            int index = 0;

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
