using System.Collections.Generic;
using Core.Block;
using UnityEngine;
using Random = System.Random;

namespace Core
{
    public class Chunk : MonoBehaviour
    {
        public const int CHUNK_SIZE = 16;
    
        public Vector3Int coord;
        public ChunkManager chunkManager;

        public byte[,,] blocks;
        private ChunkMeshGenerator meshGenerator;

        public Dictionary<int, byte> changedBlocks = new Dictionary<int, byte>();
        public bool isDirty = false;
    
        private void Awake()
        {
            meshGenerator = new ChunkMeshGenerator();
        }
        

        public void BuildMesh()
        {
            GetComponent<ChunkRendering>().BuildChunkMesh();
        }

        public byte GetBlock(int x, int y, int z)
        {
            //If inside chunk, return directly
            if (x >= 0 && x < CHUNK_SIZE &&
                y >= 0 && y < CHUNK_SIZE &&
                z >= 0 && z < CHUNK_SIZE)
            {
                return blocks[x,y,z];
            }

            //Outside this chunks -> request from neightboor chunk
            int localX = x;
            int localY = y;
            int localZ = z;
            Vector3Int neighborCoord = coord;

            if (x < 0)
            {
                neighborCoord.x -= 1;
                localX = x + CHUNK_SIZE;
            }
            else if (x >= CHUNK_SIZE)
            {
                neighborCoord.x += 1;
                localX = x - CHUNK_SIZE;
            }

            if (y < 0)
            {
                neighborCoord.y -= 1;
                localY = y + CHUNK_SIZE;
            } 
            else if (y >= CHUNK_SIZE)
            {
                neighborCoord.y += 1;
                localY = y - CHUNK_SIZE;
            }
        
            if (z < 0)
            {
                neighborCoord.z -= 1;
                localZ = z + CHUNK_SIZE;
            } 
            else if (z >= CHUNK_SIZE)
            {
                neighborCoord.z += 1;
                localZ = z - CHUNK_SIZE;
            }

            if (neighborCoord == coord)
            {
                return blocks[localX, localY, localZ];
            }

            Chunk neighbor = chunkManager.GetChunk(neighborCoord);
            if (neighbor == null) return 0;

            if (!World.Instance.IsChunkInsideOfWorld(neighborCoord)) return 0;

            return neighbor.GetBlock(localX, localY, localZ);

        }

        public Block.Block GetBlockObjet(int x, int y, int z)
        {
            byte id = GetBlock(x, y, z);
            return BlockRegistry.GetBlock(id);
        }
    

        public Vector3Int WorldToLocal(Vector3Int worldPos)
        {
            int lx = worldPos.x - coord.x * CHUNK_SIZE;
            int ly = worldPos.y - coord.y * CHUNK_SIZE;
            int lz = worldPos.z - coord.z * CHUNK_SIZE;

            return new Vector3Int(lx, ly, lz);
        }


        public void SetBlockLocal(Vector3Int localPos, byte id)
        {
            int x = localPos.x;
            int y = localPos.y;
            int z = localPos.z;

            int index = PosToIndex(x, y, z);

            // Original block from worldgen
            byte original = GetGeneratedBlockAtLocal(localPos.x,localPos.y,localPos.z);

            if (id != original)
            {
                changedBlocks[index] = id;
            }
            else
            {
                // If player changed it back to worldgen value, remove from save
                changedBlocks.Remove(index);
            }

            blocks[x, y, z] = id;

            isDirty = true;

            // Mesh updates
            chunkManager.meshQue.Add(this);
            chunkManager.EnqueueNeighborUpdates(coord, localPos);
        }

        public bool IsAir(byte id)
        {
            if (id == 0)
            {
                return true;
            }
            return false;
        }

        public bool IsBlockAir(Block.Block block)
        {
            if (block.id == 0)
            {
                return true;
            }

            return false;
        }

        public void GenerateHeightMapData()
        {
            blocks = new byte[CHUNK_SIZE, CHUNK_SIZE, CHUNK_SIZE];
        
            for(int x = 0; x < CHUNK_SIZE; x++)
            for(int z = 0; z < CHUNK_SIZE; z++)
            {
                int worldX = coord.x * CHUNK_SIZE + x;
                int worldZ = coord.z * CHUNK_SIZE + z;
            
                // Multi-layer noise for realistic terrain
                float baseHeight = WorldNoise.GetHeight(worldX * 0.01f, worldZ * 0.01f) * 128; // Big hills, last nuber determs the height
                baseHeight = Mathf.Max(baseHeight, 0);
                baseHeight += 25f;
            
                float detail = WorldNoise.GetHeight(worldX * 0.1f, worldZ * 0.1f) * 4f;        // Small bumps
                int height = Mathf.FloorToInt(baseHeight + detail);

                for (int y = 0; y < CHUNK_SIZE; y++)
                {
                    int worldY = coord.y * CHUNK_SIZE + y;

                    if (worldY > height)
                    {
                        blocks[x, y, z] = 0; //Fills with air
                    } 
                    else if (worldY == height)
                    {
                        blocks[x, y, z] = BlockRegistry.GetBlock("Grass_Block").id;
                    }
                    else if(worldY > height - 3)
                    {
                        blocks[x, y, z] = BlockRegistry.GetBlock("Dirt_Block").id;
                    }
                    else
                    {
                        blocks[x, y, z] = BlockRegistry.GetBlock("Stone_Block").id;
                    }
                
                    if (worldY == 0)
                    {
                        blocks[x, y, z] = BlockRegistry.GetBlock("Stone_Block").id;
                    }
                }

            }
        
        }

        public static int PosToIndex(int x, int y, int z)
        {
            return x + CHUNK_SIZE * (y + CHUNK_SIZE * z);
        }

        public byte GetGeneratedBlockAtLocal(int x, int y, int z)
        {
            // Regenerate what the world generator would have produced
            int worldX = coord.x * CHUNK_SIZE + x;
            int worldY = coord.y * CHUNK_SIZE + y;
            int worldZ = coord.z * CHUNK_SIZE + z;
        
            float baseHeight = WorldNoise.GetHeight(worldX * 0.01f, worldZ * 0.01f) * 64;
            baseHeight = Mathf.Max(baseHeight, 0);
            baseHeight += 25f;

            float detail = WorldNoise.GetHeight(worldX * 0.1f, worldZ * 0.1f) * 4f;
            int height = Mathf.FloorToInt(baseHeight + detail);

            if (worldY > height) return 0;
            if (worldY == height) return BlockRegistry.GetBlock("Grass_Block").id;
            if (worldY > height - 3) return BlockRegistry.GetBlock("Dirt_Block").id;
        
            return BlockRegistry.GetBlock("Stone_Block").id;
        }
    

    }
}
