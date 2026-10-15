using System.Runtime.InteropServices;
using UnityEngine;

public static class MeshUtilityCustom
{
    public static readonly int ChunkVertexStride = Marshal.SizeOf<ChunkVertex>();
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ChunkVertex
    {
        public uint positionXY;
        public uint positionZLightTile;
        public uint uv;
        public uint normal;

        private const float CoordinateScale = 100f;
        private const float InverseCoordinateScale = 1f / CoordinateScale;
        
        public ChunkVertex(Vector3 position, uint normal,
            uint uv, ushort atlasTile, byte light)
        {
            positionXY = PackCoordinate(position.x) | (PackCoordinate(position.y) << 16);
            // VoxelLight stores sky in the high nibble; the vertex stores it in bits 16-19.
            positionZLightTile = PackCoordinate(position.z)
                                 | ((uint)VoxelLight.GetSky(light) << 16)
                                 | ((uint)VoxelLight.GetBlock(light) << 20)
                                 | ((uint)atlasTile << 24);
            this.uv = uv;
            this.normal = normal;
        }
        
        public Vector3 Position => new Vector3(
            (positionXY & 0xffffu) * InverseCoordinateScale,
            (positionXY >> 16) * InverseCoordinateScale,
            (positionZLightTile & 0xffffu) * InverseCoordinateScale);

        public static uint PackUv(float u, float v)
        {
            return PackCoordinate(u) | (PackCoordinate(v) << 16);
        }

        private static uint PackCoordinate(float value)
        {
            return (uint)Mathf.Clamp(Mathf.RoundToInt(value * CoordinateScale), 0, ushort.MaxValue);
        }

        public static uint PackNormal(Vector3 value)
        {
            // Avoid quantization for the six directions emitted by regular voxel faces.
            if (value == Vector3.up) return 0u;
            if (value == Vector3.down) return 1u;
            if (value == Vector3.right) return 2u;
            if (value == Vector3.left) return 3u;
            if (value == Vector3.forward) return 4u;
            if (value == Vector3.back) return 5u;

            uint xyz = PackSNorm8(value.x) | (PackSNorm8(value.y) << 8) | (PackSNorm8(value.z) << 16);
            return PackGpuNormal(xyz);
        }

        private static uint PackSNorm8(float value)
        {
            return unchecked((byte)(sbyte)Mathf.RoundToInt(Mathf.Clamp(value, -1f, 1f) * 127f));
        }

        private static uint PackGpuNormal(uint xyz)
        {
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
        // Snapshot the already-packed data for the upload queue; no attribute conversion pass.
        return new ChunkMeshUploadData(meshData.CopyVertices(), meshData.triangles.ToArray(), meshData.Bounds);
    }
}
