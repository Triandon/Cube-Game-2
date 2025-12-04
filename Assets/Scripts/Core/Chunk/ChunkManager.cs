using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core.Block;
using UnityEngine;

namespace Core
{
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

        // How many chunks should be building at once.
        public int chunksPerFrame = 4;
        private FPSCounter fpsCounter;
        private int fps;
        public bool dynamicChunkRendering = true;

        public int initialPoolSize = 20; // pre-instantiate this many chunks

        private ThreadedChunkWorker threadedWorker;

        // --- new: track pending requests so we don't enqueue duplicates
        private HashSet<Vector3Int> pendingRequests = new HashSet<Vector3Int>();
        private HashSet<Vector3Int> generationQue = new HashSet<Vector3Int>();

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

        void Start()
        {
            BlockRegistry.BuildThreadLookup();
            ThreadConstants.PrepareCommonBlockIDs();

            // start worker threads (use processorCount -1 or 1 minimum)
            threadedWorker = new ThreadedChunkWorker(Math.Max(1, SystemInfo.processorCount - 1));
            threadedWorker.Start();

            // Always spawn first chunk at 0,0,0 if missing
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
            UpdateFPS();

            // Pull worker results onto the main thread immediately
            ProcessWorkerResults();

            Vector3Int currentPlayerChunk = GetPlayerChunkCoord();
            if (currentPlayerChunk != playerChunkCord)
            {
                playerChunkCord = currentPlayerChunk;
                UpdateChunks();
            }
            // Remove null/destroyed chunks first
            meshQue.RemoveWhere(c => c == null || c.gameObject == null);
            
            int buildChunksThisFrame = Mathf.Min(chunksPerFrame, meshQue.Count);
            int generatingChunksThisFrame = Math.Min(chunksPerFrame, generationQue.Count);
            
            //Generate list limeted number
            var toGenerate = generationQue
                .OrderBy(c => Vector3.Distance(player.position, c * Chunk.CHUNK_SIZE))
                .Take(generatingChunksThisFrame)
                .ToList();
            
            // Before generating chunks in Update()
            generationQue.RemoveWhere(coord => 
                Mathf.Abs(coord.x - playerChunkCord.x) > viewDistance ||
                Mathf.Abs(coord.y - playerChunkCord.y) > viewDistance ||
                Mathf.Abs(coord.z - playerChunkCord.z) > viewDistance);
            
            meshQue.RemoveWhere(c => 
                Mathf.Abs(c.coord.x - playerChunkCord.x) > viewDistance ||
                Mathf.Abs(c.coord.y - playerChunkCord.y) > viewDistance ||
                Mathf.Abs(c.coord.z - playerChunkCord.z) > viewDistance);


            foreach (var coord in toGenerate)
            {
                generationQue.Remove(coord);
                chunkCount++;
                GenerateChunk(coord, chunkCount);
            }

            // Build meshes from meshQue (distance prioritized)
            if (meshQue.Count > 0 && chunksPerFrame > 0)
            {
                List<Chunk> sortedChunks = meshQue
                    .OrderBy(c => Vector3.Distance(player.position, c.transform.position))
                    .ToList();

                // Build closest chunks first
                for (int i = 0; i < buildChunksThisFrame; i++)
                {
                    Chunk chunkToBuild = sortedChunks[i];
                    if (chunkToBuild != null && chunkToBuild.gameObject != null &&
                        chunkToBuild.gameObject.activeInHierarchy)
                    {
                        StartCoroutine(BuildChunkMeshNextFrame(chunkToBuild));
                    }

                    // Remove from queue regardless (we're attempting to build it)
                    meshQue.Remove(chunkToBuild);
                }
            }
        }

        private void OnDestroy()
        {
            if (threadedWorker != null)
            {
                try
                {
                    threadedWorker.Stop();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Error stopping threadedWorker: {e}");
                }
            }
        }

        private void OnApplicationQuit()
        {
            OnDestroy();
        }

        // Called each Update to dequeue all ready worker results
        private void ProcessWorkerResults()
        {
            if (threadedWorker == null) return;
            ChunkGenResult res;
            while (threadedWorker.TryDequeueResult(out res))
            {
                ApplyChunkResult(res);
            }
        }

        private void ApplyChunkResult(ChunkGenResult res)
        {
            if (res == null) return;

            // If chunk was removed while worker was working, ignore
            if (!chunks.TryGetValue(res.coord, out Chunk chunk))
            {
                // clear pendingRequests entry in case it still exists
                pendingRequests.Remove(res.coord);
                return;
            }

            // Apply block data
            chunk.blocks = res.blocks ?? new byte[Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE];

            // If chunk has stored changes, it remains dirty
            chunk.isDirty = chunk.changedBlocks.Count > 0;

            // Apply the worker mesh data to the chunk's ChunkRendering (main thread only)
            var chunkRender = chunk.GetComponent<ChunkRendering>();
            if (chunkRender != null && res.meshData != null)
            {
                try
                {
                    chunkRender.ApplyMeshData(res.meshData);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"ChunkManager: ApplyMeshData exception for {res.coord}: {e}");
                    // fallback: schedule chunk for main-thread mesh generation (older path)
                    meshQue.Add(chunk);
                }
            }
            else
            {
                // No renderer or no meshdata -> schedule for main-thread meshing
                meshQue.Add(chunk);
            }

            EnqueueChunkBorderUpdates(chunk.coord);
            
            // Remove from pending requests set so future generates are allowed
            pendingRequests.Remove(res.coord);
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

        // Generate some small local area (kept for compatibility if you want to call it)
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

            // Determine which chunks should exist
            for (int x = -viewDistance; x <= viewDistance; x++)
            for (int y = -viewDistance; y <= viewDistance+3; y++)
            for (int z = -viewDistance; z <= viewDistance; z++)
            {
                Vector3Int logicalCoord = playerChunkCord + new Vector3Int(x, y, z);
                
                // Compute distance from player chunk
                if (Mathf.Abs(x) > viewDistance || Mathf.Abs(y) > viewDistance || Mathf.Abs(z) > viewDistance)
                    continue; // skip coordinates outside view distance
                
                neededChunks.Add(logicalCoord);

                if (World.Instance.IsChunkInsideOfWorld(logicalCoord))
                {
                    if (!chunks.ContainsKey(logicalCoord) && !pendingRequests.Contains(logicalCoord) && !generationQue.Contains(logicalCoord))
                    {
                        generationQue.Add(logicalCoord);
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

                // Reset old data
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

            // Mark request pending and enqueue
            pendingRequests.Add(coord);
            threadedWorker.EnqueueRequest(req);

            if (threadedWorker == null)
            {
                chunk.GenerateHeightMapData();
                meshQue.Add(chunk);
            }
            
            EnqueueChunkBorderUpdates(coord);

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
            generationQue.Remove(coord);

            // Make sure to remove any pending request marker
            pendingRequests.Remove(coord);

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
                    generationQue.Remove(coord);
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
            if (chunk == null) return;

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
            
            // Enqueue neighbors if block is on border
            if (local.x == 0 || local.x == Chunk.CHUNK_SIZE - 1 ||
                local.y == 0 || local.y == Chunk.CHUNK_SIZE - 1 ||
                local.z == 0 || local.z == Chunk.CHUNK_SIZE - 1)
            {
                EnqueueNeighborUpdates(chunk.coord, local);
            }
        }

        public byte GetBlockAtWorldPos(Vector3Int worldPos)
        {
            Chunk chunk = GetChunkFromWorldPos(worldPos);
            if (chunk == null) return 0;

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

        private void UpdateFPS()
        {
            if (fpsCounter == null) return;
            fps = fpsCounter.CurrentFPS;

            if (!dynamicChunkRendering) return;

            // Dynamic scaling (with clamping)
            if (fps > 110)
                chunksPerFrame = 14;
            else if (fps > 80)
                chunksPerFrame = 10;
            else if (fps > 60)
                chunksPerFrame = 8;
            else if (fps > 40)
                chunksPerFrame = 6;
            else if (fps > 25)
                chunksPerFrame = 4;
            else if (fps > 15)
                chunksPerFrame = 2;
            else
                chunksPerFrame = 1;

            if (meshQue.Count <= 0 && generationQue.Count <= 0)
            {
                chunksPerFrame = 0;
            }
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

        // Build padded blocks to send to worker. padded size = [S+2,S+2,S+2] with center at [1..S]
        public byte[,,] BuildPaddedBlocks(Vector3Int coord)
        {
            int S = Chunk.CHUNK_SIZE;
            int P = S + 2;
            byte[,,] padded = new byte[P, P, P];

            // For offsets -1..+1 in x,y,z
            for (int ox = -1; ox <= 1; ox++)
            for (int oy = -1; oy <= 1; oy++)
            for (int oz = -1; oz <= 1; oz++)
            {
                Vector3Int neighborCoord = coord + new Vector3Int(ox, oy, oz);
                if (!chunks.TryGetValue(neighborCoord, out Chunk neighbor) || neighbor.blocks == null)
                    continue;

                byte[,,] src = neighbor.blocks;

                // Destination origin in the large 3xS grid: (-1->0, 0->S, +1->2S)
                int baseDstX = (ox + 1) * S;
                int baseDstY = (oy + 1) * S;
                int baseDstZ = (oz + 1) * S;

                for (int x = 0; x < S; x++)
                for (int y = 0; y < S; y++)
                for (int z = 0; z < S; z++)
                {
                    int bigX = baseDstX + x;
                    int bigY = baseDstY + y;
                    int bigZ = baseDstZ + z;

                    // Map the 3xS^3 -> compact [1..S] by subtracting S and adding 1
                    int cx = bigX - S + 1;
                    int cy = bigY - S + 1;
                    int cz = bigZ - S + 1;

                    if (cx >= 0 && cx < P && cy >= 0 && cy < P && cz >= 0 && cz < P)
                        padded[cx, cy, cz] = src[x, y, z];
                }
            }

            // --- Ensure the center is always filled ---
            bool centerEmpty = true;
            for (int x = 1; x <= S; x++)
            for (int y = 1; y <= S; y++)
            for (int z = 1; z <= S; z++)
            {
                if (padded[x, y, z] != 0)
                {
                    centerEmpty = false;
                    break;
                }
            }

            if (centerEmpty)
            {
                // Use the existing thread-safe generator from ThreadedChunkProcessor
                byte[,,] generated = ThreadedChunkProcessor.GenerateChunkBlocks(coord);
                for (int x = 0; x < S; x++)
                for (int y = 0; y < S; y++)
                for (int z = 0; z < S; z++)
                    padded[x + 1, y + 1, z + 1] = generated[x, y, z];
            }


            return padded;
        }

        private IEnumerator BuildChunkMeshNextFrame(Chunk chunk)
        {
            yield return null;
            if (chunk != null && chunk.gameObject != null)
            {
                chunk.BuildMesh();
            }
        }
        
        public void EnqueueNeighborUpdates(Vector3Int coord, Vector3Int localPos)
        {
            // If block is on any border, add neighbor chunk(s) to mesh queue
            if (localPos.x == 0) AddIfExists(coord + Vector3Int.left);
            if (localPos.x == Chunk.CHUNK_SIZE - 1) AddIfExists(coord + Vector3Int.right);

            if (localPos.y == 0) AddIfExists(coord + Vector3Int.down);
            if (localPos.y == Chunk.CHUNK_SIZE - 1) AddIfExists(coord + Vector3Int.up);

            if (localPos.z == 0) AddIfExists(coord + new Vector3Int(0, 0, -1));
            if (localPos.z == Chunk.CHUNK_SIZE - 1) AddIfExists(coord + new Vector3Int(0, 0, 1));
        }

        private void AddIfExists(Vector3Int c)
        {
            if (chunks.TryGetValue(c, out Chunk n) && n != null && n.gameObject != null)
                meshQue.Add(n);
        }
        
        private void EnqueueChunkBorderUpdates(Vector3Int coord)
        {
            int S = Chunk.CHUNK_SIZE;

            // X faces
            for (int y = 0; y < S; y++)
            for (int z = 0; z < S; z++)
            {
                EnqueueNeighborUpdates(coord, new Vector3Int(0, y, z));
                EnqueueNeighborUpdates(coord, new Vector3Int(S - 1, y, z));
            }

            // Y faces
            for (int x = 0; x < S; x++)
            for (int z = 0; z < S; z++)
            {
                EnqueueNeighborUpdates(coord, new Vector3Int(x, 0, z));
                EnqueueNeighborUpdates(coord, new Vector3Int(x, S - 1, z));
            }

            // Z faces
            for (int x = 0; x < S; x++)
            for (int y = 0; y < S; y++)
            {
                EnqueueNeighborUpdates(coord, new Vector3Int(x, y, 0));
                EnqueueNeighborUpdates(coord, new Vector3Int(x, y, S - 1));
            }
        }



    }
}
