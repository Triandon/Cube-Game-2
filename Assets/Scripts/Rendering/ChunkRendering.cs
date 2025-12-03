using System;
using Core;
using UnityEngine;

[RequireComponent(typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider))]
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
        meshCollider = GetComponent<MeshCollider>();
        chunk = GetComponent<Chunk>();

        meshGenerator = new ChunkMeshGenerator();
    }

    public void BuildChunkMesh()
    {
        if (chunk == null || chunk.blocks == null) return;

        // Generate both render mesh & collider mesh
        var meshData = meshGenerator.GenerateMesh(chunk.blocks, chunk);

        // Assign render mesh to MeshFilter
        meshFilter.sharedMesh = meshData.renderingMesh;

        // Assign collider mesh to MeshCollider
        meshCollider.sharedMesh = meshData.colliderMesh;

        // Material
        meshRenderer.sharedMaterial = Resources.Load<Material>("Materials/AtlasMaterial");
    }

    
}
