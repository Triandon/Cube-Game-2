using UnityEngine;

public static class MaterialDatabase
{
    public static MaterialProperties GrassMaterial;
    public static MaterialProperties GraniteMaterial;
    
    static MaterialDatabase()
    {
        RegisterMaterials();
    }

    private static void RegisterMaterials()
    {
        GrassMaterial = new MaterialProperties(1, "Dirt", MaterialCategory.Earth, 0.3f);
        MaterialRegistry.Register(GrassMaterial);
        
        Debug.Log("Blocks registered (static)");
    }

    public static int GetMaterial(MaterialProperties material) => material.materialId;
    
    public static void Init(){}
}
