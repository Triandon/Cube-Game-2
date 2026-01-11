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

    public void BuildChunkColliderMesh()
    {
        if(chunk == null || chunk.blocks == null)
            return;

        if (meshCollider == null)
            meshCollider = gameObject.AddComponent<MeshCollider>();
        
        var meshData = meshGenerator.GenerateMesh(chunk.blocks, chunk);
        meshCollider.sharedMesh = meshData.colliderMesh;
    }

    // modified chunk of your ChunkRendering class
    public void ApplyMeshData(MeshData meshData)
    {
        if (meshData == null) return;

        //Render mesh
        var renderMesh = new Mesh();
        renderMesh.Clear();
        renderMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        renderMesh.SetVertices(meshData.vertices);
        renderMesh.SetTriangles(meshData.triangles, 0);
        renderMesh.SetUVs(0, meshData.uvs);
        renderMesh.SetUVs(1, meshData.uvMeta);
        renderMesh.RecalculateNormals();
        renderMesh.RecalculateTangents();
        renderMesh.RecalculateBounds();

        meshFilter.sharedMesh = renderMesh;
        

        meshRenderer.sharedMaterial = Resources.Load<Material>("Materials/AtlasMaterial");
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
