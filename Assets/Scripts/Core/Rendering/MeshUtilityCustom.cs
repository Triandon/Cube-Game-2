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
        
        public byte CrackStage
        {
            get => (byte)(normal >> 24);
            set => normal = (normal & 0x00ffffffu) | ((uint)value << 24);
        }
        
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
            return PackSNorm8(value.x) | (PackSNorm8(value.y) << 8) | (PackSNorm8(value.z) << 16);
        }

        private static uint PackSNorm8(float value)
        {
            return unchecked((byte)(sbyte)Mathf.RoundToInt(Mathf.Clamp(value, -1f, 1f) * 127f));
        }

        
    }
    
    public sealed class ChunkMeshUploadData
    {
        public readonly ChunkVertex[] vertices;
        public readonly int[] indices;
        public readonly Bounds bounds;
        public readonly Core.Rendering.BlockCrackVertexData cracks;

        public ChunkMeshUploadData(ChunkVertex[] vertices, int[] indices, Bounds bounds,
            Core.Rendering.BlockCrackVertexData cracks = null)
        {
            this.vertices = vertices;
            this.indices = indices;
            this.bounds = bounds;
            this.cracks = cracks;
        }
    }

    public static ChunkMeshUploadData BuildUploadData(MeshData meshData)
    {
        // Snapshot the already-packed data for the upload queue; no attribute conversion pass.
        ChunkVertex[] vertices = meshData.CopyVertices();
        return new ChunkMeshUploadData(vertices, meshData.triangles.ToArray(), meshData.Bounds,
            meshData.CreateCrackVertexData(vertices));
    }
}
