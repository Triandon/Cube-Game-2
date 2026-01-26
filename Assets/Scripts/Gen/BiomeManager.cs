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
        int height)
    {
        float r = Hash(worldX,worldZ);
        float patch = PatchNoise(worldX, worldZ);

        //Nautrall start
        SurfaceWeights sw = new SurfaceWeights
        {
            grass = 1f,
            deadGrass = 1f,
            sand = 1f
        };
        
        // Apply climate rules (NOT if/else biomes)
        ApplyDesertRule(ref sw, climate, worldX, worldZ);
        ApplyGrassRule(ref sw, climate, worldX, worldZ);

        // Micro variation
        ApplyPatch(ref sw, patch);
        
        // Altitude effect (optional)
        sw.sand *= Mathf.InverseLerp(60f, 30f, height);
        
        sw.Normalize();
        
        // Roll
        if (r < sw.grass)
            return BlockDataBase.GrassBlock.id;

        r -= sw.grass;
        
        if (r < sw.sand)
            return BlockDataBase.SandStoneBlock.id;
        
        return BlockDataBase.DeadGrassBlock.id;
    }

    static void ApplyDesertRule(
        ref SurfaceWeights sw,
        ChunkClimate climate, int worldX, int worldZ)
    {
        // ---- DESERT STRENGTH ----
        // 0.25 = Sahara-like dryness
        // 0.40 = semi-arid edge
        float desertFactor = Mathf.InverseLerp(0.38f, 0.22f, climate.humidity);

        if (desertFactor <= 0f)
            return;

        // Soft
        sw.sand      *= Mathf.Lerp(1f, 14f, desertFactor);
        sw.deadGrass *= Mathf.Lerp(1f, 2.5f, desertFactor);
        sw.grass *= Mathf.Lerp(1f, 0.05f, desertFactor);
        
        //Hard
        if (climate.humidity < 0.25 + (Hash(worldX, worldZ) - 0.5f) * 0.1f)
        {
            sw.grass = 0f;
        }

    }
    
    static void ApplyGrassRule(ref SurfaceWeights sw, ChunkClimate climate,
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

    static void ApplyPatch(ref SurfaceWeights sw, float patch)
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



