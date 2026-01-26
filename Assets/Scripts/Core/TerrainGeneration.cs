using Core.Block;
using UnityEngine;

namespace Core
{
    [System.Serializable]
    public static class TerrainGeneration
    {
        public const int CHUNK_SIZE = 16;
    
        public static byte[,,] GenerateChunkBlocks(Vector3Int coord)
        {
            byte[,,] blocks = new byte[CHUNK_SIZE, CHUNK_SIZE, CHUNK_SIZE];

            byte grass = BlockDataBase.GrassBlock.id;
            byte dirt = BlockDataBase.GetBlock(BlockDataBase.DirtBlock);
            byte stone = BlockDataBase.StoneBlock.id;

            ChunkClimate climate = BiomeManager.GetChunkClimate(coord);
            
            for(int x = 0; x < CHUNK_SIZE; x++)
            for(int z = 0; z < CHUNK_SIZE; z++)
            {
                int worldX = coord.x * CHUNK_SIZE + x;
                int worldZ = coord.z * CHUNK_SIZE + z;
                
                // Multi-layer noise for realistic terrain
                
                //Base noise values
                float baseNoise = Mathf.Max(WorldNoise.GetHeight(worldX * 0.01f, worldZ * 0.01f), 0);
                //Different biomes can have different height

                float baseHeight = baseNoise * 64 + 25f;
                
                float detail = WorldNoise.GetHeight(worldX * 0.1f, worldZ * 0.1f) * 4f;        // Small bumps
                int height = Mathf.FloorToInt(baseHeight + detail);
                
                //What top block to have
                byte topBlock = BiomeManager.ChooseSurfaceBlock(climate, worldX, worldZ, height,coord);
                
                for (int y = 0; y < CHUNK_SIZE; y++)
                {
                    int worldY = coord.y * CHUNK_SIZE + y;

                    if (worldY > height)
                    {
                        blocks[x, y, z] = 0; //Fills with air
                    } 
                    else if (worldY == height)
                    {
                        blocks[x, y, z] = topBlock;
                        
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
        

        private static float Hash(int x, int z)
        {
            uint h = (uint)(x * 374761393 + z * 668265263);
            h = (h ^ (h >> 13)) * 1274126177;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }

        private static float BiomeBias(float bw, float bias)
        {
            //bias > 1 boost dominance, < 1 softens
            return Mathf.Pow(bw, bias);
        }

        private static float AltitudeSnowFactor(int height)
        {
            const float start = 35f; // snow begins
            const float full = 65f; // fully snow

            if (height <= start) return 0f;
            if (height >= full) return 1f;

            return Mathf.InverseLerp(start, full, height);
        }

        private static bool IsDominant(float bw, params float[] others)
        {
            float maxOther = 0f;
            for (int i = 0; i < others.Length; i++)
            {
                maxOther = Mathf.Max(maxOther, others[i]);
            }

            return bw > maxOther * 1.15f; //dominance factor
        }

        private static float PatchNoise(int worldX, int worldZ)
        {
            return (WorldNoise.GetHeight(worldX * 0.03f, worldZ * 0.03f) + 1f) * 0.5f;
        }
        
    }
    
}
