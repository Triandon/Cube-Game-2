using Core.Block;
using UnityEngine;

namespace Core
{
    [System.Serializable]
    public class TerrainGeneration
    {
        public const int CHUNK_SIZE = 16;
    
        public byte[,,] GenerateHeightMapData(Vector3Int coord)
        {
            byte[,,] blocks = new byte[CHUNK_SIZE, CHUNK_SIZE, CHUNK_SIZE];

            byte grass = BlockDataBase.GrassBlock.id;
            byte dirt = BlockDataBase.GetBlock(BlockDataBase.DirtBlock);
            byte stone = BlockDataBase.StoneBlock.id;

        
            for(int x = 0; x < CHUNK_SIZE; x++)
            for(int z = 0; z < CHUNK_SIZE; z++)
            {
                int worldX = coord.x * CHUNK_SIZE + x;
                int worldZ = coord.z * CHUNK_SIZE + z;
            
                // Multi-layer noise for realistic terrain
                float baseHeight = WorldNoise.GetHeight(worldX * 0.01f, worldZ * 0.01f) * 64; // Big hills
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
                        blocks[x, y, z] = grass;
                    }
                    else if(worldY > height - 3)
                    {
                        blocks[x, y, z] = dirt;
                    }
                    else
                    {
                        blocks[x, y, z] = stone;
                    }
                
                    if (worldY == 0)
                    {
                        blocks[x, y, z] = stone;
                    }
                }

            }

            return blocks;
        }
    }
}
