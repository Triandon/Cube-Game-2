using Core.Block;
using UnityEngine;

namespace Core
{
    [System.Serializable]
    public static class TerrainGeneration
    {
        public const int CHUNK_SIZE = Chunk.CHUNK_SIZE;
    
        public static byte[,,] GenerateChunkBlocks(Vector3Int coord)
        {
            byte[,,] blocks = new byte[CHUNK_SIZE, CHUNK_SIZE, CHUNK_SIZE];

            for (int x = 0; x < CHUNK_SIZE; x++)
            for (int y = 0; y < CHUNK_SIZE; y++)
            for (int z = 0; z < CHUNK_SIZE; z++)
            {
                int wx = coord.x * CHUNK_SIZE + x;
                int wy = coord.y * CHUNK_SIZE + y;
                int wz = coord.z * CHUNK_SIZE + z;
                
            }

            return blocks;
        }


        public static byte SampleBlock(int worldX, int worldY, int worldZ, int height,
            byte surfaceBlock)
        {
            byte topBlock = surfaceBlock;
            
            if (worldY > height)
            {
                return 0; //Fills with air
            } 
            if (worldY == height)
            {
                return topBlock;
                        
            }
            if(worldY > height - 3)
            {
                return BlockDataBase.DirtBlock.id;
            }
            if (worldY == 0)
            {
                return BlockDataBase.StoneBlock.id;
            }
            
            return BlockDataBase.StoneBlock.id;
        }

        public static int SampleHeight(int worldX, int worldZ)
        {
            float baseNoise = Mathf.Max(
                WorldNoise.GetHeight(worldX * 0.01f, worldZ * 0.01f), 0f);

            float baseHeight = baseNoise * 64 + 25f;
            float detail = WorldNoise.GetHeight(worldX * 0.1f, worldZ * 0.1f) * 4f;
            int height = Mathf.FloorToInt(baseHeight + detail);

            return height;
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
