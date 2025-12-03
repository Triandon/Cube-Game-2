using UnityEngine;

public static class WorldNoise
{
    private static FastNoiseLite heightNoise = new FastNoiseLite(12345);
    private static FastNoiseLite tempNoise = new FastNoiseLite(54321);
    private static FastNoiseLite humidityNoise = new FastNoiseLite();

    static WorldNoise()
    {
        heightNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        heightNoise.SetFractalType(FastNoiseLite.FractalType.Ridged);
        heightNoise.SetFractalOctaves(6);
        heightNoise.SetFractalGain(0.5f);
        heightNoise.SetFrequency(0.1f);
        
        tempNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        tempNoise.SetFrequency(0.0008f);
        
        humidityNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        humidityNoise.SetFrequency(0.0008f);
    }

    public static float GetHeight(float x, float z)
    {
        return heightNoise.GetNoise(x, z);
    }
    
    public static float GetTemperature(int x, int z)
    {
        return tempNoise.GetNoise(x, z);
    }

    public static float GetHumidity(int x, int z)
    {
        return humidityNoise.GetNoise(x, z);
    }
}
