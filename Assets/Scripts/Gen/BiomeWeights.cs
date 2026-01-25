using UnityEngine;

public struct BiomeWeights
{
    public float desert;
    public float plains;
    public float forest;
    public float jungle;
    public float tundra;

    public void Normalize()
    {
        float sum = desert + plains + forest + jungle + tundra;
        if(sum <= 0f) return;

        desert /= sum;
        plains /= sum;
        forest /= sum;
        jungle /= sum;
        tundra /= sum;
    }
}
