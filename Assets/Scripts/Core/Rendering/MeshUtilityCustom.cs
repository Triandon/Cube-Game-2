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
        public uint positionXY;
        public uint positionZLightTile;
        public uint uv;
        public uint normal;

        public ChunkVertex(PackedVertexPosition position, int normal,
            PackedUv uv0, ushort atlasTile, Color32 color)
        {
            // AddVertexLight stores the 0-15 light levels as multiples of 17.
            // Quantize to the nearest level for callers supplying other byte values.
            uint sky = (uint)(color.r + 8) / 17u;
            uint block = (uint)(color.g + 8) / 17u;
            positionXY = (uint)position.x | ((uint)position.y << 16);
            positionZLightTile = (uint)position.z | (sky << 16) | (block << 20)
                                 | ((uint)atlasTile << 24);
            uv = (uint)uv0.u | ((uint)uv0.v << 16);
            this.normal = PackGpuNormal(normal);
        }
        
        private static uint PackGpuNormal(int packedNormal)
        {
            uint xyz = unchecked((uint)packedNormal) & 0x00ffffffu;
            // Three-bit direction IDs, matching DecodePackedNormal in the shader.
            switch (xyz)
            {
                case 0x007f00u: return 0u; // +Y
                case 0x008100u: return 1u; // -Y
                case 0x00007fu: return 2u; // +X
                case 0x000081u: return 3u; // -X
                case 0x7f0000u: return 4u; // +Z
                case 0x810000u: return 5u; // -Z
                // Tag 6 retains all three signed bytes for slopes and zero normals.
                default: return (xyz << 8) | 6u;
            }
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
            ushort tile = i < meshData.atlasTileIndexes.Count ? meshData.atlasTileIndexes[i] : (ushort)0;
            Color32 color = i < meshData.colors.Count ? meshData.colors[i] : DefaultVertexColor;
            vertices[i] = new ChunkVertex(meshData.vertices.packedPositions[i], normal, uv0, tile, color);
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
