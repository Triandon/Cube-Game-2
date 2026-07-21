using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

public static class MeshUtilityCustom
{
    
    private static readonly VertexAttributeDescriptor[] ChunkVertexLayout =
    {
        new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.UInt16, 4),
        new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.SNorm8, 4),
        new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4),
    };

    private static readonly Color32 DefaultVertexColor = new Color32(255, 255, 255, 255);
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct ChunkVertex
    {
        public PackedVertexPosition position;
        public int normal;
        public Color32 color;
        public Vector2 uv0;
        public Vector4 uv1;

        public ChunkVertex(PackedVertexPosition position, Vector3 normal, Vector2 uv0, Vector4 uv1)
        {
            this.position = position;
            this.normal = PackSNorm8Vector4(normal);
            color = DefaultVertexColor;
            this.uv0 = uv0;
            this.uv1 = uv1;
        }
    }

    public static void ApplyChunkMesh(Mesh mesh, MeshData meshData)
    {
        int vertexCount = meshData.vertices.Count;
        int indexCount = meshData.triangles.Count;
        ChunkVertex[] vertices = new ChunkVertex[vertexCount];

        for (int i = 0; i < vertexCount; i++)
        {
            Vector3 normal = i < meshData.normals.Count ? meshData.normals[i] : Vector3.up;
            Vector2 uv0 = i < meshData.uvs.Count ? meshData.uvs[i] : Vector2.zero;
            Vector4 uv1 = i < meshData.uvMeta.Count ? meshData.uvMeta[i] : Vector4.zero;
            vertices[i] = new ChunkVertex(meshData.vertices.packedPositions[i], normal, uv0, uv1);
        }

        mesh.Clear();
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertexBufferParams(vertexCount, ChunkVertexLayout);
        mesh.SetVertexBufferData(vertices, 0, 0, vertexCount, 0, MeshUpdateFlags.DontRecalculateBounds);
        mesh.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
        mesh.SetIndexBufferData(meshData.triangles, 0, 0, indexCount, MeshUpdateFlags.DontRecalculateBounds);
        mesh.subMeshCount = 1;
        mesh.SetSubMesh(0, new SubMeshDescriptor(0, indexCount, MeshTopology.Triangles), MeshUpdateFlags.DontRecalculateBounds);
        mesh.bounds = CalculateDecodedBounds(meshData);
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


    
    private const int PackedNormalRight = 0x7F00007F;
    private const int PackedNormalLeft = 0x7F000081;
    private const int PackedNormalUp = 0x7F007F00;
    private const int PackedNormalDown = 0x7F008100;
    private const int PackedNormalForward = 0x7F7F0000;
    private const int PackedNormalBack = 0x7F810000;

    private static int PackSNorm8Vector4(Vector3 value)
    {
        // Fast path for normal voxel face directions.
        if (value == Vector3.right) return PackedNormalRight;
        if (value == Vector3.left) return PackedNormalLeft;
        if (value == Vector3.up) return PackedNormalUp;
        if (value == Vector3.down) return PackedNormalDown;
        if (value == Vector3.forward) return PackedNormalForward;
        if (value == Vector3.back) return PackedNormalBack;

        byte x = unchecked((byte)PackSNorm8(value.x));
        byte y = unchecked((byte)PackSNorm8(value.y));
        byte z = unchecked((byte)PackSNorm8(value.z));
        byte w = unchecked((byte)127);

        return x | (y << 8) | (z << 16) | (w << 24);
    }

    private static sbyte PackSNorm8(float value)
    {
        return (sbyte)Mathf.RoundToInt(Mathf.Clamp(value, -1f, 1f) * 127f);
    }
}
