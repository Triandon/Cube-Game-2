using Unity.VisualScripting;
using UnityEngine;

public static class BiomeManager
{
    public static BiomeWeights GetBiomeWeights(int worldX, int worldZ)
    {
        //Base climates
        float temp = (WorldNoise.GetTemperature(worldX, worldZ) + 1f) * 0.5f;
        float hum = (WorldNoise.GetHumidity(worldX, worldZ) + 1f) * 0.5f;

        BiomeWeights bw;
        
        //This curves defines realism;
        bw.desert = Mathf.Clamp01((temp - 0.6f) * (1f - hum));
        bw.jungle = Mathf.Clamp01((temp - 0.6f) * hum);
        bw.tundra = Mathf.Clamp01((0.4f - temp) * (1f - hum));
        bw.forest = Mathf.Clamp01(hum * (1f - Mathf.Abs(temp - 0.5f)));
        bw.plains = Mathf.Clamp01(1f - Mathf.Max(bw.desert, bw.jungle, bw.tundra, bw.forest));
        
        return bw;
    }
}

public static class NoiseClass{
    public static float GetChunkSandChance(Vector3Int chunkCoord, int chunkSize)
    {
        // Sample from chunk center (IMPORTANT)
        int worldX = chunkCoord.x * chunkSize + chunkSize / 2;
        int worldZ = chunkCoord.z * chunkSize + chunkSize / 2;


        float noise = WorldNoise.GetTemperature(worldX, worldZ);
        return Mathf.Clamp01((noise + 1f) * 0.5f);
    }
}
