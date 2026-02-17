using System.Collections.Generic;
using UnityEngine;

public static class MaterialRegistry
{
    private static Dictionary<int, MaterialProperties> materials = new Dictionary<int, MaterialProperties>();

    public static void Register(MaterialProperties material)
    {
        if (!materials.ContainsKey(material.materialId))
        {
            materials.Add(material.materialId, material);
            
            Debug.Log($"Registered item: {material.materialName} (ID: {material.materialId})");
        }
        else
        {
            Debug.LogWarning($"Item ID {material.materialId} already registered!");
        }
    }

    public static MaterialProperties GetMaterial(int id)
    {
        materials.TryGetValue(id, out var mat);
        return mat;
    }
}
