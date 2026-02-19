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
        public int colliderDistance = 1;

        private Dictionary<Vector3Int, Chunk> chunks = new Dictionary<Vector3Int, Chunk>();
        private Vector3Int playerChunkCord;
        public int chunkCount;

        public HashSet<Chunk> meshQue = new HashSet<Chunk>();
        private Queue<GameObject> chunkPool = new Queue<GameObject>();
        private HashSet<Vector3Int> generationQue = new HashSet<Vector3Int>();
        private Queue<(Chunk chunk, Vector3Int tragetPos)> transformQueue =
            new Queue<(Chunk chunk, Vector3Int tragetPos)>();

        // How many chunks should be building at once.
        public int chunksPerFrame = 4;
        private FPSCounter fpsCounter;
        private int fps;
        public bool dynamicChunkRendering = true;

        public int initialPoolSize = 20; // pre-instantiate this many chunks

        private ThreadedChunkWorker threadedWorker;

        // --- new: track pending requests so we don't enqueue duplicates
        private HashSet<Vector3Int> pendingRequests = new HashSet<Vector3Int>();
        private Settings settings;
        private int lodDistance;
        
        
        //If a player moves (so the chunks also moves), then if the player increase render distance new chunks
        //gets generated and there forms a line where moved chunks arent getting re rendered :(

        private void Awake()
        {
            fpsCounter = FindAnyObjectByType<FPSCounter>();
            Debug.Log("Save path: " + Application.persistentDataPath + "/chunks/");

            // Pre-create chunk pool
            for (int i = 0; i < initialPoolSize; i++)
            {
                GameObject go = Instantiate(chunkPrefab, Vector3.zero, Quaternion.identity, transform);
                go.SetActive(false);
                chunkPool.Enqueue(go);
            }
        }

        void Start()
        {
            settings = Settings.Instance;
            SetLodDistance();
            
            BlockRegistry.BuildThreadLookup();

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
                UpdateChunkCollidersForPlayerMove();
                UpdateChunkLODs();
            }
            
            SortChunksLists();
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

        //private const int MAX_APPLY_P_F = 1;
        
        // Called each Update to dequeue all ready worker results
        private void ProcessWorkerResults()
        {
            if (threadedWorker == null) return;

            while (threadedWorker.TryDequeueResult(out var result))
            {
                ApplyChunkResult(result);
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

            bool hasSavedBefore = WasChunkLoadedFromDisk(res.coord);

            // Apply block data
            chunk.blocks = res.blocks ?? new byte[Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE];

            //Rebuilds block entities  AFTER chunk is ready, the entity is a GO
            if (res.blockEntityLocals != null && res.blockEntityLocals.Count > 0)
            {
                //Clear old entities in case if they exist
                foreach (var be in chunk.blockEntities.Values)
                {
                    if (be != null)
                        Destroy(be.gameObject);
                }
                chunk.blockEntities.Clear();
                
                foreach (var local in res.blockEntityLocals)
                {
                    Vector3Int worldPos =
                        chunk.coord * Chunk.CHUNK_SIZE + local;

                    byte id = chunk.blocks[local.x, local.y, local.z];
                    Block.Block block = BlockRegistry.GetBlock(id);

                    if (block != null)
                    {
                        SpawnBlockEntityAtWorldPos(block, worldPos);
                    }
                }
            }

            // Apply the worker mesh data to the chunk's ChunkRendering (main thread only)
            var chunkRender = chunk.renderer;
            if (chunkRender != null && res.meshData != null)
            {
                chunk.meshData = res.meshData;
                chunkRender.ApplyMeshData(res.meshData, NeedsColliders(chunk));
                meshQue.Add(chunk);
                EnqueueNeighborRebuilds(chunk.coord);
            }
            else
            {
                meshQue.Add(chunk);
            }
            
            // Save first gen chunk
            if (!hasSavedBefore)
            {
                WorldSaveSystem.SaveChunk(chunk.coord, chunk);
                chunk.isDirty = false;
            }
            
            // Remove from pending requests set so future generates are allowed
            pendingRequests.Remove(res.coord);
        }

        public static Vector3Int IndexToPos(int index)
        {
            int C = Chunk.CHUNK_SIZE;
            
            int x = index % C;
            int y = (index / C) % C;
            int z = index / (C * C);

            return new Vector3Int(x, y, z);
        }

        public static int PosToIndex(int x, int y, int z)
        {
            return x + Chunk.CHUNK_SIZE * (y + Chunk.CHUNK_SIZE * z);
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

        public void UpdateChunks()
        {
            HashSet<Vector3Int> neededChunks = new HashSet<Vector3Int>();

            // Determine which chunks should exist
            for (int x = -viewDistance; x <= viewDistance; x++)
            for (int y = -viewDistance; y <= viewDistance; y++)
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
        
        private static readonly Vector3Int[] dirs =
        {
            Vector3Int.right, Vector3Int.left,
            Vector3Int.up, Vector3Int.down,
            new Vector3Int(0,0,1), new Vector3Int(0,0,-1)
        };

        private Chunk GenerateChunk(Vector3Int coord, int chunkNumber)
        {
            Chunk chunk = new Chunk(coord);
            GameObject go;
            Vector3Int worldPos = new Vector3Int(coord.x * Chunk.CHUNK_SIZE, coord.y * Chunk.CHUNK_SIZE,
                coord.z * Chunk.CHUNK_SIZE);

            chunk.lod = ComputeLOD(coord);
            
            if (chunkPool.Count > 0)
            {
                go = chunkPool.Dequeue();
                chunk.coord = coord;
                chunk.chunkManager = this;

                // Reset old data
                chunk.blocks = new byte[Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE];
                chunk.isDirty = false;
                
                go.SetActive(false);
                if (go.transform.position != worldPos)
                {
                    transformQueue.Enqueue((chunk,worldPos));
                }
                
            }
            else
            {
                go = Instantiate(chunkPrefab, Vector3.zero, Quaternion.identity, transform);
                chunk.coord = coord;
                chunk.chunkManager = this;
                go.transform.position = worldPos;
                go.SetActive(true);
            }

            chunk.chunkNumber = chunkNumber;
            
            go.name = "Chunk_" + coord.x + "_" + coord.y + "_" + coord.z + "_chunk_nr" + chunk.chunkNumber + "_LOD" + chunk.lod;
            ChunkRendering rendering = go.GetComponent<ChunkRendering>();
            chunk.renderer = rendering;
            rendering.SetChunkData(chunk);
            
            int lodScale = chunk.GetLodScale();

            ChunkMeshGeneratorThreaded.NeighborLODInfo neighborLODInfo =
                new ChunkMeshGeneratorThreaded.NeighborLODInfo()
                {
                    posX = GetNeighborLod(coord + Vector3Int.right, lodScale),
                    negX = GetNeighborLod(coord + Vector3Int.left, lodScale),
                    posY = GetNeighborLod(coord + Vector3Int.up, lodScale),
                    negY = GetNeighborLod(coord + Vector3Int.down, lodScale),
                    posZ = GetNeighborLod(coord + Vector3Int.forward, lodScale),
                    negZ = GetNeighborLod(coord + Vector3Int.back, lodScale),
                };

            ChunkGenRequest req;
            var neighbors = CaptureNeighborSnapshots(coord);
            
            // Load saved changes on main thread
            if (WorldSaveSystem.ChunkSaveExist(coord)) //No safe checks if the file exist not if the terrain is done generating
            //Todo make so it also checks if the terrian is ready builded
            {
                WorldSaveSystem.LoadChunk(coord, chunk);
                req = new ChunkGenRequest(coord, lodScale, neighborLODInfo, 
                    chunk.blocks, true, neighbors);
                
                //Debug.Log("NOT Building terrain, with mesh");
            }
            else
            {
                req = new ChunkGenRequest(coord, lodScale, neighborLODInfo, 
                    null, false, neighbors);
                
                //Debug.Log("Building terrain, with mesh");
            }
            
            // Mark request pending and enqueue
            chunks.Add(coord, chunk);
            pendingRequests.Add(coord);
            threadedWorker.EnqueueRequest(req);

            return chunk;
        }

        private void RemoveChunk(Chunk chunk, Vector3Int coord)
        {
            if (chunk.isDirty)
            {
                WorldSaveSystem.SaveChunk(coord, chunk);
                chunk.isDirty = false;
            }

            int C = Chunk.CHUNK_SIZE;
            
            // Reset chunk state before returning to pool
            chunk.blocks = new byte[C, C, C];
            chunk.states = new BlockStateContainer[C, C, C];
            chunk.chunkNumber = -1;
            
            //Removes old BE
            foreach (var be in chunk.blockEntities.Values)
            {
                if (be != null)
                {
                    Destroy(be.gameObject);
                }
            }
            chunk.blockEntities.Clear();
            //chunk.name = $"Chunk_{coord.x}_{coord.y}_{coord.z}_chunk_nr{chunkCount}";

            // return to pool
            chunk.renderer.gameObject.SetActive(false);
            chunkPool.Enqueue(chunk.renderer.gameObject);

            meshQue.Remove(chunk);
            generationQue.Remove(coord);

            // Make sure to remove any pending request marker
            pendingRequests.Remove(coord);

            chunks.Remove(coord);
            chunkCount--;
        }

        public void TrimUnusedChunks()
        {
            List<(Vector3Int coord, Chunk chunk)> chunksToRemove = new List<(Vector3Int coord, Chunk chunk)>();

            foreach (var kvp in chunks)
            {
                Chunk chunk = kvp.Value;
                Vector3Int coord = kvp.Key;
                
                if(IsChunkInTransformQueue(coord, chunk))
                    continue;

                // Calculate distance from player chunk
                int distanceX = Mathf.Abs(coord.x - playerChunkCord.x);
                int distanceY = Mathf.Abs(coord.y - playerChunkCord.y);
                int distanceZ = Mathf.Abs(coord.z - playerChunkCord.z);

                if (distanceX > viewDistance || distanceY > viewDistance || distanceZ > viewDistance)
                {
                    if (chunk.isDirty)
                    {
                        // Save changes before removing
                        WorldSaveSystem.SaveChunk(coord, chunk);
                        Destroy(chunk.renderer.gameObject);
                    }
                    else
                    {
                        Destroy(chunk.renderer.gameObject);
                    }

                    chunk.blocks = new byte[Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE];
                    chunk.states = new BlockStateContainer[Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE];
                    chunk.chunkNumber = -1;
                    
                    //Removes old BE
                    foreach (var be in chunk.blockEntities.Values)
                    {
                        if (be != null)
                        {
                            Destroy(be.gameObject);
                        }
                    }
                    chunk.blockEntities.Clear();
                    
                    meshQue.Remove(chunk);
                    generationQue.Remove(coord);
                    pendingRequests.Remove(coord);
                    
                    // Chunk is outside the new view distance
                    chunksToRemove.Add((coord,chunk));
                }
            }

            // Remove from dictionary
            foreach (var key in chunksToRemove)
            {
                chunks.Remove(key.coord);
                chunkCount--;
            }
        }

        public Chunk GetChunk(Vector3Int coord)
        {
            chunks.TryGetValue(coord, out var c);
            return c;
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
            
            Block.Block block = BlockRegistry.GetBlock(id);
            BlockStateContainer state = null;

            byte oldId = chunk.blocks[local.x, local.y, local.z];
            Block.Block oldBlock = BlockRegistry.GetBlock(oldId);
            

            if (id != 0 && block != null && block.HasStates)
            {
                state = new BlockStateContainer();
                block?.OnPlaced(
                    position: worldPos, state: state, player: player);
            }

            if (id != 0 && block != null && block.HasBlockEntity)
            {
                SpawnBlockEntityAtWorldPos(block, worldPos);
            }

            if (id == 0 && oldBlock != null && oldId != 0)
            {
                oldBlock?.OnMined(worldPos,state,player);
                RemoveBlockEntityAtWorldPos(worldPos);
            }
            
            // Sets block at the local chunk
            chunk.SetBlockLocal(local, id, state);
            
            // Enqueue neighbors if block is on border
            if (local.x == 0 || local.x == Chunk.CHUNK_SIZE - 1 ||
                local.y == 0 || local.y == Chunk.CHUNK_SIZE - 1 ||
                local.z == 0 || local.z == Chunk.CHUNK_SIZE - 1)
            {
                EnqueueNeighborUpdates(chunk.coord, local);
            }
        }

        private void SpawnBlockEntityAtWorldPos(Block.Block block, Vector3Int worldPos)
        {
            Chunk chunk = GetChunkFromWorldPos(worldPos);
            if(chunk == null)
                return;

            Vector3Int local = chunk.WorldToLocal(worldPos);

            if (chunk.blockEntities.ContainsKey(local))
                return;

            if (block is ChestBlock)
            {
                GameObject go = new GameObject("ChestEntity_With_No_Name :(");
                go.transform.SetParent(chunk.renderer.transform, false);
                go.transform.position = worldPos + Vector3.one * 0.5f;

                var holder = go.AddComponent<ChestInventoryHolder>();
                holder.Init(worldPos);
                chunk.blockEntities[local] = holder;
                return;
            }

            if (block is CraftingTableBlock)
            {
                GameObject go = new GameObject("CraftingTableEntity");
                go.transform.SetParent(chunk.renderer.transform, false);
                go.transform.position = worldPos + Vector3.one * 0.5f;

                var holder = go.AddComponent<CraftingTableInventoryHolder>();
                holder.Init(worldPos);
                chunk.blockEntities[local] = holder;
            }
        }

        private void RemoveBlockEntityAtWorldPos(Vector3Int worldPos)
        {
            Chunk chunk = GetChunkFromWorldPos(worldPos);
            if(chunk == null)
                return;

            Vector3Int local = chunk.WorldToLocal(worldPos);
            
            if(!chunk.blockEntities.TryGetValue(local, out InventoryHolder holder))
                return;
            
            holder.SaveInventory();
            
            Destroy(holder.gameObject);
            chunk.blockEntities.Remove(local);
        }

        private void RebuildBlockEntities(Chunk chunk)
        {
            if(chunk == null) return;
            
            chunk.blockEntities.Clear();

            for (int x = 0; x < Chunk.CHUNK_SIZE; x++)
            for (int y = 0; y < Chunk.CHUNK_SIZE; y++)
            for (int z = 0; z < Chunk.CHUNK_SIZE; z++)
            {
                byte id = chunk.blocks[x, y, z];
                
                if(id == 0) continue;

                Block.Block block = BlockRegistry.GetBlock(id);
                if(block == null || !block.HasBlockEntity) continue;

                Vector3Int localPos = new Vector3Int(x, y, z);

                Vector3Int worldPos =
                    chunk.coord * Chunk.CHUNK_SIZE + localPos;
                
                SpawnBlockEntityAtWorldPos(block, worldPos);
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

        public BlockStateContainer GetBlockStateAtWorldPos(Vector3Int worldPos)
        {
            Chunk chunk = GetChunkFromWorldPos(worldPos);
            if (chunk == null) return null;

            Vector3Int local = chunk.WorldToLocal(worldPos);

            if (local.x < 0 || local.x >= Chunk.CHUNK_SIZE ||
                local.y < 0 || local.y >= Chunk.CHUNK_SIZE ||
                local.z < 0 || local.z >= Chunk.CHUNK_SIZE)
                return null;

            return chunk.states[local.x, local.y, local.z];
        }
        

        private void UpdateChunkCollidersForPlayerMove()
        {
            foreach (var chunk in chunks.Values)
            {
                bool needsCollider = NeedsColliders(chunk);

                // Create collider if now needed but missing
                if (needsCollider && !chunk.renderer.HasCollider())
                {
                    meshQue.Add(chunk); // <-- FORCE rebuild WITH collider
                }

                // Remove collider if no longer needed
                if (!needsCollider && chunk.renderer.HasCollider())
                {
                    chunk.renderer.DestroyCollider();
                }
            }
        }



        private void UpdateChunkLODs()
        {
            foreach (var chunk in chunks.Values)
            {
                if (chunk == null || chunk.renderer == null)
                    continue;

                Chunk.ChunkLOD newLod = ComputeLOD(chunk.coord);

                if (chunk.lod != newLod)
                {
                    chunk.lod = newLod;

                    // Tell renderer (for now: just store it)
                    //chunk.renderer.SetLOD(newLod);

                    // Debug proof
                    chunk.renderer.gameObject.name =
                        $"Chunk_{chunk.coord.x}_{chunk.coord.y}_{chunk.coord.z}_chunk_nr_{chunk.chunkNumber.ToString()}_LOD{(int)newLod}";
                    
                    chunk.isColliderDirty = true;
                    
                    // Later: this will enqueue a mesh rebuild
                    meshQue.Add(chunk);
                    EnqueueNeighborRebuilds(chunk.coord);
                }
            }
        }


        private void UpdateFPS()
        {
            int mult = 1;
            
            if (fpsCounter == null) return;
            fps = fpsCounter.CurrentFPS;

            if (!dynamicChunkRendering) return;

            // Dynamic scaling (with clamping)
            if (fps > 110)
                chunksPerFrame = 14 * mult;
            else if (fps > 80)
                chunksPerFrame = 12 * mult;
            else if (fps > 60)
                chunksPerFrame = 9 * mult;
            else if (fps > 40)
                chunksPerFrame = 7 * mult;
            else if (fps > 25)
                chunksPerFrame = 4 * mult;
            else if (fps > 15)
                chunksPerFrame = 2 * mult;
            else
                chunksPerFrame = 1 * mult;

            if (meshQue.Count <= 0 && generationQue.Count <= 0 && transformQueue.Count <= 0)
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
                    WorldSaveSystem.SaveChunk(chunk.coord, chunk);
                    chunk.isDirty = false;
                }
            }
        }

        private Dictionary<Vector3Int, byte[,,]> CaptureNeighborSnapshots(Vector3Int coord)
        {
            var dict = new Dictionary<Vector3Int, byte[,,]>();
            
            for (int ox = -1; ox <= 1; ox++)
            for (int oy = -1; oy <= 1; oy++)
            for (int oz = -1; oz <= 1; oz++)
            {
                Vector3Int nc = coord + new Vector3Int(ox, oy, oz);

                if (!chunks.TryGetValue(nc, out Chunk c) || c.blocks == null)
                    continue;

                // SNAPSHOT (important!)
                dict[nc] = (byte[,,])c.blocks.Clone();
            }

            return dict;
        }

        private bool IsChunkBusy(Vector3Int coord, Chunk chunk)
        {
            return pendingRequests.Contains(coord) ||
                   generationQue.Contains(coord) ||
                   meshQue.Contains(chunk) ||
                   transformQueue.Any(t => t.chunk == chunk);
        }

        private bool IsChunkInTransformQueue(Vector3Int coord, Chunk chunk)
        {
            return transformQueue.Any(t => t.chunk == chunk);
        }

        private IEnumerator BuildChunkMeshNextFrame(Chunk chunk)
        {
            yield return null;
            
            if (chunk == null || chunk.renderer == null)
                yield break;
            
            chunk.renderer.Rebuild(NeedsColliders(chunk));
            chunk.isColliderDirty = false;
        }
        
        private bool NeedsColliders(Chunk chunk)
        {
            if(chunk == null || chunk.blocks == null)
                return false;

            int dx = Mathf.Abs(chunk.coord.x - playerChunkCord.x);
            int dy = Mathf.Abs(chunk.coord.y - playerChunkCord.y);
            int dz = Mathf.Abs(chunk.coord.z - playerChunkCord.z);
            bool needsCollider =
                dx <= colliderDistance &&
                dy <= colliderDistance &&
                dz <= colliderDistance;

            if (needsCollider)
            {
                return true;
            }

            return false;
        }
        
        
        private void EnqueueNeighborRebuilds(Vector3Int coord)
        {
            if(!chunks.TryGetValue(coord, out Chunk c))
                return;
            
            meshQue.Add(c);

            foreach (var d in dirs)
            {
                if (chunks.TryGetValue(coord + d, out Chunk neighbor) &&
                    neighbor.renderer && neighbor.renderer.gameObject)
                {
                    meshQue.Add(neighbor);
                }
            }
        }

        
        public void EnqueueNeighborUpdates(Vector3Int coord, Vector3Int localPos)
        {
            // If block is on any border, add neighbor chunk(s) to mesh queue
            
            //This is currently only used for block placement!
            if (localPos.x == 0) AddIfExists(coord + Vector3Int.left);
            if (localPos.x == Chunk.CHUNK_SIZE - 1) AddIfExists(coord + Vector3Int.right);

            if (localPos.y == 0) AddIfExists(coord + Vector3Int.down);
            if (localPos.y == Chunk.CHUNK_SIZE - 1) AddIfExists(coord + Vector3Int.up);

            if (localPos.z == 0) AddIfExists(coord + new Vector3Int(0, 0, -1));
            if (localPos.z == Chunk.CHUNK_SIZE - 1) AddIfExists(coord + new Vector3Int(0, 0, 1));
        }

        private void AddIfExists(Vector3Int c)
        {
            if (chunks.TryGetValue(c, out Chunk n) && n != null && n.renderer.gameObject != null)
            {
                meshQue.Add(n);
                n.isColliderDirty = true;
            }
                
        }
        

        private void SortChunksLists()
        {
            // Remove null/destroyed chunks first
            meshQue.RemoveWhere(c => c == null || c.renderer.gameObject == null);
            
            int buildChunksThisFrame = Mathf.Min(chunksPerFrame, meshQue.Count);
            int generatingChunksThisFrame = Mathf.Min(chunksPerFrame, generationQue.Count);

            var ordered = generationQue.OrderBy(c =>
            {
                float dx = player.position.x - c.x * Chunk.CHUNK_SIZE;
                float dy = player.position.y - c.y * Chunk.CHUNK_SIZE;
                float dz = player.position.z - c.z * Chunk.CHUNK_SIZE;
                return dx * dx + dy * dy + dz * dz;
            }).Take(generatingChunksThisFrame).ToList();
            
            // Before generating chunks in Update()
            generationQue.RemoveWhere(coord => 
                Mathf.Abs(coord.x - playerChunkCord.x) > viewDistance ||
                Mathf.Abs(coord.y - playerChunkCord.y) > viewDistance ||
                Mathf.Abs(coord.z - playerChunkCord.z) > viewDistance);
            
            meshQue.RemoveWhere(c => 
                Mathf.Abs(c.coord.x - playerChunkCord.x) > viewDistance ||
                Mathf.Abs(c.coord.y - playerChunkCord.y) > viewDistance ||
                Mathf.Abs(c.coord.z - playerChunkCord.z) > viewDistance);


            foreach (var coord in ordered)
            {
                generationQue.Remove(coord);
                chunkCount++;
                GenerateChunk(coord, chunkCount);
            }
            
            if (transformQueue.Count > 0)
            {
                int transformChunksThisFrame = Mathf.Min(chunksPerFrame, transformQueue.Count);
                transformChunksThisFrame = Mathf.Clamp(transformChunksThisFrame/2, 1, transformQueue.Count);
            
                //Transform que
                for (int i = 0; i < transformChunksThisFrame; i++)
                {
                    var t = transformQueue.Dequeue();
                    if (t.chunk != null && t.chunk.renderer != null
                        && t.chunk.renderer.gameObject != null && 
                        chunks.ContainsKey(t.chunk.coord))
                    {
                        t.chunk.renderer.transform.position = t.tragetPos;
                        t.chunk.renderer.gameObject.SetActive(true);
                        
                        EnqueueNeighborRebuilds(t.chunk.coord);
                    }
                }
            }

            // Build meshes from meshQue (distance prioritized)
            if (meshQue.Count > 0 && chunksPerFrame > 0)
            {
                // Sort by distance to player using chunk coordinates
                List<Chunk> sortedChunks = meshQue
                    .Where(c => c != null)
                    .OrderBy(c =>
                    {
                        // Compute world position from coord
                        Vector3 worldPos = new Vector3(
                            c.coord.x * Chunk.CHUNK_SIZE,
                            c.coord.y * Chunk.CHUNK_SIZE,
                            c.coord.z * Chunk.CHUNK_SIZE
                        );
                        return Vector3.SqrMagnitude(player.position - worldPos);
                    })
                    .ToList();

                // Build closest chunks first
                for (int i = 0; i < buildChunksThisFrame; i++)
                {
                    Chunk chunkToBuild = sortedChunks[i];
                    if (chunkToBuild != null && chunkToBuild.renderer.gameObject != null &&
                        chunkToBuild.renderer.gameObject.activeInHierarchy)
                    {
                        StartCoroutine(BuildChunkMeshNextFrame(chunkToBuild));
                    }

                    // Remove from queue regardless (we're attempting to build it)
                    meshQue.Remove(chunkToBuild);
                }
            }

            
            
            
        }

        private void SetLodDistance()
        {
            int distance = 32; //Default number
            
            if (settings != null)
            {
                distance = settings.lodDistance;
            }

            lodDistance = distance;
        }
        
        private Chunk.ChunkLOD ComputeLOD(Vector3Int chunkCoord)
        {
            int lodDistance = this.lodDistance;
            
            int dx = Mathf.Abs(chunkCoord.x - playerChunkCord.x);
            int dy = Mathf.Abs(chunkCoord.y - playerChunkCord.y);
            int dz = Mathf.Abs(chunkCoord.z - playerChunkCord.z);

            int dist = Mathf.Max(dx, Mathf.Max(dy, dz));
            if (dist <= lodDistance) return Chunk.ChunkLOD.LOD0;
            if (dist <= lodDistance * 2) return Chunk.ChunkLOD.LOD1;
            if (dist <= lodDistance * 4) return Chunk.ChunkLOD.LOD2;
            if (dist <= lodDistance * 8) return Chunk.ChunkLOD.LOD3;
            return Chunk.ChunkLOD.LOD4;
        }
        
        int GetNeighborLod(Vector3Int c, int fallback)
        {
            if (!chunks.TryGetValue(c, out var ch))
                return int.MaxValue; // force coarse-side face

            return ch.GetLodScale();
        }
        
        public ChunkMeshGeneratorThreaded.NeighborLODInfo GetNeighborLODInfo(Vector3Int coord)
        {
            int fallback = chunks.TryGetValue(coord, out var center)
                ? center.GetLodScale()
                : 1;

            return new ChunkMeshGeneratorThreaded.NeighborLODInfo
            {
                posX = GetNeighborLod(coord + Vector3Int.right,   fallback),
                negX = GetNeighborLod(coord + Vector3Int.left,    fallback),
                posY = GetNeighborLod(coord + Vector3Int.up,      fallback),
                negY = GetNeighborLod(coord + Vector3Int.down,    fallback),
                posZ = GetNeighborLod(coord + Vector3Int.forward, fallback),
                negZ = GetNeighborLod(coord + Vector3Int.back,    fallback),
            };
        }

        private bool WasChunkLoadedFromDisk(Vector3Int coord)
        {
            return WorldSaveSystem.ChunkSaveExist(coord);
        }
        
    }
}
