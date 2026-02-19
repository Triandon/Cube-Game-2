using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
[System.Serializable]
public class CompositionLogic
{
    [SerializeField]
    private List<MaterialEntry> entries = new List<MaterialEntry>();
    
    //% add up to 1.0
    public Dictionary<int, float> contents =>
        entries.ToDictionary(e => e.materialId, e => e.amount);

    [System.Serializable]
    public struct MaterialEntry
    {
        public int materialId; 
        public float amount;

        public MaterialEntry(int materialId, float amount)
        {
            this.materialId = materialId;
            this.amount = amount;
        }
    }

    public void AddLogic(int materialId, float amount)
    {
        var index = entries.FindIndex(e => e.materialId == materialId);

        if (index >= 0)
        {
            var e = entries[index];
            e.amount += amount;
            entries[index] = e;
        }
        else
        {
            entries.Add(new MaterialEntry(materialId, amount));
        }
        
    }

    public static CompositionLogic Add(params (int materialId, float amount)[] materials)
    {
        CompositionLogic comp = new CompositionLogic();

        foreach (var m in materials)
        {
            comp.AddLogic(m.materialId, m.amount);
        }
        
        comp.Normalize();
        return comp;
    }
    
    public void Normalize()
    {
        float total = entries.Sum(e => e.amount);
        if (total <= 0f) return;
        
        //Cheks if content is 100%
        if (total < 1f)
        {
            float airAmount = 1f - total;
            AddLogic(0, airAmount);
            total = 1f;
        }

        //Normalize to 1.0
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            e.amount /= total;
            entries[i] = e;
        }
    }
    
    public static CompositionLogic Combine(
        CompositionLogic a, float aWeight,
        CompositionLogic b, float bWeight)
    {
        CompositionLogic result = new CompositionLogic();

        foreach (var e in a.entries)
            result.AddLogic(e.materialId, e.amount * aWeight);

        foreach (var e in b.entries)
            result.AddLogic(e.materialId, e.amount * bWeight);

        result.Normalize();
        return result;
    }

    public override string ToString()
    {
        if (entries == null || entries.Count == 0)
            return "No Composition";

        var sorted = entries.OrderByDescending(e => e.amount);

        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        foreach (var e in sorted)
        {
            var mat = MaterialRegistry.GetMaterial(e.materialId);
            string name = mat != null ? mat.materialName : "Unknown";
            
            sb.AppendLine($"{name}: {(e.amount * 100f):F1}%");
        }

        return sb.ToString();
    }

    public CompositionLogic Clone()
    {
        CompositionLogic copy = new CompositionLogic();
        foreach (var e in entries)
        {
            copy.AddLogic(e.materialId, e.amount);
        }
        copy.Normalize();
        return copy;
    }

    public bool IsWithinTolerance(CompositionLogic other, float tolerance)
    {
        if (other == null) return false;

        var dictA = this.contents;
        var dictB = other.contents;
        
        // Check union of material ids
        var allKeys = new HashSet<int>(dictA.Keys);
        allKeys.UnionWith(dictB.Keys);

        foreach (var key in allKeys)
        {
            float aVal = dictA.ContainsKey(key) ? dictA[key] : 0f;
            float bVal = dictB.ContainsKey(key) ? dictB[key] : 0f;

            if (Mathf.Abs(aVal - bVal) > tolerance)
                return false;
        }

        return true;
    }
}

public static class CompositionGenerator
{
    public static CompositionLogic GenerateRandom()
    {
        CompositionLogic comp = new CompositionLogic();
        
        //Random
        // Add random raw values
        comp.AddLogic(MaterialDatabase.Dirt.materialId, Random.Range(0f, 1f));
        comp.AddLogic(MaterialDatabase.Clay.materialId, Random.Range(0f, 1f));
        comp.AddLogic(MaterialDatabase.SandGranite.materialId, Random.Range(0f, 1f));
        
        comp.Normalize();

        return comp;
    }
}
