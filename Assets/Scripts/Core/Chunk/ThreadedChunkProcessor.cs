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
        
        byte[] center;
        byte[] padded;
        BlockStateContainer[] paddedStates;

        //1 Builds block data
        if (req.meshOnly)
        {
            center = req.blocks;
            // Initial disk-load/data requests are installed into the lighting world
            // before their first mesh is requested. Do not build their temporary
            // padded inputs (or a mesh that the main thread cannot safely use yet).
            padded = req.isMeshRebuild
                ? BuildPaddedFromCenter(center, req.neighborBlocks)
                : null;
            paddedStates = req.isMeshRebuild
                ? BuildPaddedStatesFromCenter(req.states, req.neighborStates)
                : null;
        }
        else
        {
            center = GenerateTerrainCenter(coord);
            padded = null;
            paddedStates = null;
        }
        
        // 1.1
        // Skip mesh generation for all air chunks!
        //2 Detect block entities
        bool isAllAir = AnalyzeBlocks(center, out List<Vector3Int> blockEntities,
            out List<Vector3Int> instantTickLocals, out List<Vector3Int> scheduledTickLocals,
            out List<Vector3Int> randomTickLocals);

        byte[] skyLight = req.isMeshRebuild && req.skyLight != null
            ? req.skyLight
            : BuildSkyLight(center, req.incomingSkyLightFromAbove);
        byte[] blockLight = req.isMeshRebuild && req.blockLight != null
            ? req.blockLight
            : new byte[ArrayIndexing.Volume];

        if (isAllAir)
        {
            MeshData emptyMesh = req.isMeshRebuild ? new MeshData() : null;
            ChunkGenResult emptyResult = new ChunkGenResult(coord,
                req.isMeshRebuild ? null : center,
                req.isMeshRebuild ? null : req.states,
                emptyMesh, null,
                true, instantTickLocals, scheduledTickLocals, randomTickLocals,
                skyLight, blockLight);
            emptyResult.isMeshRebuild = req.isMeshRebuild;
            emptyResult.meshRevision = req.meshRevision;

            if (req.isMeshRebuild)
            {
                emptyResult.meshUploadData = MeshUtilityCustom.BuildUploadData(emptyMesh);
                emptyResult.meshData = null;
            }
            
            return emptyResult;
        }
        
        // A data-generation result is integrated with cross-chunk lighting on the
        // main thread, which then schedules the one authoritative mesh rebuild.
        // Generating a mesh here would only be discarded by ApplyChunkResult.
        if (!req.isMeshRebuild)
        {
            return new ChunkGenResult(coord, center, req.states, null, blockEntities,
                false, instantTickLocals, scheduledTickLocals, randomTickLocals,
                skyLight, blockLight);
        }
        
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
            
            return states[PaddedIndex(px, py, pz)];
        };
        
        Func<int, int, int, byte> getSkyLight = (x, y, z) =>
        {
            if (req.paddedSkyLight != null)
            {
                int px = x + 1;
                int py = y + 1;
                int pz = z + 1;
                if ((uint)px < (uint)(S + 2) && (uint)py < (uint)(S + 2) && (uint)pz < (uint)(S + 2))
                    return req.paddedSkyLight[PaddedIndex(px, py, pz)];
            }
            
            if ((uint)x >= (uint)S || (uint)y >= (uint)S || (uint)z >= (uint)S)
                return VoxelLight.Min;

            return skyLight[ArrayIndexing.ToIndex(x, y, z)];
        };

        Func<int, int, int, byte> getBlockLight = (x, y, z) =>
        {
            if (req.paddedBlockLight != null)
            {
                int px = x + 1;
                int py = y + 1;
                int pz = z + 1;
                if ((uint)px < (uint)(S + 2) && (uint)py < (uint)(S + 2) && (uint)pz < (uint)(S + 2))
                    return req.paddedBlockLight[PaddedIndex(px, py, pz)];
            }
            
            if ((uint)x >= (uint)S || (uint)y >= (uint)S || (uint)z >= (uint)S)
                return VoxelLight.Min;

            return blockLight[ArrayIndexing.ToIndex(x, y, z)];
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
        ChunkGenResult result = new ChunkGenResult(coord,
            req.isMeshRebuild ? null : center,
            req.isMeshRebuild ? null : req.states,
            meshData, blockEntities,
            false,instantTickLocals, scheduledTickLocals, randomTickLocals,
            skyLight, blockLight);
        result.isMeshRebuild = req.isMeshRebuild;
        result.meshRevision = req.meshRevision;
        result.meshUploadData = MeshUtilityCustom.BuildUploadData(meshData);
        result.meshData = null;
        return result;
    }

    private static byte[] BuildPaddedFromCenter(byte[] center,
        ChunkBoundarySnapshot<byte> neighbors)
    {
        int S = Chunk.CHUNK_SIZE;
        int P = S + 2;
        byte[] padded = new byte[P * P * P];

        for (int x = 0; x < S; x++)
        for (int y = 0; y < S; y++)
        for (int z = 0; z < S; z++)
            padded[PaddedIndex(x + 1, y + 1, z + 1)] = center[ArrayIndexing.ToIndex(x, y, z)];

        CopyNeighborFaces(neighbors, padded);
        
        return padded;
    }
    
    private static BlockStateContainer[] BuildPaddedStatesFromCenter(
        BlockStateContainer[] centerStates,
        ChunkBoundarySnapshot<BlockStateContainer> neighbors)
    {
        int S = Chunk.CHUNK_SIZE;

        if (centerStates == null && (neighbors == null || neighbors.IsEmpty))
            return null;

        int paddedSize = S + 2;
        BlockStateContainer[] padded = new BlockStateContainer[paddedSize * paddedSize * paddedSize];

        if (centerStates != null)
        {
            for (int x = 0; x < S; x++)
            for (int y = 0; y < S; y++)
            for (int z = 0; z < S; z++)
                padded[PaddedIndex(x + 1, y + 1, z + 1)] = centerStates[ArrayIndexing.ToIndex(x, y, z)];
        }

        CopyNeighborFaces(neighbors, padded);

        return padded;
    }
    private static byte[] GenerateTerrainCenter(Vector3Int coord)
    {
        int S = Chunk.CHUNK_SIZE;
        byte[] center = new byte[ArrayIndexing.Volume];

        int[,] heightCache = new int[S, S];
        byte[,] surfaceBlockCache = new byte[S, S];

        for (int x = 0; x < S; x++)
        for (int z = 0; z < S; z++)
        {
            int wx = coord.x * S + x;
            int wz = coord.z * S + z;

            int height = TerrainGeneration.SampleHeight(wx, wz);
            ChunkClimate climate = BiomeManager.GetClimateAt(wx, wz);

            heightCache[x, z] = height;
            surfaceBlockCache[x, z] =
                BiomeManager.ChooseSurfaceBlock(
                    climate, wx, wz, height, coord);
        }
        
        for (int x = 0; x < S; x++)
        for (int y = 0; y < S; y++)
        for (int z = 0; z < S; z++)
        {
            int wx = coord.x * S + x;
            int wy = coord.y * S + y;
            int wz = coord.z * S + z;
            
            int height = heightCache[x, z];
            byte surface = surfaceBlockCache[x, z];
            
            center[ArrayIndexing.ToIndex(x, y, z)] =
                TerrainGeneration.SampleBlock(
                    wx, wy, wz, height, surface);

        }

        return center;
    }

    private static byte[] GenerateTerrainPadded(Vector3Int coord, ChunkBoundarySnapshot<byte> neighbors)
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
        CopyNeighborFaces(neighbors, padded);

        return padded;
    }

    private static int PaddedIndex(int x, int y, int z)
    {
        int P = Chunk.CHUNK_SIZE + 2;
        return x + P * (y + P * z);
    }

    private static void CopyNeighborFaces<T>(ChunkBoundarySnapshot<T> neighbors, T[] padded)
    {
        if (neighbors == null)
            return;

        int S = Chunk.CHUNK_SIZE;
        
        CopyBoundaryFace(neighbors.PositiveX, padded, axis: 0, destination: S + 1);
        CopyBoundaryFace(neighbors.NegativeX, padded, axis: 0, destination: 0);
        CopyBoundaryFace(neighbors.PositiveY, padded, axis: 1, destination: S + 1);
        CopyBoundaryFace(neighbors.NegativeY, padded, axis: 1, destination: 0);
        CopyBoundaryFace(neighbors.PositiveZ, padded, axis: 2, destination: S + 1);
        CopyBoundaryFace(neighbors.NegativeZ, padded, axis: 2, destination: 0);

    }

    private static void CopyBoundaryFace<T>(T[] source, T[] padded, int axis, int destination)
    {
        if (source == null)
            return;
        
        int size = Chunk.CHUNK_SIZE;
        for (int a = 0; a < size; a++)
        for (int b = 0; b < size; b++)
        {
            int x = axis == 0 ? destination : a + 1;
            int y = axis == 1 ? destination : (axis == 0 ? a + 1 : b + 1);
            int z = axis == 2 ? destination : b + 1;
            padded[PaddedIndex(x, y, z)] = source[a + size * b];
        }
    }
    
    private static List<Vector3Int> DetectBlockEntities(byte[] center)
    {
        int S = Chunk.CHUNK_SIZE;
        
        List<Vector3Int> result = null;

        for (int x = 0; x < S; x++)
        for (int y = 0; y < S; y++)
        for (int z = 0; z < S; z++)
        {
            byte id = center[ArrayIndexing.ToIndex(x, y, z)];
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

    private static bool AnalyzeBlocks(byte[] center, out List<Vector3Int> blockEntities,
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
            byte id = center[ArrayIndexing.ToIndex(x, y, z)];
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
    
    private static byte[] BuildSkyLight(byte[] blocks, byte[,] incomingSkyLightFromAbove)
    {
        int S = Chunk.CHUNK_SIZE;
        byte[] skyLight = new byte[ArrayIndexing.Volume];

        for (int x = 0; x < S; x++)
        for (int z = 0; z < S; z++)
        {
            byte currentSkyLight = incomingSkyLightFromAbove != null
                ? incomingSkyLightFromAbove[x, z]
                : VoxelLight.Max;

            for (int y = S - 1; y >= 0; y--)
            {
                byte blockId = blocks[ArrayIndexing.ToIndex(x, y, z)];
                skyLight[ArrayIndexing.ToIndex(x, y, z)] = VoxelLight.BlocksSkyLight(blockId) ? VoxelLight.Min : currentSkyLight;

                if (VoxelLight.BlocksSkyLight(blockId))
                    currentSkyLight = VoxelLight.Min;
            }
        }

        return skyLight;
    }

    
}
