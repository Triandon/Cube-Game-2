using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

public static class MeshUtilityCustom
{
    public static readonly int ChunkVertexStride = Marshal.SizeOf<ChunkVertex>();

    private static readonly Color32 DefaultVertexColor = new Color32(255, 255, 255, 255);
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ChunkVertex
    {
        public PackedVertexPosition position;
        public int normal;
        public Color32 color;
        public PackedUv uv0;
        public PackedAtlasTile uv1;

        public ChunkVertex(PackedVertexPosition position, int normal,
            PackedUv uv0, PackedAtlasTile uv1)
        {
            this.position = position;
            this.normal = normal;
            color = DefaultVertexColor;
            this.uv0 = uv0;
            this.uv1 = uv1;
        }
    }
    
    public sealed class ChunkMeshUploadData
    {
        public readonly ChunkVertex[] vertices;
        public readonly int[] indices;
        public readonly Bounds bounds;

        public ChunkMeshUploadData(ChunkVertex[] vertices, int[] indices, Bounds bounds)
        {
            this.vertices = vertices;
            this.indices = indices;
            this.bounds = bounds;
        }
    }

    public static ChunkMeshUploadData BuildUploadData(MeshData meshData)
    {
        int vertexCount = meshData.vertices.Count;
        ChunkVertex[] vertices = new ChunkVertex[vertexCount];

        for (int i = 0; i < vertexCount; i++)
        {
            int normal = i < meshData.normals.Count ? meshData.normals[i] : PackedNormal.Up;
            PackedUv uv0 = i < meshData.uvs.Count ? meshData.uvs.packedUvs[i] : default(PackedUv);
            PackedAtlasTile tile = i < meshData.atlasTileIndexes.Count
                ? new PackedAtlasTile(meshData.atlasTileIndexes[i])
                : default(PackedAtlasTile);
            vertices[i] = new ChunkVertex(meshData.vertices.packedPositions[i], normal, uv0, tile);

            if (i < meshData.colors.Count)
                vertices[i].color = meshData.colors[i];
        }

        return new ChunkMeshUploadData(vertices, meshData.triangles.ToArray(),
            CalculateDecodedBounds(meshData));
    }
    
    private static Bounds CalculateDecodedBounds(MeshData meshData)
    {
        int vertexCount = meshData.vertices.Count;
        if (vertexCount == 0)
            return new Bounds(Vector3.zero, Vector3.zero);

        Vector3 min = meshData.vertices[0];
        Vector3 max = min;

        for (int i = 1; i < vertexCount; i++)
        {
            Vector3 position = meshData.vertices[i];
            min = Vector3.Min(min, position);
            max = Vector3.Max(max, position);
        }

        Bounds bounds = new Bounds();
        bounds.SetMinMax(min, max);
        return bounds;
    }
}
