using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core.Block;
using UnityEngine;

namespace Core
{
    
    //TO Chat gbt tommorow.
    //Give him all classes
    //Say yes you can re write my chunk and chunk manager. And can you maek it simmilar to the old code?
    //Yeah remove legacy code
    //After that we can go to the meshGenerating.
    
    public class ChunkManager : MonoBehaviour
    {

        public GameObject chunkPrefab;
        public Transform player;
        public int viewDistance;

        private Dictionary<Vector3Int, Chunk> chunks = new Dictionary<Vector3Int, Chunk>();
        private Vector3Int playerChunkCord;
        public int chunkCount;

        public HashSet<Chunk> meshQue = new HashSet<Chunk>();
        private Queue<Chunk> chunkPool = new Queue<Chunk>();

        //How many chunks should be building at once.
        //The amount that is currently building.
        public int chunksPerFrame = 4;
        private FPSCounter fpsCounter;
        private int fps;
        public bool dynamicChunkRendering = true;

        public int initialPoolSize = 20; // pre-instantiate this many chunks

        private ThreadedChunkWorker threadedWorker;

        private void Awake()
        {
            fpsCounter = FindAnyObjectByType<FPSCounter>();
            Debug.Log("Save path: " + Application.persistentDataPath + "/chunks/");

            // Pre-create chunk pool
            for (int i = 0; i < initialPoolSize; i++)
            {
                GameObject go = Instantiate(chunkPrefab, Vector3.zero, Quaternion.identity, transform);
                go.SetActive(false);
                chunkPool.Enqueue(go.GetComponent<Chunk>());
            }
        }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            BlockRegistry.BuildThreadLookup();
            ThreadConstants.PrepareCommonBlockIDs();

            // start worker threads (use processorCount -1 or 1 minimum)
            threadedWorker = new ThreadedChunkWorker(Math.Max(1, SystemInfo.processorCount - 1));
            threadedWorker.Start();

            //Always spawns the first chunk at 0,0,0
            if (!chunks.ContainsKey(Vector3Int.zero))
            {
                chunkCount++;
                GenerateChunk(Vector3Int.zero, chunkCount);
            }

            UpdatePlayerChunkCoord();
            UpdateChunks();
        }


        // Update is called once per frame
        void Update()
        {

            Vector3Int currentPlayerChunk = GetPlayerChunkCoord();

            if (currentPlayerChunk != playerChunkCord)
            {
                playerChunkCord = currentPlayerChunk;
                UpdateChunks();
            }

            

        }

        // Get the chunk coordinate the player is currently inside
        private Vector3Int GetPlayerChunkCoord()
        {
            return new Vector3Int(
                Mathf.FloorToInt(player.position.x / Chunk.CHUNK_SIZE),
                Mathf.FloorToInt(player.position.y / Chunk.CHUNK_SIZE),
                Mathf.FloorToInt(player.position.z / Chunk.CHUNK_SIZE)
            );
        }

        private void UpdatePlayerChunkCoord()
        {
            playerChunkCord = GetPlayerChunkCoord();
        }

        private void GenerateWorld()
        {
            List<Chunk> newChunks = new List<Chunk>();

            for (int x = 0; x <= 4; x++)
            for (int y = 0; y <= 2; y++)
            for (int z = 0; z <= 4; z++)
            {
                Vector3Int chunkCord =
                    new Vector3Int(playerChunkCord.x + x, playerChunkCord.y + y, playerChunkCord.z + z);

                if (!chunks.ContainsKey(chunkCord))
                {
                    chunkCount++;
                    Chunk chunk = GenerateChunk(chunkCord, chunkCount);
                    newChunks.Add(chunk);
                }
            }
            
        }

        public void UpdateChunks()
        {
            HashSet<Vector3Int> neededChunks = new HashSet<Vector3Int>();

            //Determine whick chunks shoudl exist
            for (int x = -viewDistance; x <= viewDistance; x++)
            for (int y = -viewDistance; y <= viewDistance; y++)
            for (int z = -viewDistance; z <= viewDistance; z++)
            {
                Vector3Int logicalCoord = playerChunkCord + new Vector3Int(x, y, z);
                neededChunks.Add(logicalCoord);

                if (World.Instance.IsChunkInsideOfWorld(logicalCoord))
                {
                    if (!chunks.ContainsKey(logicalCoord))
                    {
                        chunkCount++;
                        GenerateChunk(logicalCoord, chunkCount);
                    }
                }

            }

            // Remove chunks no longer needed
            List<Vector3Int> chunksToRemove = new List<Vector3Int>();
            foreach (var kvp in chunks)
            {
                if (!neededChunks.Contains(kvp.Key))
                {
                    chunksToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in chunksToRemove)
            {
                Chunk chunk = chunks[key];
                RemoveChunk(chunk, key);
            }

        }

        private Chunk GenerateChunk(Vector3Int coord, int chunkNumber)
        {
            Chunk chunk;
            if (chunkPool.Count > 0)
            {
                chunk = chunkPool.Dequeue();
                chunk.gameObject.SetActive(true);
                chunk.coord = coord;
                chunk.chunkManager = this;
                chunk.name = "Chunk_" + coord.x + "_" + coord.y + "_" + coord.z + "_chunk_nr" + chunkNumber;

                //Reset old data
                chunk.blocks = new byte[Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE];
                chunk.changedBlocks.Clear();
                chunk.isDirty = false;
            }
            else
            {
                GameObject gameObject = Instantiate(chunkPrefab, Vector3.zero, Quaternion.identity, transform);
                chunk = gameObject.GetComponent<Chunk>();
                chunk.coord = coord;
                chunk.chunkManager = this;
                chunk.name = "Chunk_" + coord.x + "_" + coord.y + "_" + coord.z + "_chunk_nr" + chunkNumber;
            }

            Vector3Int worldPos = new Vector3Int(coord.x * Chunk.CHUNK_SIZE, coord.y * Chunk.CHUNK_SIZE,
                coord.z * Chunk.CHUNK_SIZE);
            chunk.transform.position = worldPos;

            chunks.Add(coord, chunk);

            // Load saved changes on main thread
            Dictionary<int, byte> savedChanges = null;
            if (WorldSaveSystem.ChunkSaveExist(coord))
            {
                savedChanges = WorldSaveSystem.LoadChunk(coord);
                // do NOT apply saved changes to chunk.blocks here; worker will merge them
                // but keep changedBlocks to reflect that the chunk has saved modifications
                foreach (var kv in savedChanges)
                {
                    chunk.changedBlocks[kv.Key] = kv.Value;
                }

                if (chunk.changedBlocks.Count > 0) chunk.isDirty = true;
            }

            // Build padded blocks (center maybe empty — worker will generate center blocks or use padded center)
            byte[,,] padded = BuildPaddedBlocks(coord);

            // Create request with padded array and saved changes
            var req = new ChunkGenRequest(coord, savedChanges, padded);
            threadedWorker.EnqueueRequest(req);
            

            return chunk;
        }


        private void RemoveChunk(Chunk chunk, Vector3Int coord)
        {
            if (chunk.isDirty)
            {
                WorldSaveSystem.SaveChunk(coord, chunk.changedBlocks);
                chunk.isDirty = false;
            }

            // Reset chunk state before returning to pool
            chunk.blocks = new byte[Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE];
            chunk.changedBlocks.Clear();
            chunk.name = $"Chunk_{coord.x}_{coord.y}_{coord.z}_chunk_nr{chunkCount}";

            // return to pool
            chunk.gameObject.SetActive(false);
            chunkPool.Enqueue(chunk);

            meshQue.Remove(chunk);
            chunks.Remove(coord);
            chunkCount--;
        }

        public void TrimUnusedChunks()
        {
            List<Vector3Int> chunksToRemove = new List<Vector3Int>();

            foreach (var kvp in chunks)
            {
                Chunk chunk = kvp.Value;
                Vector3Int coord = kvp.Key;

                // Calculate distance from player chunk
                int distanceX = Mathf.Abs(coord.x - playerChunkCord.x);
                int distanceY = Mathf.Abs(coord.y - playerChunkCord.y);
                int distanceZ = Mathf.Abs(coord.z - playerChunkCord.z);

                if (distanceX > viewDistance || distanceY > viewDistance || distanceZ > viewDistance)
                {
                    if (chunk.isDirty)
                    {
                        // Save changes before removing
                        WorldSaveSystem.SaveChunk(coord, chunk.changedBlocks);
                        Destroy(chunk.gameObject);
                    }
                    else
                    {
                        Destroy(chunk.gameObject);
                    }

                    meshQue.Remove(chunk);
                    // Chunk is outside the new view distance
                    chunksToRemove.Add(coord);
                }
            }

            // Remove from dictionary
            foreach (var key in chunksToRemove)
            {
                chunks.Remove(key);
                chunkCount--;
            }
        }
        

        public Chunk GetChunk(Vector3Int coord)
        {
            chunks.TryGetValue(coord, out var c);
            return c;
        }

        private List<Vector3Int> GetNeighborCoords(Vector3Int coord)
        {
            return new List<Vector3Int>
            {
                coord + Vector3Int.right,
                coord + Vector3Int.left,
                coord + Vector3Int.up,
                coord + Vector3Int.down,
                coord + new Vector3Int(0, 0, 1),
                coord + new Vector3Int(0, 0, -1)
            };
        }

        public Chunk GetChunkFromWorldPos(Vector3Int worldPos)
        {
            Vector3Int chunkCord = new Vector3Int(
                Mathf.FloorToInt((float)worldPos.x / Chunk.CHUNK_SIZE),
                Mathf.FloorToInt((float)worldPos.y / Chunk.CHUNK_SIZE),
                Mathf.FloorToInt((float)worldPos.z / Chunk.CHUNK_SIZE));

            chunks.TryGetValue(chunkCord, out Chunk chunk);
            return chunk;
        }

        public void SetBlockAtWorldPos(Vector3Int worldPos, byte id)
        {
            Chunk chunk = GetChunkFromWorldPos(worldPos);
            if (chunk == null)
            {
                return;
            }

            Vector3Int local = chunk.WorldToLocal(worldPos);

            // Bounds check
            if (local.x < 0 || local.x >= Chunk.CHUNK_SIZE ||
                local.y < 0 || local.y >= Chunk.CHUNK_SIZE ||
                local.z < 0 || local.z >= Chunk.CHUNK_SIZE)
            {
                // Block is outside this chunk, skip
                return;
            }

            chunk.SetBlockLocal(local, id);
        }

        public byte GetBlockAtWorldPos(Vector3Int worldPos)
        {
            Chunk chunk = GetChunkFromWorldPos(worldPos);
            if (chunk == null)
            {
                return 0;
            }

            Vector3Int local = chunk.WorldToLocal(worldPos);

            // Bounds check
            if (local.x < 0 || local.x >= Chunk.CHUNK_SIZE ||
                local.y < 0 || local.y >= Chunk.CHUNK_SIZE ||
                local.z < 0 || local.z >= Chunk.CHUNK_SIZE)
            {
                // Block is outside this chunk, skip
                return 0;
            }

            return chunk.blocks[local.x, local.y, local.z];
        }
        

        public void SaveWorld()
        {
            foreach (var kvp in chunks)
            {
                Chunk chunk = kvp.Value;
                if (chunk.isDirty)
                {
                    WorldSaveSystem.SaveChunk(chunk.coord, chunk.changedBlocks);
                    chunk.isDirty = false;
                }
            }

            Debug.Log("World saved successfully!");
        }

        // inside ChunkManager class
        private byte[,,] BuildPaddedBlocks(Vector3Int coord)
        {
            int S = Chunk.CHUNK_SIZE;
            byte[,,] padded = new byte[S + 2, S + 2, S + 2];

            // Loop normal chunk coords but offset by +1
            for (int ox = -1; ox <= 1; ox++)
            for (int oy = -1; oy <= 1; oy++)
            for (int oz = -1; oz <= 1; oz++)
            {
                Vector3Int nc = coord + new Vector3Int(ox, oy, oz);

                if (!chunks.TryGetValue(nc, out Chunk neighbor))
                    continue;

                byte[,,] src = neighbor.blocks;

                int dstX = (ox + 1) * S; // maps -1 → 0, 0 → S, +1 → 2S
                int dstY = (oy + 1) * S;
                int dstZ = (oz + 1) * S;

                for (int x = 0; x < S; x++)
                for (int y = 0; y < S; y++)
                for (int z = 0; z < S; z++)
                {
                    padded[dstX + x, dstY + y, dstZ + z] = src[x, y, z];
                }
            }

            // compact to 1..S in every axis
            byte[,,] compact = new byte[S + 2, S + 2, S + 2];

            for (int x = 0; x < S; x++)
            for (int y = 0; y < S; y++)
            for (int z = 0; z < S; z++)
            {
                compact[x + 1, y + 1, z + 1] = padded[S + x, S + y, S + z];
            }

            // Fill X−1
            for (int y = 0; y < S; y++)
            for (int z = 0; z < S; z++)
                compact[0, y + 1, z + 1] = padded[0 + y * 0, S + y, S + z];

            // Fill X+1
            for (int y = 0; y < S; y++)
            for (int z = 0; z < S; z++)
                compact[S + 1, y + 1, z + 1] = padded[2 * S + y * 0, S + y, S + z];

            // Fill Y and Z similarly (worker only needs ±1 layer)
            // but air is fine because padded missing neighbors = air anyway

            return compact;
        }


    }
}
