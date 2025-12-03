using System.Collections.Generic;
using System.IO;
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
    }
}
