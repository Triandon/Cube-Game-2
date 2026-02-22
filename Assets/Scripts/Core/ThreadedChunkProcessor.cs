using Core;
using Core.Block;
using UnityEngine;
using System;
using System.Collections.Generic;

public static class ThreadedChunkProcessor
{
    private const int CHUNK_SIZE = Chunk.CHUNK_SIZE;

    //Entry point for the worker thread
    // Entry point for the worker thread
    public static ChunkGenResult ProcessRequest(ChunkGenRequest req)
    {
        const int S = CHUNK_SIZE;
        Vector3Int coord = req.coord;
        
        byte[,,] center;
        byte[,,] padded;

        //1 Builds block data
        if (req.meshOnly)
        {
            center = req.blocks;
            padded = BuildPaddedFromCenter(center);
        }
        else
        {
            padded = GenerateTerrainPadded(coord, req.neighborBlocks);
            center = ExtractCenter(padded);
        }
        
        //2 Detect block entities
        List<Vector3Int> blockEntities = DetectBlockEntities(center);

        // ------------------------------------
        // 3. THREAD-SAFE BLOCK QUERY
        // ------------------------------------
        // Mesher queries local coords in [-1 .. S] inclusive, map to padded [0 .. S+1]
        Func<int, int, int, byte> getBlock = (lx, ly, lz) =>
        {
            int px = lx + 1;
            int py = ly + 1;
            int pz = lz + 1;

            // unsigned check to catch negative or beyond bounds quickly
            if ((uint)px >= (uint)(S + 2) || (uint)py >= (uint)(S + 2) || (uint)pz >= (uint)(S + 2))
                return 0;

            return padded[px, py, pz];
        };
        
        Func<int,int,int,BlockStateContainer> getState = (x, y, z) => null;

        // ------------------------------------
        // 4. MESH GENERATION
        // ------------------------------------
        MeshData meshData;
        try
        {
            meshData = ChunkMeshGeneratorThreaded.GenerateMeshData(getBlock,getState,req.lodScale,req.neighborLods);
        }
        catch (Exception e)
        {
            Debug.LogError($"ThreadedChunkProcessor: mesher exception at {coord}: {e}");
            meshData = new MeshData(); // return empty mesh to avoid main-thread crash
        }

        // ------------------------------------
        // 5. RETURN RESULT
        // ------------------------------------
        return new ChunkGenResult(coord, center, meshData,blockEntities);
    }

    private static byte[,,] BuildPaddedFromCenter(byte[,,] center)
    {
        int S = Chunk.CHUNK_SIZE;
        
        byte[,,] padded = new byte[S + 2, S + 2, S + 2];

        for (int x = 0; x < S; x++)
        for (int y = 0; y < S; y++)
        for (int z = 0; z < S; z++)
            padded[x + 1, y + 1, z + 1] = center[x, y, z];

        return padded;
    }

    private static byte[,,] ExtractCenter(byte[,,] padded)
    {
        int S = Chunk.CHUNK_SIZE;
        // ------------------------------------
        // 2. MAKE CENTER ARRAY (RETURNED TO CHUNK)
        // ------------------------------------
        byte[,,] center = new byte[S, S, S];
        for (int x = 0; x < S; x++)
        for (int y = 0; y < S; y++)
        for (int z = 0; z < S; z++)
            center[x, y, z] = padded[x + 1, y + 1, z + 1];

        return center;
    }
    
    private static byte[,,] GenerateTerrainPadded(Vector3Int coord, Dictionary<Vector3Int, byte[,,]> neighbors)
    {
        int S = Chunk.CHUNK_SIZE;
        
        // ------------------------------------
        // 1. PREPARE PADDED BLOCKS
        // ------------------------------------
        // padded expected size = (S+2)^3, center located at [1..S] on each axis
        byte[,,] padded = new byte[S+2, S+2, S+2];

        int S2 = S + 2;
        
        // column caches
        int[,] heightCache = new int[S2, S2];
        //ChunkClimate[,] climateCache = new ChunkClimate[S2, S2];
        byte[,] surfaceBlockCache = new byte[S2, S2];

        // build column data ONCE
        for (int x = -1; x <= S; x++)
        for (int z = -1; z <= S; z++)
        {
            int wx = coord.x * S + x;
            int wz = coord.z * S + z;

            int height = TerrainGeneration.SampleHeight(wx, wz);
            ChunkClimate climate = BiomeManager.GetClimateAt(wx, wz);

            heightCache[x + 1, z + 1] = height;

            surfaceBlockCache[x + 1, z + 1] =
                BiomeManager.ChooseSurfaceBlock(
                    climate, wx, wz, height, coord);
        }

        // now fill padded blocks
        for (int x = -1; x <= S; x++)
        for (int y = -1; y <= S; y++)
        for (int z = -1; z <= S; z++)
        {
            int wx = coord.x * S + x;
            int wy = coord.y * S + y;
            int wz = coord.z * S + z;

            int height = heightCache[x + 1, z + 1];
            byte surface = surfaceBlockCache[x + 1, z + 1];

            padded[x + 1, y + 1, z + 1] =
                TerrainGeneration.SampleBlock(
                    wx, wy, wz, height, surface);
        }
        
        //2 Override borders ONLY if neighbor exists
        // ----------------------------
        if (neighbors != null)
        {
            foreach (var kv in neighbors)
            {
                Vector3Int delta = kv.Key - coord;
                byte[,,] n = kv.Value;

                if (delta == Vector3Int.right)
                    CopyFace(n, padded, srcX: 0, dstX: S + 1);
                else if (delta == Vector3Int.left)
                    CopyFace(n, padded, srcX: S - 1, dstX: 0);
                else if (delta == Vector3Int.forward)
                    CopyFace(n, padded, srcZ: 0, dstZ: S + 1);
                else if (delta == Vector3Int.back)
                    CopyFace(n, padded, srcZ: S - 1, dstZ: 0);
                else if (delta == Vector3Int.up)
                    CopyFace(n, padded, srcY: 0, dstY: S + 1);
                else if (delta == Vector3Int.down)
                    CopyFace(n, padded, srcY: S - 1, dstY: 0);
            }
        }

        return padded;
    }
    
    private static void CopyFace(
        byte[,,] src,
        byte[,,] dst,
        int srcX = -1, int dstX = -1,
        int srcY = -1, int dstY = -1,
        int srcZ = -1, int dstZ = -1)
    {
        int S = Chunk.CHUNK_SIZE;

        for (int x = 0; x < S; x++)
        for (int y = 0; y < S; y++)
        for (int z = 0; z < S; z++)
        {
            int sx = srcX >= 0 ? srcX : x;
            int sy = srcY >= 0 ? srcY : y;
            int sz = srcZ >= 0 ? srcZ : z;

            int dx = dstX >= 0 ? dstX : x;
            int dy = dstY >= 0 ? dstY : y;
            int dz = dstZ >= 0 ? dstZ : z;

            dst[dx, dy, dz] = src[sx, sy, sz];
        }
    }

    
    private static List<Vector3Int> DetectBlockEntities(byte[,,] center)
    {
        int S = Chunk.CHUNK_SIZE;
        
        List<Vector3Int> result = null;

        for (int x = 0; x < S; x++)
        for (int y = 0; y < S; y++)
        for (int z = 0; z < S; z++)
        {
            byte id = center[x, y, z];
            if (id == 0) continue;

            Block block = BlockRegistry.GetBlock(id);
            if (block != null && block.HasBlockEntity)
            {
                result ??= new List<Vector3Int>();
                result.Add(new Vector3Int(x, y, z));
            }
        }

        return result;
    }
    
}
