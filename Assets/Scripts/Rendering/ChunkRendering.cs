using System;
using Core;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ChunkRendering : MonoBehaviour
{
    private static Material atlasMaterial;
    
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;
    private Chunk chunk;
    private ChunkMeshGenerator meshGenerator;

    private Mesh shearedRenderMesh;
    private Mesh shearedColliderMesh;

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

        if (atlasMaterial == null)
        {
            atlasMaterial = Resources.Load<Material>("Materials/AtlasMaterial");
        }
    }

    private void OnDestroy()
    {
        if (meshFilter != null)
        {
            meshFilter.sharedMesh = null;
        }

        if (meshCollider != null)
        {
            meshCollider.sharedMesh = null;
        }

        if (shearedRenderMesh != null)
        {
            Destroy(shearedRenderMesh);
            shearedRenderMesh = null;
        }

        if (shearedColliderMesh != null)
        {
            Destroy(shearedColliderMesh);
            shearedColliderMesh = null;
        }
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
        
        // Material
        if (atlasMaterial != null)
        {
            meshRenderer.sharedMaterial = atlasMaterial;
        }
    }

    // modified chunk of your ChunkRendering class
    public void ApplyMeshData(MeshData meshData, bool withCollider)
    {
        if (meshData == null)
            return;

        // Render mesh
        if (shearedRenderMesh == null)
        {
            shearedRenderMesh = new Mesh()
            {
                name = $"ChunkRenderMesh_{GetInstanceID()}",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };
        }

        shearedRenderMesh.Clear();
        shearedRenderMesh.SetVertices(meshData.vertices);
        shearedRenderMesh.SetTriangles(meshData.triangles, 0);
        shearedRenderMesh.SetUVs(0, meshData.uvs);
        shearedRenderMesh.SetUVs(1, meshData.uvMeta);
        shearedRenderMesh.RecalculateNormals();
        shearedRenderMesh.RecalculateTangents();
        shearedRenderMesh.RecalculateBounds();
        
        meshFilter.sharedMesh = shearedRenderMesh;

        // Collider (SAME MeshData)
        if (withCollider)
            UpdateCollider(meshData);
        else
            DestroyCollider();

        if (atlasMaterial != null)
        {
            meshRenderer.sharedMaterial = atlasMaterial;
        }
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
        UpdateCollider(meshData);
    }

    public void UpdateCollider(MeshData meshData)
    {
        if (meshData == null)
            return;
        
        bool hasColliderGeometry =
            meshData.colliderVertices != null &&
            meshData.colliderTriangles != null &&
            meshData.colliderVertices.Count > 0 &&
            meshData.colliderTriangles.Count >= 3;

        if (!hasColliderGeometry)
        {
            if (meshCollider != null)
            {
                meshCollider.sharedMesh = null;
            }

            if (shearedColliderMesh != null)
            {
                shearedColliderMesh.Clear();
            }
            return;
        }

        if (meshCollider == null)
        {
            meshCollider = gameObject.AddComponent<MeshCollider>();
        }

        if (shearedColliderMesh == null)
        {
            shearedColliderMesh = new Mesh
            {
                name = $"ChunkColliderMesh_{GetInstanceID()}",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };
        }

        shearedColliderMesh.Clear();
        shearedColliderMesh.SetVertices(meshData.colliderVertices);
        shearedColliderMesh.SetTriangles(meshData.colliderTriangles, 0);
        shearedColliderMesh.RecalculateNormals();
        shearedColliderMesh.RecalculateTangents();
        shearedColliderMesh.RecalculateBounds();
        
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = shearedColliderMesh;
    }

    public void DestroyCollider()
    {
        if (meshCollider == null)
            return;

        meshCollider.sharedMesh = null;
        Destroy(meshCollider);
        meshCollider = null;
    }

    public bool HasCollider()
    {
        return meshCollider != null;
    }
    
    
}
