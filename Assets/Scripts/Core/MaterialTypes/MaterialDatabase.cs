using UnityEngine;

public static class MaterialDatabase
{
    public static MaterialProperties Air;
    
    // Soil
    public static MaterialProperties Dirt;
    public static MaterialProperties SandGranite;
    public static MaterialProperties Clay;
    
    // Organic
    public static MaterialProperties OrganicMatter;
    
    // Pebbles
    public static MaterialProperties PebblesGranite;
    
    // Rocks
    public static MaterialProperties Granite;
    public static MaterialProperties Quartz;
    public static MaterialProperties Mica;
    
    static MaterialDatabase()
    {
        RegisterMaterials();
    }

    private static void RegisterMaterials()
    {
        Air = new MaterialProperties(materialId:0, materialName: "Air", category: MaterialCategory.Air,  isContinuous:true);
        MaterialRegistry.Register(Air);
        
        // Soils
        Dirt = new MaterialProperties(materialId:1, materialName: "Dirt", category: MaterialCategory.Soil,  isContinuous:true);
        MaterialRegistry.Register(Dirt);
        
        SandGranite = new MaterialProperties(materialId:2, materialName: "Granite Sand", category: MaterialCategory.Soil,  isContinuous:true);
        MaterialRegistry.Register(SandGranite);
        
        Clay = new MaterialProperties(materialId:3, materialName: "Clay", category: MaterialCategory.Soil,  isContinuous:true);
        MaterialRegistry.Register(Clay);
        
        // Organic
        OrganicMatter = new MaterialProperties(materialId:4, materialName: "Organic Matter", category: MaterialCategory.Soil,  isContinuous:true);
        MaterialRegistry.Register(OrganicMatter);
        
        // Pebbles
        PebblesGranite = new MaterialProperties(materialId:5, materialName: "Granite Pebbles", category: MaterialCategory.Rock,  isContinuous:false, mergeTolerance:0.05f);
        MaterialRegistry.Register(PebblesGranite);
        
        // Rocks
        Granite = new MaterialProperties(materialId:6, materialName: "Granite", category: MaterialCategory.Rock,  isContinuous:false, mergeTolerance:0.05f);
        MaterialRegistry.Register(Granite);
        
        Quartz = new MaterialProperties(materialId:7, materialName: "Quartz", category: MaterialCategory.Rock,  isContinuous:false, mergeTolerance:0.05f);
        MaterialRegistry.Register(Quartz);
        
        Mica = new MaterialProperties(materialId:8, materialName: "Mica", category: MaterialCategory.Rock,  isContinuous:false, mergeTolerance:0.05f);
        MaterialRegistry.Register(Mica);
        
        Debug.Log("Blocks registered (static)");
    }

    public static int GetMaterial(MaterialProperties material) => material.materialId;
    
    public static void Init(){}
}
