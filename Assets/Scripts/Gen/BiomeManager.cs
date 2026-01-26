using Core;
using Core.Block;
using Unity.VisualScripting;
using UnityEngine;

public static class BiomeManager
{

    public static byte ChooseSurfaceBlock(
        ChunkClimate climate,
        int worldX,
        int worldZ,
        int height, Vector3Int coord)
    {
        float r = Hash(worldX,worldZ);
        float patch = PatchNoise(worldX, worldZ);

        bool sandSpawn = CanBlockSpawn(climate, BlockDataBase.SandStoneBlock);

        if (sandSpawn)
        {
            return DesertRule(climate,worldX, worldZ).id;
        }

        return BlockDataBase.GrassBlock.id;
    }

    private static bool CanBlockSpawn(ChunkClimate climate, Block block)
    {
        
        float hum = climate.humidity;

        if (hum > 0.25 && block == BlockDataBase.SandStoneBlock)
        {
            return false;
        }

        return true;
    }

    private static Block DesertRule(
        ChunkClimate climate,
        int worldX,
        int worldZ
    )
    {
        float hum = climate.humidity;

        // Hard cutoff

        // Normalize dryness: 0.25 → 0, 0.0 → 1
        float t = Mathf.InverseLerp(0.25f, 0.0f, hum);
        t = Mathf.Clamp01(t);

        // Bias the start so it stays quiet near 0.25
        t = Mathf.Pow(t, 2.2f); // <-- THIS removes the gap

        // Logarithmic growth
        const float k = 10f;
        float desertChance = Mathf.Log(1f + k * t) / Mathf.Log(1f + k);

        // Spatial coherence
        float noise =
            Hash(worldX, worldZ) * 0.6f; //+
            //PatchNoise(worldX, worldZ) * 0.4f;

        if (noise < desertChance)
            return BlockDataBase.SandStoneBlock;

        return BlockDataBase.DeadGrassBlock;
    }
    
    private static void ApplyDesertRule(
        ref SurfaceWeights sw,
        ChunkClimate climate, int worldX, int worldZ)
    {
        float random = (Hash(worldX, worldZ) - 0.5f) * 0.1f;
        // ---- DESERT STRENGTH ----
        // 0.25 = Sahara-like dryness
        // 0.40 = semi-arid edge
        float desertFactor = Mathf.InverseLerp(0.38f, 0.22f, climate.humidity);
        desertFactor = desertFactor * desertFactor * (3f - 2f * desertFactor);

        if (desertFactor <= 0f)
            return;

        
        
        // Soft
        sw.sand      *= Mathf.Lerp(1f, 14f, desertFactor);
        sw.deadGrass *= Mathf.Lerp(1f, 2.5f, desertFactor);
        sw.grass *= Mathf.Lerp(1f, 0.05f, desertFactor);
        
        //Hard
        if (climate.humidity < 0.25 + random)
        {
            sw.grass = 0f;
        }

    }
    
    private static void ApplyGrassRule(ref SurfaceWeights sw, ChunkClimate climate,
        int worldX, int worldZ)
    {
        float humidityFactor = Mathf.InverseLerp(0.45f, 0.75f, climate.humidity);
        float coolFactor     = Mathf.InverseLerp(0.75f, 0.35f, climate.temperature);

        float grassFactor = humidityFactor * coolFactor;

        float random = (Hash(worldX, worldZ) - 0.5f) * 0.1f;

        //Soft
        if (grassFactor > 0f)
        {
            sw.grass     *= Mathf.Lerp(1f, 10f, grassFactor);
            sw.deadGrass *= Mathf.Lerp(1f, 0.67f, grassFactor);
            sw.sand      *= Mathf.Lerp(1f, 0.05f, grassFactor);
        }
        
        //Hard
        if (climate.humidity > 0.25 + random)
        {
            sw.sand = 0f;

            if (climate.humidity < 0.29f && climate.humidity > 0.25)
            {
                sw.sand = 0.1f;
            }
        }

        // Hot + humid → dead grass
        float hotFactor = Mathf.InverseLerp(0.6f, 0.9f, climate.temperature);
        float wetFactor = Mathf.InverseLerp(0.6f, 0.85f, climate.humidity);
        float hotWet = hotFactor * wetFactor;

        if (hotWet > 0f)
        {
            sw.deadGrass *= Mathf.Lerp(1f, 4.0f, hotWet);
            sw.grass     *= Mathf.Lerp(1f, 0.4f, hotWet);
        }

        if (climate.humidity > 0.75 + random && climate.temperature < 0.25 + random)
        {
            sw.deadGrass = 0f;
        }
    }

    private static void ApplyPatch(ref SurfaceWeights sw, float patch)
    {
        sw.grass     *= Mathf.Lerp(0.7f, 1.3f, patch);
        sw.deadGrass *= Mathf.Lerp(0.8f, 1.2f, patch);
        sw.sand      *= Mathf.Lerp(0.9f, 1.1f, patch);
    }
    
    public static ChunkClimate GetChunkClimate(Vector3Int coord)
    {
        int worldX = coord.x * Core.TerrainGeneration.CHUNK_SIZE
                     + Core.TerrainGeneration.CHUNK_SIZE / 2;
        int worldZ = coord.z * Core.TerrainGeneration.CHUNK_SIZE
                     + Core.TerrainGeneration.CHUNK_SIZE / 2;
        
        float temp = (WorldNoise.GetTemperature(worldX, worldZ) + 1f) * 0.5f;
        float hum = (WorldNoise.GetHumidity(worldX, worldZ) + 1f) * 0.5f;

        return new ChunkClimate
        {
            temperature = temp,
            humidity = hum,
        };
    }
    
    private static float Hash(int x, int z)
    {
        uint h = (uint)(x * 374761393 + z * 668265263);
        h = (h ^ (h >> 13)) * 1274126177;
        return (h & 0xFFFFFF) / (float)0x1000000;
    }
    
    private static float PatchNoise(int worldX, int worldZ)
    {
        return (WorldNoise.GetHeight(worldX * 0.03f, worldZ * 0.03f) + 1f) * 0.5f;
    }
}



