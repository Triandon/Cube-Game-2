using UnityEngine;

public struct ChunkClimate
{
    public float temperature;
    public float humidity;
}

public struct SurfaceWeights
{
    public float grass;
    public float deadGrass;
    public float sand;


    public void Normalize()
    {
        float sum = grass + deadGrass + sand;
        if (sum <= 0f) return;

        grass /= sum;
        deadGrass /= sum;
        sand /= sum;
    }
}
