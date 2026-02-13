using System.Collections.Generic;
using Core.Block;
using UnityEngine;
using Random = System.Random;

namespace Core
{
    public class Chunk
    {
        //Chunk
        public const int CHUNK_SIZE = 16;
    
        public Vector3Int coord;
        public byte[,,] blocks;
        public BlockStateContainer[,,] states;
        public Dictionary<Vector3Int, InventoryHolder> blockEntities = new Dictionary<Vector3Int, InventoryHolder>();
        
        public bool isDirty = false;
        public bool isColliderDirty = false;
        public int chunkNumber;
        
        public ChunkManager chunkManager;
        public ChunkRendering renderer;
        public MeshData meshData;
        public ChunkLOD lod;

        public Chunk(Vector3Int coord)
        {
            this.coord = coord;
            blocks = new byte[CHUNK_SIZE, CHUNK_SIZE, CHUNK_SIZE];

            states = new BlockStateContainer[CHUNK_SIZE, CHUNK_SIZE, CHUNK_SIZE];
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

            if (!World.Instance.IsChunkInsideOfWorld(neighborCoord)) return 0;
            
            Chunk neighbor = chunkManager.GetChunk(neighborCoord);
            if (neighbor == null) return 0;

            

            return neighbor.GetBlock(localX, localY, localZ);

        }

        public BlockStateContainer GetStateAt(int x, int y, int z)
        {
            if (x < 0 || y < 0 || z < 0 || x >= CHUNK_SIZE || y >= CHUNK_SIZE || z >= CHUNK_SIZE) 
                return null;
            return states[x, y, z];
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
            SetBlockLocal(localPos, id, null);
            
        }

        public void SetBlockLocal(Vector3Int localPos, byte id, BlockStateContainer state)
        {
            int x = localPos.x;
            int y = localPos.y;
            int z = localPos.z;

            blocks[x, y, z] = id;

            if (state != null && !state.IsStateless())
            {
                states[x, y, z] = state;
            }
            else
            {
                states[x, y, z] = null;
            }

            isDirty = true;
            isColliderDirty = true;

            if (chunkManager != null)
            {
                // Mesh updates
                chunkManager.meshQue.Add(this);
                
                //ask chunk manager to also add neighbto chunks if it was on the border change
                chunkManager.EnqueueNeighborUpdates(coord,localPos);
            }
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
        

        public static int PosToIndex(int x, int y, int z)
        {
            return x + CHUNK_SIZE * (y + CHUNK_SIZE * z);
        }
        
        public enum ChunkLOD : byte
        {
            LOD0 = 0, // 1x1
            LOD1 = 1, // 2x2
            LOD2 = 2, // 4x4
            LOD3 = 3, // 8x8
            LOD4 = 4  // 16x16
        }

        public int GetLodScale()
        {
            return 1 << (int)lod;
        }

    }
    
}


