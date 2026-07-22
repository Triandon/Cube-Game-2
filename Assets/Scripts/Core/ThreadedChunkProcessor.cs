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

        if (req.allowDiskLoad && req.blocks == null && !string.IsNullOrEmpty(req.chunkSavePath))
        {
            Chunk savedChunk = new Chunk(coord);
            if (WorldSaveSystem.LoadChunk(req.chunkSavePath, coord, savedChunk))
            {
                savedChunk.RebuildSpecialMeshBlocks();
                req.blocks = savedChunk.blocks;
                req.states = savedChunk.states;
                req.specialMeshBlocks = savedChunk.GetSpecialMeshBlocksSnapshot();

                req.meshOnly = true;
            }
        }
        
        byte[,,] center;
        byte[] padded;
        BlockStateContainer[,,] paddedStates;

        //1 Builds block data
        if (req.meshOnly)
        {
            center = req.blocks;
            padded = BuildPaddedFromCenter(center, coord, req.neighborBlocks);
            paddedStates = BuildPaddedStatesFromCenter(coord, req.states, req.neighborStates);
        }
        else
        {
            padded = GenerateTerrainPadded(coord, req.neighborBlocks);
            center = ExtractCenter(padded);
            paddedStates = null;
        }
        
        // 1.1
        // Skip mesh generation for all air chunks!
        //2 Detect block entities
        bool isAllAir = AnalyzeBlocks(center, out List<Vector3Int> blockEntities,
            out List<Vector3Int> instantTickLocals, out List<Vector3Int> scheduledTickLocals,
            out List<Vector3Int> randomTickLocals);

        byte[,,] skyLight = BuildSkyLight(center, req.incomingSkyLightFromAbove);
        byte[,,] blockLight = new byte[S, S, S];
        
        if (isAllAir)
            return new ChunkGenResult(coord, center, req.states, new MeshData(), null,
                true, instantTickLocals, scheduledTickLocals, randomTickLocals,
                skyLight, blockLight);

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

            return padded[PaddedIndex(px, py, pz)];
        };
        
        // 3.1 Get states. Similar
        Func<int,int,int,BlockStateContainer> getState = (x, y, z) =>
        {
            var states = paddedStates;
            if (states == null)
                return null;

            int px = x + 1;
            int py = y + 1;
            int pz = z + 1;

            if ((uint)px >= (uint)(S + 2) || (uint)py >= (uint)(S + 2) || (uint)pz >= (uint)(S + 2))
                return null;
            
            return states[px, py, pz];
        };
        
        Func<int, int, int, byte> getSkyLight = (x, y, z) =>
        {
            if ((uint)x >= (uint)S || (uint)y >= (uint)S || (uint)z >= (uint)S)
                return VoxelLight.Max;

            return skyLight[x, y, z];
        };

        Func<int, int, int, byte> getBlockLight = (x, y, z) =>
        {
            if ((uint)x >= (uint)S || (uint)y >= (uint)S || (uint)z >= (uint)S)
                return VoxelLight.Min;

            return blockLight[x, y, z];
        };
        


        // ------------------------------------
        // 4. MESH GENERATION
        // ------------------------------------
        MeshData meshData;
        try
        {
            meshData = ChunkMeshGeneratorThreaded.GenerateMeshData(getBlock,getState,req.lodScale,req.neighborLods, req.specialMeshBlocks,
                getSkyLight, getBlockLight);
        }
        catch (Exception e)
        {
            Debug.LogError($"ThreadedChunkProcessor: mesher exception at {coord}: {e}");
            meshData = new MeshData(); // return empty mesh to avoid main-thread crash
        }

        // ------------------------------------
        // 5. RETURN RESULT
        // ------------------------------------
        return new ChunkGenResult(coord, center, req.states ,meshData,blockEntities,
            false,instantTickLocals, scheduledTickLocals, randomTickLocals,
            skyLight, blockLight);
    }

    private static byte[] BuildPaddedFromCenter(byte[,,] center, Vector3Int coord,
        Dictionary<Vector3Int, byte[,,]> neighbors)
    {
        int S = Chunk.CHUNK_SIZE;
        int P = S + 2;
        byte[] padded = new byte[P * P * P];

        for (int x = 0; x < S; x++)
        for (int y = 0; y < S; y++)
        for (int z = 0; z < S; z++)
            padded[PaddedIndex(x + 1, y + 1, z + 1)] = center[x, y, z];

        CopyNeighborFaces(coord, neighbors, padded);
        
        return padded;
    }
    
    private static BlockStateContainer[,,] BuildPaddedStatesFromCenter(
        Vector3Int coord,
        BlockStateContainer[,,] centerStates,
        Dictionary<Vector3Int, BlockStateContainer[,,]> neighbors)
    {
        int S = Chunk.CHUNK_SIZE;

        if (centerStates == null && (neighbors == null || neighbors.Count == 0))
            return null;

        BlockStateContainer[,,] padded = new BlockStateContainer[S + 2, S + 2, S + 2];

        if (centerStates != null)
        {
            for (int x = 0; x < S; x++)
            for (int y = 0; y < S; y++)
            for (int z = 0; z < S; z++)
                padded[x + 1, y + 1, z + 1] = centerStates[x, y, z];
        }

        if (neighbors != null)
        {
            foreach (var kv in neighbors)
            {
                Vector3Int delta = kv.Key - coord;
                BlockStateContainer[,,] n = kv.Value;
                if (n == null)
                    continue;

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



    private static byte[,,] ExtractCenter(byte[] padded)
    {
        int S = Chunk.CHUNK_SIZE;
        // ------------------------------------
        // 2. MAKE CENTER ARRAY (RETURNED TO CHUNK)
        // ------------------------------------
        byte[,,] center = new byte[S, S, S];
        for (int x = 0; x < S; x++)
        for (int y = 0; y < S; y++)
        for (int z = 0; z < S; z++)
            center[x, y, z] = padded[PaddedIndex(x + 1, y + 1, z + 1)];

        return center;
    }

    private static byte[] GenerateTerrainPadded(Vector3Int coord, Dictionary<Vector3Int, byte[,,]> neighbors)
    {
        int S = Chunk.CHUNK_SIZE;
        int S2 = S + 2;
        // ------------------------------------
        // 1. PREPARE PADDED BLOCKS
        // ------------------------------------
        // padded expected size = (S+2)^3, center located at [1..S] on each axis
        byte[] padded = new byte[S2 * S2 * S2];

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

            padded[PaddedIndex(x + 1, y + 1, z + 1)] =
                TerrainGeneration.SampleBlock(
                    wx, wy, wz, height, surface);
        }

        //2 Override borders ONLY if neighbor exists
        // ----------------------------
        CopyNeighborFaces(coord, neighbors, padded);

        return padded;
    }

    private static int PaddedIndex(int x, int y, int z)
    {
        int P = Chunk.CHUNK_SIZE + 2;
        return x + P * (y + P * z);
    }

    private static void CopyNeighborFaces(Vector3Int coord, Dictionary<Vector3Int, byte[,,]> neighbors, byte[] padded)
    {
        if (neighbors == null)
            return;

        int S = Chunk.CHUNK_SIZE;
        
        foreach (var kv in neighbors)
        {
            Vector3Int delta = kv.Key - coord;
            byte[,,] n = kv.Value;
            
            if (n == null)
                continue;

            if (delta == Vector3Int.right)
                CopyBlockFace(n, padded, srcX: 0, dstX: S + 1);
            else if (delta == Vector3Int.left)
                CopyBlockFace(n, padded, srcX: S - 1, dstX: 0);
            else if (delta == Vector3Int.forward)
                CopyBlockFace(n, padded, srcZ: 0, dstZ: S + 1);
            else if (delta == Vector3Int.back)
                CopyBlockFace(n, padded, srcZ: S - 1, dstZ: 0);
            else if (delta == Vector3Int.up)
                CopyBlockFace(n, padded, srcY: 0, dstY: S + 1);
            else if (delta == Vector3Int.down)
                CopyBlockFace(n, padded, srcY: S - 1, dstY: 0);
        }
    }
    
    
    private static void CopyBlockFace(
        byte[,,] src,
        byte[] dst,
        int srcX = -1, int dstX = -1,
        int srcY = -1, int dstY = -1,
        int srcZ = -1, int dstZ = -1)
    {
        int S = Chunk.CHUNK_SIZE;
        
        if (srcX >= 0 || dstX >= 0)
        {
            for (int y = 0; y < S; y++)
            for (int z = 0; z < S; z++)
            {
                dst[PaddedIndex(dstX, y + 1, z + 1)] =
                    src[srcX, y, z];
            }
            return;
        }
        
        if (srcY >= 0 || dstY >= 0)
        {
            for (int x = 0; x < S; x++)
            for (int z = 0; z < S; z++)
            {
                dst[PaddedIndex(x + 1, dstY, z + 1)] = src[x, srcY, z];
            }
            return;
        }
        
        if (srcZ >= 0 || dstZ >= 0)
        {
            for (int x = 0; x < S; x++)
            for (int y = 0; y < S; y++)
            {
                dst[PaddedIndex(x + 1, y + 1, dstZ)] = src[x, y, srcZ];
            }
        }


    }
    
    private static void CopyFace<T>(
        T[,,] src,
        T[,,] dst,
        int srcX = -1, int dstX = -1,
        int srcY = -1, int dstY = -1,
        int srcZ = -1, int dstZ = -1)
    {
        int S = Chunk.CHUNK_SIZE;

        if (srcX >= 0 || dstX >= 0)
        {
            for (int y = 0; y < S; y++)
            for (int z = 0; z < S; z++)
            {
                dst[dstX, y + 1, z + 1] = src[srcX, y, z];
            }
            return;
        }
        
        if (srcY >= 0 || dstY >= 0)
        {
            for (int x = 0; x < S; x++)
            for (int z = 0; z < S; z++)
            {
                dst[x + 1, dstY, z + 1] = src[x, srcY, z];
            }
            return;
        }

        if (srcZ >= 0 || dstZ >= 0)
        {
            for (int x = 0; x < S; x++)
            for (int y = 0; y < S; y++)
            {
                dst[x + 1, y + 1, dstZ] = src[x, y, srcZ];
            }
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

    private static bool AnalyzeBlocks(byte[,,] center, out List<Vector3Int> blockEntities,
        out List<Vector3Int> instantTickLocals, out List<Vector3Int> scheduledTickLocals,
        out List<Vector3Int> randomTickLocals)
    {
        int S = Chunk.CHUNK_SIZE;
        bool isAllAir = true;

        blockEntities = null;
        instantTickLocals = null;
        scheduledTickLocals = null;
        randomTickLocals = null;
        
        for (int x = 0; x < S; x++)
        for (int y = 0; y < S; y++)
        for (int z = 0; z < S; z++)
        {
            byte id = center[x, y, z];
            if (id == 0) continue;

            isAllAir = false;

            Block block = BlockRegistry.GetBlock(id);
            if (block == null)
                continue;

            Vector3Int localPos = new Vector3Int(x, y, z);

            if (block.HasBlockEntity)
            {
                blockEntities ??= new List<Vector3Int>();
                blockEntities.Add(localPos);
            }

            if (block.HasInstantTick)
            {
                instantTickLocals ??= new List<Vector3Int>();
                instantTickLocals.Add(localPos);
            }

            if (block.HasScheduledTick)
            {
                scheduledTickLocals ??= new List<Vector3Int>();
                scheduledTickLocals.Add(localPos);
            }

            if (block.HasRandomTick)
            {
                randomTickLocals ??= new List<Vector3Int>();
                randomTickLocals.Add(localPos);
            }
        }

        return isAllAir;
    }
    
    private static byte[,,] BuildSkyLight(byte[,,] blocks, byte[,] incomingSkyLightFromAbove)
    {
        int S = Chunk.CHUNK_SIZE;
        byte[,,] skyLight = new byte[S, S, S];

        for (int x = 0; x < S; x++)
        for (int z = 0; z < S; z++)
        {
            byte currentSkyLight = incomingSkyLightFromAbove != null
                ? incomingSkyLightFromAbove[x, z]
                : VoxelLight.Max;

            for (int y = S - 1; y >= 0; y--)
            {
                byte blockId = blocks[x, y, z];
                skyLight[x, y, z] = VoxelLight.BlocksSkyLight(blockId) ? VoxelLight.Min : currentSkyLight;

                if (VoxelLight.BlocksSkyLight(blockId))
                    currentSkyLight = VoxelLight.Min;
            }
        }

        return skyLight;
    }

    
}
