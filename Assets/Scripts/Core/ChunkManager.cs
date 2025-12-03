using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        //How many chunks should be building at once.
        //The amount that is currently building.
        public int chunksPerFrame = 4;
        private FPSCounter fpsCounter;
        private int fps;
        public bool dynamicChunkRendering = true;
        
        public int initialPoolSize = 20; // pre-instantiate this many chunks

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
            UpdateFPS();
        
            Vector3Int currentPlayerChunk = GetPlayerChunkCoord();

            if (currentPlayerChunk != playerChunkCord)
            {
                playerChunkCord = currentPlayerChunk;
                UpdateChunks();
            }
        
            // Remove null/destroyed chunks first 
            meshQue.RemoveWhere(c => c == null || c.gameObject == null);
            // Sort the mesh queue based on distance to player
            if (meshQue.Count > 0)
            {
                List<Chunk> sortedChunks = meshQue.OrderBy(c => Vector3.Distance(player.position, c.transform.position)).ToList();
                
                int buildChunksThisFrame = Mathf.Min(chunksPerFrame, meshQue.Count);
                for (int i = buildChunksThisFrame - 1; i >= 0; i--)
                {
                    Chunk chunkToBuild = sortedChunks[i];
                    if (chunkToBuild != null && chunkToBuild.gameObject.activeSelf)
                    {
                        StartCoroutine(BuildChunkMeshNextFrame(chunkToBuild));
                    }
                    meshQue.Remove(chunkToBuild);
                }
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
                Vector3Int chunkCord = new Vector3Int(playerChunkCord.x + x, playerChunkCord.y + y,playerChunkCord.z + z);
            
                if (!chunks.ContainsKey(chunkCord))
                {
                    chunkCount++;
                    Chunk chunk = GenerateChunk(chunkCord, chunkCount);
                    newChunks.Add(chunk);
                }
            }

            foreach (Chunk chunk in newChunks)
            {
                StartCoroutine(BuildChunkMeshNextFrame(chunk));
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
                        GenerateChunk(logicalCoord,chunkCount);
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
                RemoveChunk(chunk,key);
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
            
            chunks.Add(coord,chunk);
            chunk.GenerateHeightMapData();
            
            if (WorldSaveSystem.ChunkSaveExist(coord))
            { 
                Dictionary<int, byte> savedChanges = WorldSaveSystem.LoadChunk(coord);
                
                // Apply modifications:
                foreach (var kv in savedChanges)
                {
                    int index = kv.Key;
                    byte id = kv.Value;

                    int x = index % Chunk.CHUNK_SIZE;
                    int y = (index / Chunk.CHUNK_SIZE) % Chunk.CHUNK_SIZE;
                    int z = index / (Chunk.CHUNK_SIZE * Chunk.CHUNK_SIZE);

                    chunk.blocks[x, y, z] = id;
                    
                    // Keep dirty tracking consistent (optional)
                    chunk.changedBlocks[index] = id;
                }
                
                //mark chunk dirty if there were saved changes
                if (chunk.changedBlocks.Count > 0)
                {
                    chunk.isDirty = true;
                }

            }
            
            meshQue.Add(chunk);
            QueChinkAndNeighborsToRebuild(coord);

            return chunk;
        }

        private void RemoveChunk(Chunk chunk, Vector3Int coord)
        {
            if (chunk.isDirty)
            {
                WorldSaveSystem.SaveChunk(coord,chunk.changedBlocks);
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


        public void QueChinkAndNeighborsToRebuild(Vector3Int coord)
        {
            Vector3Int[] neighbors =
            {
                coord + Vector3Int.right, // +X
                coord + Vector3Int.left, // -X
                coord + Vector3Int.forward, // +Z
                coord + Vector3Int.back, // -Z
                coord + Vector3Int.up,
                coord + Vector3Int.down
            };

            //Main Chunk
            if (chunks.TryGetValue(coord, out Chunk chunk))
            {
                meshQue.Add(chunk);
            }

            foreach (var n in neighbors)
            {
                if (chunks.TryGetValue(n, out Chunk i))
                {
                    meshQue.Add(i);
                }
            }
        }

        public Chunk GetChunk(Vector3Int coord)
        {
            chunks.TryGetValue(coord, out var c);
            return c;
        }

        private IEnumerator BuildChunkMeshNextFrame(Chunk chunk)
        {
            yield return null;

            if (chunk != null && chunk.gameObject != null)
            {
                chunk.BuildMesh();
            }
        
        }

        private List<Vector3Int> GetNeighborCoords(Vector3Int coord)
        {
            return new List<Vector3Int>
            {
                coord + Vector3Int.right,
                coord + Vector3Int.left,
                coord + Vector3Int.up,
                coord + Vector3Int.down,
                coord + new Vector3Int(0,0,1),
                coord + new Vector3Int(0,0,-1)
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
    
        public void EnqueueNeighborUpdates(Vector3Int coord, Vector3Int localPos)
        {
            if (localPos.x == 0)              AddIfExists(coord + Vector3Int.left);
            if (localPos.x == Chunk.CHUNK_SIZE - 1) AddIfExists(coord + Vector3Int.right);
        
            if(localPos.y == 0) AddIfExists(coord + Vector3Int.down);
            if(localPos.y == Chunk.CHUNK_SIZE - 1) AddIfExists(coord + Vector3Int.up);

            if (localPos.z == 0)              AddIfExists(coord + new Vector3Int(0,0,-1));
            if (localPos.z == Chunk.CHUNK_SIZE - 1) AddIfExists(coord + new Vector3Int(0,0,1));
        }

        private void AddIfExists(Vector3Int c)
        {
            if (chunks.TryGetValue(c, out Chunk n))
                meshQue.Add(n);
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


        private void UpdateFPS()
        {
            fps = fpsCounter.CurrentFPS;
        
            if(!dynamicChunkRendering) return;
        
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

            if (meshQue.Count <= 0)
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
                    WorldSaveSystem.SaveChunk(chunk.coord,chunk.changedBlocks);
                    chunk.isDirty = false;
                }
            }
            Debug.Log("World saved successfully!");
        }
    
    }
    
    
}
