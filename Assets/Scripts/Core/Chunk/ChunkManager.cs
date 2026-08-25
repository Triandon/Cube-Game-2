using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Core.Block;
using Core.Block.TileEntities;
using Misc.InventoryHolders;
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
        private Vector3 lastChunkUpdatePosition;
        public int chunkCount;

        public HashSet<Chunk> meshQue = new HashSet<Chunk>(); //mabye make private
        private Queue<GameObject> chunkPool = new Queue<GameObject>();
        public HashSet<Vector3Int> generationQue = new HashSet<Vector3Int>(); //p
        public Queue<(Chunk chunk, Vector3Int tragetPos)> transformQueue =
            new Queue<(Chunk chunk, Vector3Int tragetPos)>(); //p

        // How many chunks should be building at once.
        public int chunksPerFrame = 4;
        public int visualChunksPerFrame = 4;
        private FPSCounter fpsCounter;
        private int fps;
        public bool dynamicChunkRendering = true;

        public int initialPoolSize = 20; // pre-instantiate this many chunks

        private ThreadedChunkWorker threadedWorker;
        private TickCaller tickCaller;

        // --- new: track pending requests so we don't enqueue duplicates
        private HashSet<Vector3Int> pendingRequests = new HashSet<Vector3Int>();
        protected HashSet<Vector3Int> knownAllAirChunks = new HashSet<Vector3Int>();
        
        private Settings settings;
        private int lodDistance;
        private LightPropagator lightPropagator;
        private ChunkLightWorld lightWorld;
        private readonly Queue<Chunk> lightQueue = new Queue<Chunk>();
        private readonly LightingSkyOcclusionMap skyOcclusionMap = new LightingSkyOcclusionMap();
        private bool skyOcclusionDirty;
        private float skyOcclusionDirtyTime;
        private readonly Queue<Vector3Int> skyRepairQueue = new Queue<Vector3Int>();
        private readonly HashSet<Vector3Int> queuedSkyRepairs = new HashSet<Vector3Int>();
        private const float SkyOcclusionSaveDelay = 3f;

        
        //If a player moves (so the chunks also moves), then if the player increase render distance new chunks
        //gets generated and there forms a line where moved chunks arent getting re rendered :(

        private void Awake()
        {
            fpsCounter = FindAnyObjectByType<FPSCounter>();
            WorldSaveSystem.Initialize(Application.persistentDataPath);
            WorldSaveSystem.LoadSkyOcclusionMap(skyOcclusionMap);
            Debug.Log("Save path: " + WorldSaveSystem.GetChunkDirectory() + "/");

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
            tickCaller = World.Instance != null ? World.Instance.GetTickCaller() : null;
            
            BlockRegistry.BuildThreadLookup();
            lightWorld = new ChunkLightWorld(this);
            lightPropagator = new LightPropagator(lightWorld);

            // start worker threads (use processorCount -1 or 1 minimum)
            threadedWorker = new ThreadedChunkWorker(Math.Max(1, SystemInfo.processorCount - 1));
            threadedWorker.Start();

            UpdatePlayerChunkCoord();
            lastChunkUpdatePosition = player != null ? player.position : Vector3.zero;
            UpdateChunks();
        }

        // Update is called once per frame
        void Update()
        {
            UpdateFPS();

            // Pull worker results onto the main thread immediately
            ProcessWorkerResults();
            ProcessLightingIntegration();
            SaveSkyOcclusionMapAfterDelay();
            
            if (HasMovedChunkDistance())
            {
                playerChunkCord = GetPlayerChunkCoord();
                lastChunkUpdatePosition = player.position;
                UpdateChunks();
                UpdateChunkLODs();
            }
            
            SortChunksLists();
        }

        private void OnDestroy()
        {
            SaveSkyOcclusionMapIfDirty();
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

            //This limits the amount of chunks that gets applyed to the main thread from the sepperate thread.
            int applyedResults = 0;
            int a = chunksPerFrame * 5 + 1;
            int applyLimit = Mathf.Max(0, a);

            while (applyedResults < applyLimit && threadedWorker.TryDequeueResult(out var result))
            {
                ApplyChunkResult(result);
                applyedResults++;
            }
        }

        private void ApplyChunkResult(ChunkGenResult res)
        {
            if (res == null) return;

            // If chunk outside of render distance
            if (IsOutsideRenderDistance(res.coord))
            {
                pendingRequests.Remove(res.coord);
                return;
            }

            if (!chunks.TryGetValue(res.coord, out Chunk chunk))
            {
                if (res.isAllAir)
                {
                    RecordSkyOcclusion(res.coord, res.blocks, true);
                    knownAllAirChunks.Add(res.coord);
                    pendingRequests.Remove(res.coord);
                    return;
                }

                chunkCount++;
                chunk = GenerateChunkShell(res.coord, chunkCount);
            }
            
            Vector3 chunkWorldPos = new Vector3(
                res.coord.x * Chunk.CHUNK_SIZE,
                res.coord.y * Chunk.CHUNK_SIZE,
                res.coord.z * Chunk.CHUNK_SIZE);

            if (chunk.renderer != null)
            {
                chunk.renderer.transform.position = chunkWorldPos;
            }

            bool hasSavedBefore = WasChunkLoadedFromDisk(res.coord);

            // Apply block data
            chunk.blocks = res.blocks ?? new byte[Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE];
            chunk.states = res.states ?? new BlockStateContainer[Chunk.CHUNK_SIZE,
                Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE];
            chunk.skyLight = res.skyLight ?? new byte[Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE];
            chunk.blockLight = res.blockLight ?? new byte[Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE];
            RecordSkyOcclusion(res.coord, chunk.blocks, true);
            
            // Light maps are runtime data and the propagator's source registry is
            // not serialized. Re-register emitters from the loaded block data so
            // lamps work immediately after loading a world (and can react to later
            // voxel changes without first being mined and placed again).
            RegisterBlockLightSources(chunk);
            
            chunk.RebuildSpecialMeshBlocks();
            lightQueue.Enqueue(chunk);

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
            if (chunkRender != null)
                //&& res.meshData != null
            {
                //chunk.meshData = res.meshData;
                //chunkRender.ApplyMeshData(res.meshData);
                // REMOVE meshQue.Add
                //todo Place to fix!!!
                
                meshQue.Add(chunk);
                EnqueueNeighborRebuilds(chunk.coord);
            }
            else
            {
                meshQue.Add(chunk);
            }
            
            tickCaller?.RegisterChunk(chunk, res.instantTickLocals, res.scheduledTickLocals,
                res.randomTickLocals);
            
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
        
        private bool HasMovedChunkDistance()
        {
            if (player == null)
                return false;

            Vector3 delta = player.position - lastChunkUpdatePosition;
            return Mathf.Abs(delta.x) >= Chunk.CHUNK_SIZE ||
                   Mathf.Abs(delta.y) >= Chunk.CHUNK_SIZE ||
                   Mathf.Abs(delta.z) >= Chunk.CHUNK_SIZE;
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
                    if (!chunks.ContainsKey(logicalCoord) && !knownAllAirChunks.Contains(logicalCoord) &&
                        !pendingRequests.Contains(logicalCoord) && !generationQue.Contains(logicalCoord))
                    {
                        generationQue.Add(logicalCoord);
                    }
                }
            }

            knownAllAirChunks.RemoveWhere(coord => !neededChunks.Contains(coord));
            
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

        private Chunk GenerateChunkShell(Vector3Int coord, int chunkNumber)
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
                chunk.states = new BlockStateContainer[Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE];
                chunk.skyLight = new byte[Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE];
                chunk.blockLight = new byte[Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE];

                chunk.isDirty = false;

                go.SetActive(false);
                //if (go.transform.position != worldPos)
                //{
                    //transformQueue.Enqueue((chunk, worldPos));
                //}
                
                // Always enqueue pooled chunks so they become active again,
                // even when reused at the same world position.
                transformQueue.Enqueue((chunk,worldPos));
                
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

            go.name = "Chunk_" + coord.x + "_" + coord.y + "_" + coord.z + "_chunk_nr" + chunk.chunkNumber + "_LOD" +
                      chunk.lod;
            ChunkRendering rendering = go.GetComponent<ChunkRendering>();
            chunk.renderer = rendering;
            rendering.SetChunkData(chunk);

            chunks.Add(coord, chunk);
            return chunk;
        }

        private void EnqueueChunkDataRequest(Vector3Int coord)
        {
            int lodScale = chunks.TryGetValue(coord, out var existingChunk)
                ? existingChunk.GetLodScale()
                : 1 << (int)ComputeLOD(coord);

            ChunkMeshGeneratorThreaded.NeighborLODInfo neighborLODInfo =
                new ChunkMeshGeneratorThreaded.NeighborLODInfo
                {
                    posX = GetNeighborLod(coord + Vector3Int.right, lodScale),
                    negX = GetNeighborLod(coord + Vector3Int.left, lodScale),
                    posY = GetNeighborLod(coord + Vector3Int.up, lodScale),
                    negY = GetNeighborLod(coord + Vector3Int.down, lodScale),
                    posZ = GetNeighborLod(coord + Vector3Int.forward, lodScale),
                    negZ = GetNeighborLod(coord + Vector3Int.back, lodScale),
                };

            HashSet<Vector3Int> specialMeshBlocks =
                existingChunk != null
                    ? existingChunk.GetSpecialMeshBlocksSnapshot()
                    : new HashSet<Vector3Int>();
            
            var (neighbors, neighborStates) = CaptureNeighborSnapshots(coord);
            var req = new ChunkGenRequest(
                coord,
                lodScale,
                neighborLODInfo,
                existingChunk?.blocks,
                existingChunk?.states,
                existingChunk != null,
                neighbors,
                neighborStates,
                specialMeshBlocks,
                allowDiskLoad: existingChunk == null,
                chunkSavePath: existingChunk == null ? WorldSaveSystem.GetChunkPath(coord) : null,
                incomingSkyLightFromAbove: BuildIncomingSkyLightFromAbove(coord));
            
            pendingRequests.Add(coord);
            threadedWorker.EnqueueRequest(req);
        }
        
        private byte[,] BuildIncomingSkyLightFromAbove(Vector3Int coord)
        {
            int S = Chunk.CHUNK_SIZE;
            byte[,] incoming = new byte[S, S];

            if (chunks.TryGetValue(coord + Vector3Int.up, out Chunk above) && above?.skyLight != null)
            {
                for (int x = 0; x < S; x++)
                for (int z = 0; z < S; z++)
                    incoming[x, z] = above.skyLight[x, 0, z];
            }
            else
            {
                for (int x = 0; x < S; x++)
                for (int z = 0; z < S; z++)
                    incoming[x, z] = HasKnownSkyOccluderAbove(
                        coord.x * S + x, coord.y * S + S - 1, coord.z * S + z)
                        ? VoxelLight.Min
                        : VoxelLight.Max;
            }

            return incoming;
        }

        private void RemoveChunk(Chunk chunk, Vector3Int coord)
        {
            tickCaller?.UnregisterChunk(chunk);
            if (chunk.isDirty)
            {
                WorldSaveSystem.SaveChunk(coord, chunk);
                chunk.isDirty = false;
            }
            
            UnregisterBlockLightSources(chunk);

            int C = Chunk.CHUNK_SIZE;
            
            // Reset chunk state before returning to pool
            chunk.blocks = new byte[C, C, C];
            chunk.states = new BlockStateContainer[C, C, C];
            chunk.skyLight = new byte[C, C, C];
            chunk.blockLight = new byte[C, C, C];
            chunk.specialMeshBlocks.Clear();
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
                    tickCaller?.UnregisterChunk(chunk);
                    UnregisterBlockLightSources(chunk);
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
        
        internal byte GetVirtualSkyLight(Vector3Int worldPos)
        {
            int size = Chunk.CHUNK_SIZE;
            Vector3Int chunkCoord = new Vector3Int(
                Mathf.FloorToInt((float)worldPos.x / size),
                Mathf.FloorToInt((float)worldPos.y / size),
                Mathf.FloorToInt((float)worldPos.z / size));

            World world = World.Instance;
            if (world == null)
                return VoxelLight.Min;

            // There is no chunk above the world's ceiling, but mesh faces still
            // sample that voxel and should see open sky.
            if (chunkCoord.y >= world.worldSizeY)
                return VoxelLight.Max;

            // A generated all-air chunk is deliberately kept virtual. Treat it
            // as transparent without doing the much more expensive operation of
            // materializing a renderer just so a neighboring face can sample it.
            if (!world.IsChunkInsideOfWorld(chunkCoord) || !knownAllAirChunks.Contains(chunkCoord))
                return VoxelLight.Min;

            return HasKnownSkyOccluderAbove(worldPos.x, worldPos.y, worldPos.z)
                ? VoxelLight.Min
                : VoxelLight.Max;
        }

        private Chunk GetOrCreateChunkForWorld(Vector3Int worldPos, byte idToWrite)
        {
            Vector3Int chunkCord = new Vector3Int(
                Mathf.FloorToInt((float)worldPos.x / Chunk.CHUNK_SIZE),
                Mathf.FloorToInt((float)worldPos.y / Chunk.CHUNK_SIZE),
                Mathf.FloorToInt((float)worldPos.z / Chunk.CHUNK_SIZE));

            if (chunks.TryGetValue(chunkCord, out Chunk existingChunk))
            {
                return existingChunk;
            }

            if (idToWrite == 0)
            {
                return null;
            }

            if (World.Instance == null || !World.Instance.IsChunkInsideOfWorld(chunkCord))
            {
                return null;
            }

            if (!knownAllAirChunks.Contains(chunkCord) && !WorldSaveSystem.ChunkSaveExist(chunkCord))
            {
                if (!pendingRequests.Contains(chunkCord) && !generationQue.Contains(chunkCord))
                {
                    generationQue.Add(chunkCord);
                }

                return null;
            }

            knownAllAirChunks.Remove(chunkCord);
            generationQue.Remove(chunkCord);
            pendingRequests.Remove(chunkCord);

            chunkCount++;
            Chunk chunk = GenerateChunkShell(chunkCord, chunkCount);
            chunk.blocks = new byte[Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE];
            chunk.states = new BlockStateContainer[Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE];

            if (WorldSaveSystem.ChunkSaveExist(chunkCord))
            {
                if (WorldSaveSystem.LoadChunk(chunkCord, chunk))
                {
                    chunk.RebuildSpecialMeshBlocks();
                    RebuildBlockEntities(chunk);
                }
            }
            
            RegisterBlockLightSources(chunk);
            
            // All-air chunks normally stay virtual, so this shell did not pass
            // through ThreadedChunkProcessor and its light arrays still contain
            // zeroes. Seed its direct sunlight before applying the first placed
            // block; otherwise the incremental repair has no lit neighbor to
            // propagate from until that block is removed again.
            InitializeSkyLight(chunk);
            lightQueue.Enqueue(chunk);

            meshQue.Add(chunk);
            EnqueueNeighborRebuilds(chunkCord);

            return chunk;
        }
        
        private void InitializeSkyLight(Chunk chunk)
        {
            int size = Chunk.CHUNK_SIZE;
            byte[,] incoming = BuildIncomingSkyLightFromAbove(chunk.coord);
            chunk.skyLight = new byte[size, size, size];

            for (int x = 0; x < size; x++)
            for (int z = 0; z < size; z++)
            {
                byte current = incoming[x, z];
                for (int y = size - 1; y >= 0; y--)
                {
                    byte blockId = chunk.blocks[x, y, z];
                    if (VoxelLight.BlocksSkyLight(blockId))
                    {
                        chunk.skyLight[x, y, z] = VoxelLight.Min;
                        current = VoxelLight.Min;
                    }
                    else
                    {
                        chunk.skyLight[x, y, z] = current;
                    }
                }
            }
        }
        
        private void RegisterBlockLightSources(Chunk chunk)
        {
            if (chunk?.blocks == null)
                return;

            EnsureLightPropagator();
            Vector3Int origin = chunk.coord * Chunk.CHUNK_SIZE;
            for (int x = 0; x < Chunk.CHUNK_SIZE; x++)
            for (int y = 0; y < Chunk.CHUNK_SIZE; y++)
            for (int z = 0; z < Chunk.CHUNK_SIZE; z++)
            {
                Block.Block block = BlockRegistry.GetBlock(chunk.blocks[x, y, z]);
                byte emission = block?.LightLevel ?? VoxelLight.Min;
                if (emission > VoxelLight.Min)
                    lightPropagator.AddBlockLight(origin + new Vector3Int(x, y, z), emission);
            }

            EnqueueChangedLightMeshes();
        }

        private void UnregisterBlockLightSources(Chunk chunk)
        {
            if (chunk?.blocks == null || lightPropagator == null)
                return;

            Vector3Int origin = chunk.coord * Chunk.CHUNK_SIZE;
            for (int x = 0; x < Chunk.CHUNK_SIZE; x++)
            for (int y = 0; y < Chunk.CHUNK_SIZE; y++)
            for (int z = 0; z < Chunk.CHUNK_SIZE; z++)
            {
                Block.Block block = BlockRegistry.GetBlock(chunk.blocks[x, y, z]);
                if ((block?.LightLevel ?? VoxelLight.Min) > VoxelLight.Min)
                    lightPropagator.RemoveBlockLight(origin + new Vector3Int(x, y, z));
            }

            EnqueueChangedLightMeshes();
        }

        public void SetBlockAtWorldPos(Vector3Int worldPos, byte id, Vector3Int? placementFace = null)
        {
            Chunk chunk = GetOrCreateChunkForWorld(worldPos, id);
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
            byte oldEmission = oldBlock?.LightLevel ?? VoxelLight.Min;

            byte newEmission = block?.LightLevel ?? VoxelLight.Min;
            bool skyOpacityChanged = VoxelLight.BlocksSkyLight(oldId)
                                     != VoxelLight.BlocksSkyLight(id);
            

            if (id != 0 && block != null)
            {
                state = new BlockStateContainer();
                block?.OnPlaced(
                    position: worldPos, state: state, player: player, placementFace: placementFace);
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
            if (skyOcclusionMap.UpdateColumn(chunk.coord, chunk.blocks, local.x, local.z, GetWorldHeight()))
                MarkSkyOcclusionDirty();
            
            // Keep block-light sources in sync with voxel changes. Previously the
            // public node-light API was never called by block placement/mining, so
            // a mined lamp remained in the source dictionary and kept the room lit.
            EnsureLightPropagator();
            if (oldEmission > VoxelLight.Min)
                lightPropagator.RemoveBlockLight(worldPos);
            if (newEmission > VoxelLight.Min)
                lightPropagator.AddBlockLight(worldPos, newEmission);

            // Sky light only depends on transparency. Avoid rebuilding the entire
            // loaded domain for changes between blocks with the same opacity.
            if (skyOpacityChanged)
            {
                // Repair the local gradients instead of synchronously rebuilding
                // every light voxel in every loaded chunk.
                lightPropagator.UpdateAfterVoxelChange(worldPos);
                EnqueueChangedLightMeshes();
            }
            else if (oldEmission != newEmission)
            {
                EnqueueChangedLightMeshes();
            }
            
            tickCaller?.OnBlockChanged(worldPos, oldId, id);
            
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
            if (chunk == null)
                return;

            Vector3Int local = chunk.WorldToLocal(worldPos);

            if (chunk.blockEntities.ContainsKey(local))
                return;

            if (!BlockEntityRegistry.TryCreate(block.id, chunk.renderer.transform,
                    worldPos, out InventoryHolder holder))
            {
                return;
            }

            chunk.blockEntities[local] = holder;
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

        public bool CheckForVoxel(float _x, float _y, float _z)
        {
            Vector3 samplePos = new Vector3(_x, _y, _z);
            Vector3Int worldBlockPos = Vector3Int.FloorToInt(samplePos);

            if (!TryGetCollisionBlock(worldBlockPos, out Chunk chunk, out Vector3Int local, out byte blockId))
                return false;

            if (!TryGetBlockCollisionBounds(chunk, local, out Vector3 min, out Vector3 max))
                return false;

            Vector3 localPoint = samplePos - worldBlockPos;
            const float eps = 0.0001f;
            
            return localPoint.x >= min.x - eps && localPoint.x <= max.x + eps &&
                   localPoint.y >= min.y - eps && localPoint.y <= max.y + eps &&
                   localPoint.z >= min.z - eps && localPoint.z <= max.z + eps;
        }

        public bool CheckForVoxel(Vector3 worldPos)
        {
            return CheckForVoxel(worldPos.x, worldPos.y, worldPos.z);
        }

        public bool CheckForVoxel(Vector3Int worldBlockPos)
        {
            return TryGetCollisionBlock(worldBlockPos, out _, out _, out _);
        }

        private bool TryGetCollisionBlock(Vector3Int worldBlockPos, out Chunk chunk, out Vector3Int local,
            out byte blockId)
        {
            chunk = GetChunkFromWorldPos(worldBlockPos);
            local = default;
            blockId = 0;

            if (chunk == null)
                return false;

            local = chunk.WorldToLocal(worldBlockPos);

            if (local.x < 0 || local.x >= Chunk.CHUNK_SIZE ||
                local.y < 0 || local.y >= Chunk.CHUNK_SIZE ||
                local.z < 0 || local.z >= Chunk.CHUNK_SIZE)
                return false;

            blockId = chunk.blocks[local.x, local.y, local.z];
            if (blockId == 0)
                return false;

            Block.Block block = BlockRegistry.GetBlock(blockId);

            if (!block.isTransparent)
                return true;
            
            BlockStateContainer state = chunk.states[local.x, local.y, local.z];
            bool hasCollisionState = state != null &&
                                     (state.HasState(BlockStateKeys.HeightState) ||
                                      state.HasState(BlockStateKeys.WidthState));
            bool hasNonCubeShape = block.shapeIndex != (int)BlockShapes.Cube || block.isCentered;

            return hasCollisionState || hasNonCubeShape;
        }

        private bool TryGetBlockCollisionBounds(Chunk chunk, Vector3Int local, out Vector3 min, out Vector3 max)
        {
            min = Vector3.zero;
            max = Vector3.one;

            BlockStateContainer state = chunk.states[local.x, local.y, local.z];
            if (state == null || state.IsStateless())
                return true;

            float height = ParseState01(state, BlockStateKeys.HeightState, 1f);
            float width = ParseState01(state, BlockStateKeys.WidthState, 1f);

            if (height >= 0.999f && width >= 0.999f)
                return true;

            string facing = state.GetState(BlockStateKeys.DirectionalFacing);
            if (string.IsNullOrWhiteSpace(facing))
                facing = "up";

            switch (facing)
            {
                case "down":
                    min.y = 1f - height;
                    break;
                case "east":
                    min.x = 1f - height;
                    break;
                case "west":
                    max.x = height;
                    break;
                case "north":
                    min.z = 1f - height;
                    break;
                case "south":
                    max.z = height;
                    break;
                default:
                    max.y = height;
                    break;
            }
            
            if (width < 0.999f)
                ApplyCenteredWidth(facing, width, ref min, ref max);

            return true;
        }
        
        private static float ParseState01(BlockStateContainer state, string key, float fallback)
        {
            string raw = state.GetState(key);
            if (string.IsNullOrWhiteSpace(raw))
                return fallback;

            if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                return fallback;

            return Mathf.Clamp01(parsed);
        }

        private static void ApplyCenteredWidth(string facing, float width, ref Vector3 min, ref Vector3 max)
        {
            float inset = (1f - width) * 0.5f;
            float centeredMin = inset;
            float centeredMax = 1f - inset;

            switch (facing)
            {
                case "east":
                case "west":
                    min.y = Mathf.Max(min.y, centeredMin);
                    max.y = Mathf.Min(max.y, centeredMax);
                    min.z = Mathf.Max(min.z, centeredMin);
                    max.z = Mathf.Min(max.z, centeredMax);
                    break;
                case "north":
                case "south":
                    min.x = Mathf.Max(min.x, centeredMin);
                    max.x = Mathf.Min(max.x, centeredMax);
                    min.y = Mathf.Max(min.y, centeredMin);
                    max.y = Mathf.Min(max.y, centeredMax);
                    break;
                default:
                    min.x = Mathf.Max(min.x, centeredMin);
                    max.x = Mathf.Min(max.x, centeredMax);
                    min.z = Mathf.Max(min.z, centeredMin);
                    max.z = Mathf.Min(max.z, centeredMax);
                    break;
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
            
            if (!(meshQue.Count <= 0 && generationQue.Count <= 0 && transformQueue.Count <= 0))
            {
                visualChunksPerFrame = chunksPerFrame;
            }
            else
            {
                visualChunksPerFrame = 0;
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
            SaveSkyOcclusionMapIfDirty();
        }
        
        private void RecordSkyOcclusion(Vector3Int coord, byte[,,] blocks, bool rebuildIfChanged)
        {
            var changedColumns = new List<Vector2Int>();
            if (!skyOcclusionMap.UpdateChunk(coord, blocks, GetWorldHeight(), changedColumns))
                return;

            MarkSkyOcclusionDirty();
            if (!rebuildIfChanged)
                return;

            int size = Chunk.CHUNK_SIZE;
            foreach (Vector2Int local in changedColumns)
            {
                // Repair immediately below the changed chunk column. This removes
                // stale level-15 rays without rebuilding every loaded light voxel.
                Vector3Int repair = new Vector3Int(
                    coord.x * size + local.x, coord.y * size - 1, coord.z * size + local.y);
                if (GetChunkFromWorldPos(repair) != null && queuedSkyRepairs.Add(repair))
                    skyRepairQueue.Enqueue(repair);
            }
        }

        private bool HasKnownSkyOccluderAbove(int worldX, int worldY, int worldZ)
        {
            return skyOcclusionMap.HasOccluderAbove(worldX, worldY, worldZ);
        }

        private int GetWorldHeight() => World.Instance != null ? World.Instance.worldSizeY : 0;

        private void MarkSkyOcclusionDirty()
        {
            skyOcclusionDirty = true;
            skyOcclusionDirtyTime = Time.unscaledTime;
        }

        private void SaveSkyOcclusionMapAfterDelay()
        {
            if (skyOcclusionDirty && Time.unscaledTime - skyOcclusionDirtyTime >= SkyOcclusionSaveDelay)
                SaveSkyOcclusionMapIfDirty();
        }

        private void SaveSkyOcclusionMapIfDirty()
        {
            if (!skyOcclusionDirty)
                return;

            WorldSaveSystem.SaveSkyOcclusionMap(skyOcclusionMap);
            skyOcclusionDirty = false;
        }


        private (Dictionary<Vector3Int, byte[,,]> blocks, Dictionary<Vector3Int, BlockStateContainer[,,]> states) CaptureNeighborSnapshots(Vector3Int coord)
        {
            var blockDict = new Dictionary<Vector3Int, byte[,,]>();
            var stateDict = new Dictionary<Vector3Int, BlockStateContainer[,,]>();
            
            for (int ox = -1; ox <= 1; ox++)
            for (int oy = -1; oy <= 1; oy++)
            for (int oz = -1; oz <= 1; oz++)
            {
                Vector3Int nc = coord + new Vector3Int(ox, oy, oz);

                if (!chunks.TryGetValue(nc, out Chunk c) || c.blocks == null)
                    continue;

                // SNAPSHOT (important!)
                blockDict[nc] = (byte[,,])c.blocks.Clone();
                if (c.states != null)
                    stateDict[nc] = (BlockStateContainer[,,])c.states.Clone();
            }

            return (blockDict, stateDict);
        }

        private void ProcessLightingIntegration()
        {
            EnsureLightPropagator();
            int repairBudget = Mathf.Max(1, chunksPerFrame);
            bool repairedSkyLight = false;
            while (repairBudget-- > 0 && skyRepairQueue.Count > 0)
            {
                Vector3Int repair = skyRepairQueue.Dequeue();
                queuedSkyRepairs.Remove(repair);
                if (GetChunkFromWorldPos(repair) == null)
                    continue;

                lightPropagator.UpdateAfterVoxelChange(repair);
                repairedSkyLight = true;
            }
            if (repairedSkyLight)
                EnqueueChangedLightMeshes();

            int lim = chunksPerFrame / 2;
            int budget = Mathf.Max(1, lim);
            while (budget-- > 0 && lightQueue.Count > 0)
            {
                Chunk chunk = lightQueue.Dequeue();
                // A queued chunk may have been unloaded or its pooled shell reused
                // before reaching the front of the lighting queue.
                if (chunk?.blocks == null || GetChunk(chunk.coord) != chunk)
                    continue;

                lightPropagator.PropagateExistingSkyLight(GetSkyPropagationSeeds(chunk));
                lightPropagator.PropagateExistingBlockLight(GetBlockPropagationSeeds(chunk));
                EnqueueChangedLightMeshes();
            }
        }

        private static IEnumerable<Vector3Int> GetSkyPropagationSeeds(Chunk chunk)
        {
            int size = Chunk.CHUNK_SIZE;
            Vector3Int origin = chunk.coord * size;
            byte[,,] light = chunk.skyLight;
            if (light == null)
                yield break;

            for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            for (int z = 0; z < size; z++)
            {
                byte level = light[x, y, z];
                if (level == VoxelLight.Min)
                    continue;
                
                // Interior voxels surrounded by equal/brighter light cannot improve
                // anything. Only queue the chunk boundary and actual light/dark
                // frontiers, greatly shrinking the flood-fill's initial queue.
                bool boundary = x == 0 || x == size - 1 || y == 0 || y == size - 1 ||
                                z == 0 || z == size - 1;
                bool hasDarkerNeighbor = !boundary &&
                                         (light[x - 1, y, z] < level || light[x + 1, y, z] < level ||
                                          light[x, y - 1, z] < level || light[x, y + 1, z] < level ||
                                          light[x, y, z - 1] < level || light[x, y, z + 1] < level);
                if (boundary || hasDarkerNeighbor)
                    yield return origin + new Vector3Int(x, y, z);
            }
        }

        private IEnumerable<Vector3Int> GetBlockPropagationSeeds(Chunk chunk)
        {
            int size = Chunk.CHUNK_SIZE;
            Vector3Int origin = chunk.coord * size;

            // Include both sides of every chunk face. Light in an already-loaded
            // neighbor can then enter this chunk, while light restored in this
            // chunk can continue into its neighbors. Duplicate edge/corner seeds
            // are harmless and keep this boundary-only pass small.
            for (int a = 0; a < size; a++)
            for (int b = 0; b < size; b++)
            {
                yield return origin + new Vector3Int(0, a, b);
                yield return origin + new Vector3Int(-1, a, b);
                yield return origin + new Vector3Int(size - 1, a, b);
                yield return origin + new Vector3Int(size, a, b);

                yield return origin + new Vector3Int(a, 0, b);
                yield return origin + new Vector3Int(a, -1, b);
                yield return origin + new Vector3Int(a, size - 1, b);
                yield return origin + new Vector3Int(a, size, b);

                yield return origin + new Vector3Int(a, b, 0);
                yield return origin + new Vector3Int(a, b, -1);
                yield return origin + new Vector3Int(a, b, size - 1);
                yield return origin + new Vector3Int(a, b, size);
            }
        }

        
        private void EnsureLightPropagator()
        {
            if (lightPropagator != null)
                return;

            lightWorld = new ChunkLightWorld(this);
            lightPropagator = new LightPropagator(lightWorld);
        }
        
        private void EnqueueChangedLightMeshes()
        {
            foreach (Chunk changed in lightWorld.DrainTouchedChunks())
            {
                if (changed?.renderer == null)
                    continue;
                
                meshQue.Add(changed);
                foreach (Vector3Int direction in dirs)
                {
                    if (chunks.TryGetValue(changed.coord + direction, out Chunk neighbor) && neighbor?.renderer != null)
                        meshQue.Add(neighbor);
                }
            }
        }


        private sealed class ChunkLightWorld : LightPropagator.ILightWorld
        {
            private readonly ChunkManager manager;
            private readonly HashSet<Chunk> touchedChunks = new HashSet<Chunk>();
            private Dictionary<Chunk, byte[,,]> stagedSkyLight;

            public ChunkLightWorld(ChunkManager manager)
            {
                this.manager = manager;
            }

            public void BeginSkyRebuild()
            {
                stagedSkyLight = new Dictionary<Chunk, byte[,,]>(manager.chunks.Count);
                foreach (Chunk chunk in manager.chunks.Values)
                {
                    if (chunk?.blocks != null)
                        stagedSkyLight[chunk] = new byte[Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE];
                }
            }

            public void EndSkyRebuild()
            {
                foreach (KeyValuePair<Chunk, byte[,,]> entry in stagedSkyLight)
                {
                    if (!LightMapsEqual(entry.Key.skyLight, entry.Value))
                    {
                        entry.Key.skyLight = entry.Value;
                        touchedChunks.Add(entry.Key);
                    }
                }
                
                stagedSkyLight = null;
            }

            public IEnumerable<Chunk> DrainTouchedChunks()
            {
                Chunk[] result = touchedChunks.ToArray();
                touchedChunks.Clear();
                return result;
            }
            
            public IEnumerable<Vector3Int> GetSkyLightSeeds()
            {
                int size = Chunk.CHUNK_SIZE;
                foreach (Chunk chunk in manager.chunks.Values)
                {
                    if (chunk?.blocks == null)
                        continue;

                    int topY = chunk.coord.y * size + size - 1;
                    Vector3Int aboveCoord = chunk.coord + Vector3Int.up;
                    bool hasLoadedChunkAbove = manager.chunks.TryGetValue(aboveCoord, out Chunk above) &&
                                               above?.blocks != null;

                    for (int x = 0; x < size; x++)
                    for (int z = 0; z < size; z++)
                    {
                        if (hasLoadedChunkAbove || manager.HasKnownSkyOccluderAbove(
                                chunk.coord.x * size + x, topY, chunk.coord.z * size + z))
                            continue;

                        yield return new Vector3Int(
                            chunk.coord.x * size + x, topY, chunk.coord.z * size + z);
                    }

                }
            }
            
            public bool IsSkyLightSeed(Vector3Int position)
            {
                Chunk chunk = manager.GetChunkFromWorldPos(position);
                if (chunk?.blocks == null)
                    return false;

                int size = Chunk.CHUNK_SIZE;
                Vector3Int local = chunk.WorldToLocal(position);
                if (local.y != size - 1)
                    return false;

                bool hasLoadedChunkAbove = manager.chunks.TryGetValue(
                    chunk.coord + Vector3Int.up, out Chunk above) && above?.blocks != null;
                return !hasLoadedChunkAbove &&
                       !manager.HasKnownSkyOccluderAbove(position.x, position.y, position.z);
            }

            public void ClearLight(LightPropagator.Channel channel)
            {
                foreach (Chunk chunk in manager.chunks.Values)
                {
                    if (chunk?.blocks == null)
                        continue;

                    byte[,,] map;
                    if (channel == LightPropagator.Channel.Sky)
                    {
                        map = GetSkyMap(chunk);
                        if (map == null)
                        {
                            map = new byte[Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE];
                            if (stagedSkyLight != null)
                                stagedSkyLight[chunk] = map;
                            else
                                chunk.skyLight = map;
                        }
                    }
                    else
                    {
                        chunk.blockLight ??= new byte[Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE];
                        map = chunk.blockLight;
                    }

                    Array.Clear(map, 0, map.Length);
                }

            }
            
            public bool TryGetVoxel(Vector3Int position, out byte blockId)
            {
                Chunk chunk = manager.GetChunkFromWorldPos(position);
                if (chunk?.blocks == null)
                {
                    blockId = 0;
                    return false;
                }

                Vector3Int local = chunk.WorldToLocal(position);
                blockId = chunk.blocks[local.x, local.y, local.z];
                return true;
            }

            public byte GetLight(Vector3Int position, LightPropagator.Channel channel)
            {
                Chunk chunk = manager.GetChunkFromWorldPos(position);
                if (chunk == null)
                    return VoxelLight.Min;

                Vector3Int local = chunk.WorldToLocal(position);
                byte[,,] map = channel == LightPropagator.Channel.Sky
                    ? GetSkyMap(chunk)
                    : chunk.blockLight;
                return map?[local.x, local.y, local.z] ?? VoxelLight.Min;
            }
            
            public void SetLight(Vector3Int position, LightPropagator.Channel channel, byte value)
            {
                Chunk chunk = manager.GetChunkFromWorldPos(position);
                if (chunk == null)
                    return;

                Vector3Int local = chunk.WorldToLocal(position);
                if (channel == LightPropagator.Channel.Sky)
                {
                    byte[,,] map = GetSkyMap(chunk);
                    if (map == null)
                    {
                        chunk.skyLight = new byte[Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE];
                        map = chunk.skyLight;
                    }
                    
                    if (map[local.x, local.y, local.z] != value)
                    {
                        map[local.x, local.y, local.z] = value;
                        if (stagedSkyLight == null)
                            touchedChunks.Add(chunk);
                    }
                }
                else
                {
                    chunk.blockLight ??= new byte[Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE];
                    if (chunk.blockLight[local.x, local.y, local.z] != value)
                    {
                        chunk.blockLight[local.x, local.y, local.z] = value;
                        touchedChunks.Add(chunk);
                    }
                }
            }
            
            private byte[,,] GetSkyMap(Chunk chunk)
            {
                if (stagedSkyLight != null && stagedSkyLight.TryGetValue(chunk, out byte[,,] staged))
                    return staged;

                return chunk.skyLight;
            }

            private static bool LightMapsEqual(byte[,,] left, byte[,,] right)
            {
                if (left == null)
                    return false;

                for (int x = 0; x < Chunk.CHUNK_SIZE; x++)
                for (int y = 0; y < Chunk.CHUNK_SIZE; y++)
                for (int z = 0; z < Chunk.CHUNK_SIZE; z++)
                {
                    if (left[x, y, z] != right[x, y, z])
                        return false;
                }

                return true;
            }
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

        private void BuildChunkMesh(Chunk chunk)
        {
            if (chunk == null || chunk.renderer == null)
                return;
            
            chunk.renderer.Rebuild();
            chunk.isColliderDirty = false;
        }
        
        
        private void EnqueueNeighborRebuilds(Vector3Int coord)
        {
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

        private bool IsOutsideRenderDistance(Vector3Int coord)
        {
            return Mathf.Abs(coord.x - playerChunkCord.x) > viewDistance ||
                   Mathf.Abs(coord.y - playerChunkCord.y) > viewDistance ||
                   Mathf.Abs(coord.z - playerChunkCord.z) > viewDistance;
        }

        private static void InsertCandidate<T>(T item, float sqrDistance, T[] bestItems,
            float[] bestDistances, ref int count, ref int worstIndex)
        {
            if (bestItems.Length == 0)
                return;

            if (count < bestItems.Length)
            {
                bestItems[count] = item;
                bestDistances[count] = sqrDistance;
                count++;

                if (count == 1 || sqrDistance > bestDistances[worstIndex])
                {
                    worstIndex = count - 1;
                }
                
                return;
            }
            
            if (sqrDistance >= bestDistances[worstIndex])
                return;

            bestItems[worstIndex] = item;
            bestDistances[worstIndex] = sqrDistance;

            worstIndex = 0;
            for (int i = 1; i < count; i++)
            {
                if (bestDistances[i] > bestDistances[worstIndex])
                {
                    worstIndex = i;
                }
            }
        }

        private List<Vector3Int> TakeClosestGenerationCoords(int limit)
        {
            if (limit <= 0 || generationQue.Count == 0)
                return new List<Vector3Int>(0);

            Vector3Int[] bestCoords = new Vector3Int[limit];
            float[] bestDistances = new float[limit];
            int count = 0;
            int worstIndex = 0;

            List<Vector3Int> toRemove = null;

            foreach (var coord in generationQue)
            {
                if (IsOutsideRenderDistance(coord))
                {
                    toRemove ??= new List<Vector3Int>();
                    toRemove.Add(coord);
                    continue;
                }
                
                float dx = player.position.x - coord.x * Chunk.CHUNK_SIZE;
                float dy = player.position.y - coord.y * Chunk.CHUNK_SIZE;
                float dz = player.position.z - coord.z * Chunk.CHUNK_SIZE;
                float sqrDistance = dx * dx + dy * dy + dz * dz;

                InsertCandidate(coord, sqrDistance, bestCoords, bestDistances, ref count, ref worstIndex);
            }

            if (toRemove != null)
            {
                foreach (var coord in toRemove)
                {
                    generationQue.Remove(coord);
                }
            }

            List<Vector3Int> result = new List<Vector3Int>(count);
            for (int i = 0; i < count; i++)
            {
                result.Add(bestCoords[i]);
            }
            
            result.Sort((a, b) =>
            {
                float adx = player.position.x - a.x * Chunk.CHUNK_SIZE;
                float ady = player.position.y - a.y * Chunk.CHUNK_SIZE;
                float adz = player.position.z - a.z * Chunk.CHUNK_SIZE;
                float aSqr = adx * adx + ady * ady + adz * adz;

                float bdx = player.position.x - b.x * Chunk.CHUNK_SIZE;
                float bdy = player.position.y - b.y * Chunk.CHUNK_SIZE;
                float bdz = player.position.z - b.z * Chunk.CHUNK_SIZE;
                float bSqr = bdx * bdx + bdy * bdy + bdz * bdz;

                return aSqr.CompareTo(bSqr);
            });

            return result;
        }
        
        private List<Chunk> TakeClosestMeshChunks(int limit)
        {
            if (limit <= 0 || meshQue.Count == 0)
                return new List<Chunk>(0);

            Chunk[] bestChunks = new Chunk[limit];
            float[] bestDistances = new float[limit];
            int count = 0;
            int worstIndex = 0;

            List<Chunk> toRemove = null;

            foreach (var chunk in meshQue)
            {
                if (chunk == null || chunk.renderer == null || chunk.renderer.gameObject == null ||
                    IsOutsideRenderDistance(chunk.coord))
                {
                    toRemove ??= new List<Chunk>();
                    toRemove.Add(chunk);
                    continue;
                }

                float dx = player.position.x - chunk.coord.x * Chunk.CHUNK_SIZE;
                float dy = player.position.y - chunk.coord.y * Chunk.CHUNK_SIZE;
                float dz = player.position.z - chunk.coord.z * Chunk.CHUNK_SIZE;
                float sqrDistance = dx * dx + dy * dy + dz * dz;

                InsertCandidate(chunk, sqrDistance, bestChunks, bestDistances, ref count, ref worstIndex);
            }

            if (toRemove != null)
            {
                foreach (var chunk in toRemove)
                    meshQue.Remove(chunk);
            }

            List<Chunk> result = new List<Chunk>(count);
            for (int i = 0; i < count; i++)
                result.Add(bestChunks[i]);

            result.Sort((a, b) =>
            {
                float adx = player.position.x - a.coord.x * Chunk.CHUNK_SIZE;
                float ady = player.position.y - a.coord.y * Chunk.CHUNK_SIZE;
                float adz = player.position.z - a.coord.z * Chunk.CHUNK_SIZE;
                float aSqr = adx * adx + ady * ady + adz * adz;

                float bdx = player.position.x - b.coord.x * Chunk.CHUNK_SIZE;
                float bdy = player.position.y - b.coord.y * Chunk.CHUNK_SIZE;
                float bdz = player.position.z - b.coord.z * Chunk.CHUNK_SIZE;
                float bSqr = bdx * bdx + bdy * bdy + bdz * bdz;

                return aSqr.CompareTo(bSqr);
            });

            return result;
        }

        
        private void SortChunksLists()
        {
            // Generation QUE and sorting!
            
            int generatingChunksThisFrame = Mathf.Min(chunksPerFrame, generationQue.Count) * 3 + 2;

            List<Vector3Int> orderedGeneration = TakeClosestGenerationCoords(generatingChunksThisFrame);

            if (generationQue.Count > 0)
            {
                foreach (var coord in orderedGeneration)
                {
                    generationQue.Remove(coord);
                    EnqueueChunkDataRequest(coord);
                }
            }
            
            if (transformQueue.Count > 0)
            {
                int transformChunksThisFrame = Mathf.Min(chunksPerFrame, transformQueue.Count);
            
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
                        
                        //EnqueueNeighborRebuilds(t.chunk.coord);
                        // The transform does not change the meshing of neighbor chunks
                        // was needed before ill think
                    }
                }
            }

            // Build meshes from meshQue (distance prioritized)
            if (meshQue.Count > 0 && chunksPerFrame > 0)
            {
                int buildChunksThisFrame = Mathf.Min(chunksPerFrame, meshQue.Count);
                List<Chunk> sortedChunks = TakeClosestMeshChunks(buildChunksThisFrame);

                // Build closest chunks first
                for (int i = 0; i < sortedChunks.Count; i++)
                {
                    Chunk chunkToBuild = sortedChunks[i];
                    if (chunkToBuild != null && chunkToBuild.renderer.gameObject != null &&
                        chunkToBuild.renderer.gameObject.activeInHierarchy)
                    {
                        BuildChunkMesh(chunkToBuild);
                        
                        // Remove only when the chunk is scheduled for the rebuild.
                        // If inactive, (still waiting in trans que) keep it que
                        meshQue.Remove(chunkToBuild);
                    }
                }
            }
            //Debug.Log(generationQue.Count+ " " + meshQue.Count + " " + transformQueue.Count);
            //Debug.Log(meshQue);
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
