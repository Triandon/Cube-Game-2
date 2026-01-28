using System.Collections.Generic;
using Core;
using Core.Block;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;

public static class BiomeManager
{
    private const float BIOME_FLOOR = 0.00015f;

    public static byte ChooseSurfaceBlock(
        ChunkClimate climate,
        int worldX,
        int worldZ,
        int height, Vector3Int coord)
    {
        List<SurfaceCandidate> candidates = new();
        
        float sandChance  = DesertChance(climate);
        float dryGrassChance = SavannaChance(climate);
        float snowChance = TundraChance(climate);
        float grassChance = GrassChance(climate);

        if (sandChance > 0.001f)
        {
            candidates.Add(new SurfaceCandidate()
            {
                block = BlockDataBase.SandStoneBlock.id,
                weight = sandChance
            });
        }

        if (dryGrassChance > 0.001f)
        {
            candidates.Add(new SurfaceCandidate()
            {
                block = BlockDataBase.DeadGrassBlock.id,
                weight = dryGrassChance
            });
        }

        if (snowChance > 0.001f)
        {
            candidates.Add(new SurfaceCandidate()
            {
                block = BlockDataBase.SnowBlock.id,
                weight = snowChance
            });
        }

        if (grassChance > 0.001f)
        {
            candidates.Add(new SurfaceCandidate()
            {
                block = BlockDataBase.GrassBlock.id,
                weight = grassChance
            });
        }

        return SpinTheWheel(candidates, worldX, worldZ);
    }

    private static byte SpinTheWheel(
        List<SurfaceCandidate> candidates,
        int worldX,
        int worldZ)
    {
        float total = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            total += candidates[i].weight;
        }

        float roll = Hash(worldX, worldZ) * total;

        float acc = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            acc += candidates[i].weight;
            if (roll <= acc)
            {
                return candidates[i].block;
            }
        }

        if (candidates.Count == 0)
        {
            return BlockDataBase.GrassBlock.id;
        }

        return candidates[candidates.Count - 1].block;
    }
    
    private static float DesertChance(ChunkClimate climate)
    {
        float hum = climate.humidity;
        float temp = climate.temperature;


        float tHum = Mathf.InverseLerp(0.25f, 0.0f, hum);
        float tTemp = Mathf.InverseLerp(0.75f, 1.0f, temp);


        tHum = Mathf.Pow(Mathf.Clamp01(tHum), 2.2f);
        tTemp = Mathf.Pow(Mathf.Clamp01(tTemp), 2.0f);


        const float k = 10f;
        float desertHum = Mathf.Log(1f + k * tHum) / Mathf.Log(1f + k);
        float desertHeat = Mathf.Log(1f + k * tTemp) / Mathf.Log(1f + k);

        float chance = desertHeat * desertHum;
        
        return BIOME_FLOOR + chance; // 0 → 1
    }
    
    private static float SavannaChance(ChunkClimate climate)
    {
        float hum = climate.humidity;
        float temp = climate.temperature;


        // Hot
        float tTemp = Mathf.InverseLerp(0.45f, 0.65f, temp);
        tTemp = Mathf.Clamp01(tTemp);
        tTemp = Mathf.Pow(Mathf.Clamp01(tTemp), 2.5f);


        // Mid-humidity bell
        float tHum = Mathf.InverseLerp(0.05f, 0.45f, hum);
        tHum = Mathf.Pow(Mathf.Clamp01(tHum), 1.4f);


        float chance = tTemp * tHum;


        // Savanna should never dominate 100%
        return BIOME_FLOOR + chance;
    }
    
    private static float TundraChance(ChunkClimate climate)
    {
        float temp = climate.temperature;

        float t = Mathf.InverseLerp(0.35f, 15.0f, temp);
        t = Mathf.Clamp01(t);

        //Bias toward cold, removes edges.
        t = Mathf.Pow(t, 2.0f);

        const float k = 10f;
        
        float chance = Mathf.Log(1f + k * t) / Mathf.Log(1f + k);

        return BIOME_FLOOR + chance;
    }
    
    private static float GrassChance(ChunkClimate climate)
    {
        float hum = climate.humidity;
        float temp = climate.temperature;

        // Humidity: thrives above ~0.35
        float h = Mathf.InverseLerp(0.15f, 0.75f, hum);

        // Temperature: dislikes extreme cold & extreme heat
        float t = Mathf.InverseLerp(0.15f, 0.65f, temp);

        h = Mathf.Clamp01(h);
        t = Mathf.Clamp01(t);

        // Grass should be smooth and forgiving
        h = Mathf.Pow(h, 1.1f);
        t = Mathf.Pow(t, 1.0f);

        float chance = h * t;

        return BIOME_FLOOR + chance;
    }
    
    private static byte DesertBlock(int x, int z, float chance)
    {
        chance = Mathf.Max(chance, 0.15f);
        
        float rx =  0.8f * x + 0.6f * z;
        float rz = -0.6f * x + 0.8f * z;

        float noise =
            Hash((int)rx, (int)rz) * 0.6f +
            PatchNoise(x, z) * 0.4f;

        if (noise < chance)
            return BlockDataBase.SandStoneBlock.id;

        return BlockDataBase.DeadGrassBlock.id;
    }
    
    private static byte SavannaBlock(int x, int z, float chance)
    {
        chance = Mathf.Max(chance, 0.15f);
        
        float noise =
            Hash(x + 2000, z - 2000) * 0.5f +
            PatchNoise(x, z) * 0.5f;

        if (noise < chance)
            return BlockDataBase.DeadGrassBlock.id;

        return BlockDataBase.GrassBlock.id;
    }
    
    private static byte TundraBlock(int x, int z, float chance)
    {
        chance = Mathf.Max(chance, 0.15f);
        
        // Rotate + offset so tundra doesn't echo desert/savanna
        float rx =  0.8f * x + 0.6f * z + 4000;
        float rz = -0.6f * x + 0.8f * z - 4000;

        float noise =
            Hash((int)rx, (int)rz) * 0.6f +
            PatchNoise(x, z) * 0.4f;

        if (noise < chance)
            return BlockDataBase.SnowBlock.id;

        // Edge breakup
        return BlockDataBase.GrassBlock.id;
    }

    private static bool CanBlockSpawn(ChunkClimate climate, Block block)
    {
        float temp = climate.temperature;
        float hum = climate.humidity;

        if (block == BlockDataBase.SandStoneBlock)
        {
            return hum < 0.25f && temp > 0.75f;
        }

        if (temp < 0.25 && block == BlockDataBase.SnowBlock)
        {
            return true;
        }

        if (block == BlockDataBase.DeadGrassBlock)
        {
            return hum < 0.55f && temp > 0.60f;
        }

        return false;
    }

    private static Block DesertRule(
        ChunkClimate climate,
        int worldX,
        int worldZ
    )
    {
        float hum = climate.humidity;
        float temp = climate.temperature;

        // Hard cutoff

        // Normalize dryness: 0.25 → 0, 0.0 → 1
        float tHum = Mathf.InverseLerp(0.25f, 0.0f, hum);
        float tTemp = Mathf.InverseLerp(0.75f, 1.0f, temp);
        
        tHum = Mathf.Clamp01(tHum);
        tTemp = Mathf.Clamp01(tTemp);

        // Bias the start so it stays quiet near 0.25
        tHum = Mathf.Pow(tHum, 2.2f); // <-- THIS removes the gap
        tTemp = Mathf.Pow(tTemp, 2.0f);
        
        // Logarithmic growth
        const float k = 10f;
        float desertHum = Mathf.Log(1f + k * tHum) / Mathf.Log(1f + k);
        float desertHeat = Mathf.Log(1f + k * tTemp) / Mathf.Log(1f + k);

        float desertChance = desertHeat * desertHum;

        // Spatial coherence
            float noiseTest = Hash((int)(0.8f * worldX + 0.6f * worldX),
                (int)(-0.6f * worldX + 0.8f * worldZ)) * 0.6f; 

        if (noiseTest < desertChance)
            return BlockDataBase.SandStoneBlock;

        return BlockDataBase.DeadGrassBlock;
    }

    private static Block TundraRule(
        ChunkClimate climate,
        int worldX,
        int worldZ
    )
    {
        float temp = climate.temperature;

        // Hard cutoff

        // Normalize dryness: 0.25 → 0, 0.0 → 1
        float t = Mathf.InverseLerp(0.25f, 0.0f, temp);
        t = Mathf.Clamp01(t);

        // Bias the start so it stays quiet near 0.25
        t = Mathf.Pow(t, 2.2f); // <-- THIS removes the gap

        // Logarithmic growth
        const float k = 10f;
        float taigaChance = Mathf.Log(1f + k * t) / Mathf.Log(1f + k);

        // Spatial coherence
        float noise =
            Hash((int)(0.8f * worldX + 0.6f * worldX),
                (int)(-0.6f * worldX + 0.8f * worldZ)) * 0.6f +
        PatchNoise(worldX, worldZ) * 0.4f;

        if (noise < taigaChance)
            return BlockDataBase.SnowBlock;

        return BlockDataBase.GrassBlock;
    }

    private static Block SavannaRule(
        ChunkClimate climate,
        int worldX,
        int worldZ
    )
    {
        float hum = climate.humidity;
        float temp = climate.temperature;

        // Hard cutoff

        // Normalize dryness: 0.25 → 0, 0.0 → 1
        // Peak at ~0.42, fades toward dry & wet
        float tHum =
            1f - Mathf.Abs(hum - 0.42f) / 0.15f;
        float tTemp = Mathf.InverseLerp(0.95f, 0.6f, temp);
        
        tHum = Mathf.Clamp01(tHum);
        tTemp = Mathf.Clamp01(tTemp);

        // Bias the start so it stays quiet near 0.25
        tHum = Mathf.Pow(tHum, 1.6f); // <-- THIS removes the gap
        tTemp = Mathf.Pow(tTemp, 2.0f);
        
        // Logarithmic growth
        const float k = 10f;
        float savHum = Mathf.Log(1f + k * tHum) / Mathf.Log(1f + k);
        float savHeat = Mathf.Log(1f + k * tTemp) / Mathf.Log(1f + k);

        float desertChance = savHeat * savHum;

        float rx = 0.8f * worldX + 0.6f * worldZ;
        float rz = -0.6f * worldX + 0.8f * worldZ;
        
        // Spatial coherence
        float noise = Hash((int)rx, (int)rz) * 0.6f + PatchNoise(worldX, worldZ) * 0.4f; 

        if (noise < desertChance)
            return BlockDataBase.DeadGrassBlock;

        return BlockDataBase.GrassBlock;
    }

    public static ChunkClimate GetClimateAt(int worldX, int worldZ)
    {
        float temp = (WorldNoise.GetTemperature(worldX, worldZ) + 1f) * 0.5f;
        float hum = (WorldNoise.GetHumidity(worldX, worldZ) + 1f) * 0.5f;

        return new ChunkClimate()
        {
            temperature = temp,
            humidity = hum
        };
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



