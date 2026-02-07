using System;
using Core;
using UnityEngine;

[RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
public class ChunkRendering : MonoBehaviour
{
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;
    private Chunk chunk;
    private ChunkMeshGenerator meshGenerator;
    private MaterialPropertyBlock mpb;

    public struct ChunkMeshData
    {
        public Mesh renderingMesh;
        public Mesh colliderMesh;
    }

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        
        meshGenerator = new ChunkMeshGenerator();

        mpb = new MaterialPropertyBlock();
    }

    public void SetChunkData(Chunk chunkData)
    {
        chunk = chunkData;
    }

    public void BuildChunkMesh()
    {
        if (chunk == null || chunk.blocks == null) return;

        // Generate both render mesh & collider mesh
        var meshData = meshGenerator.GenerateMesh(chunk.blocks, chunk);

        // Assign render mesh to MeshFilter
        meshFilter.sharedMesh = meshData.renderingMesh;

        // Assign collider mesh to MeshCollider
        //meshCollider.sharedMesh = meshData.colliderMesh;
        
        // Material
        meshRenderer.sharedMaterial = Resources.Load<Material>("Materials/AtlasMaterial");
    }

    // modified chunk of your ChunkRendering class
    public void ApplyMeshData(MeshData meshData, bool withCollider)
    {
        if (meshData == null)
            return;

        // Render mesh
        Mesh renderMesh = new Mesh
        {
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
        };

        renderMesh.SetVertices(meshData.vertices);
        renderMesh.SetTriangles(meshData.triangles, 0);
        renderMesh.SetUVs(0, meshData.uvs);
        renderMesh.SetUVs(1, meshData.uvMeta);
        renderMesh.RecalculateNormals();
        renderMesh.RecalculateTangents();
        renderMesh.RecalculateBounds();

        meshFilter.sharedMesh = renderMesh;

        // Collider (SAME MeshData)
        if (withCollider)
            UpdateCollider(meshData);
        else
            DestroyCollider();

        meshRenderer.sharedMaterial =
            Resources.Load<Material>("Materials/AtlasMaterial");
    }

    
    public void Rebuild(bool withCollider)
    {
        if (chunk == null || chunk.blocks == null)
            return;

        // Generate fresh MeshData
        MeshData md = ChunkMeshGeneratorThreaded.GenerateMeshData(
            (x,y,z) => chunk.GetBlock(x,y,z),
            (x,y,z) => chunk.GetStateAt(x,y,z),
            chunk.GetLodScale(),
            chunk.chunkManager.GetNeighborLODInfo(chunk.coord)
        );

        // Store it
        chunk.meshData = md;

        // Apply render mesh
        ApplyMeshData(md, withCollider);
    }


    public void CreateCollider(MeshData meshData)
    {
        if(meshData == null)
            return;

        if (meshCollider == null)
        {
            meshCollider = gameObject.AddComponent<MeshCollider>();
        }

        var colliderMesh = new Mesh();
        colliderMesh.Clear();
        colliderMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        colliderMesh.SetVertices(meshData.colliderVertices);
        colliderMesh.SetTriangles(meshData.colliderTriangles, 0);
        colliderMesh.RecalculateNormals();
        colliderMesh.RecalculateTangents();
        colliderMesh.RecalculateBounds();

        meshCollider.sharedMesh = colliderMesh;
    }

    public void UpdateCollider(MeshData meshData)
    {
        if(meshData == null)
            return;

        if (meshCollider == null)
        {
            meshCollider = gameObject.AddComponent<MeshCollider>();
        }

        var colliderMesh = new Mesh();
        colliderMesh.Clear();
        colliderMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        colliderMesh.SetVertices(meshData.colliderVertices);
        colliderMesh.SetTriangles(meshData.colliderTriangles, 0);
        colliderMesh.RecalculateNormals();
        colliderMesh.RecalculateTangents();
        colliderMesh.RecalculateBounds();

        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = colliderMesh;
    }

    public void DestroyCollider()
    {
        if(meshCollider == null)
            return;
        
        Destroy(meshCollider);
        meshCollider = null;
    }

    public bool HasCollider()
    {
        if (meshCollider == null)
            return false;
        
        return meshCollider != null;
    }
    
    
}
