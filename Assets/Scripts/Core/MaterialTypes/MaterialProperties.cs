using UnityEngine;

public class MaterialProperties
{
    public int materialId;
    public string materialName;
    public MaterialCategory category;

    public bool isContinuous;
    public float mergeTolerance;
    public float grainSize;

    public MaterialProperties(int materialId, string materialName, MaterialCategory category,bool isContinuous, float mergeTolerance = 0.0f, float grainSize = 0.0f)
    {
        this.materialId = materialId;
        this.materialName = materialName;
        this.category = category;

        this.isContinuous = isContinuous;
        this.mergeTolerance = mergeTolerance;
        this.grainSize = grainSize;
    }
}
