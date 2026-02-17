using UnityEngine;

public class MaterialProperties
{
    public int materialId;
    public string materialName;
    public MaterialCategory category;

    public float grainSize;

    public MaterialProperties(int materialId, string materialName, MaterialCategory category, float grainSize = 0.0f)
    {
        this.materialId = materialId;
        this.materialName = materialName;
        this.category = category;

        this.grainSize = grainSize;
    }
}
