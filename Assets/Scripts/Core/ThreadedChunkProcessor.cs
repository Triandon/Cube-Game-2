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

        // ------------------------------------
        // 1. PREPARE PADDED BLOCKS
        // ------------------------------------
        // padded expected size = (S+2)^3, center located at [1..S] on each axis
        byte[,,] padded = null;

        if (req.neighborSnapshots != null && req.neighborSnapshots.Count > 0)
        {
            padded = BuildPaddedBlocks(req.coord, req.neighborSnapshots);
        }

        if (padded == null)
        {
            padded = new byte[S + 2, S + 2, S + 2];
            byte[,,] gen = GenerateChunkBlocks(coord);

            for (int x = 0; x < S; x++)
            for (int y = 0; y < S; y++)
            for (int z = 0; z < S; z++)
                padded[x + 1, y + 1, z + 1] = gen[x, y, z];
            
            Debug.Log($"ThreadedChunkProcessor: generated center for chunk {coord} (no padded provided).");
        }

        // ------------------------------------
        // 2. MAKE CENTER ARRAY (RETURNED TO CHUNK)
        // ------------------------------------
        byte[,,] center = new byte[S, S, S];
        for (int x = 0; x < S; x++)
        for (int y = 0; y < S; y++)
        for (int z = 0; z < S; z++)
            center[x, y, z] = padded[x + 1, y + 1, z + 1];

        // ------------------------------------
        // 3. APPLY SAVED CHANGES (MODIFICATIONS)
        // ------------------------------------
        if (req.savedChanges != null && req.savedChanges.Count > 0)
        {
            foreach (var kv in req.savedChanges)
            {
                int idx = kv.Key;
                byte id = kv.Value;

                // inverse of PosToIndex: x + S*(y + S*z)
                int x = idx % S;
                int y = (idx / S) % S;
                int z = idx / (S * S);

                // Bounds sanity check (defensive)
                if (x >= 0 && x < S && y >= 0 && y < S && z >= 0 && z < S)
                {
                    center[x, y, z] = id;
                    padded[x + 1, y + 1, z + 1] = id; // keep mesher consistent with saved changes
                }
                else
                {
                    // Shouldn't happen, but log in case of corrupted save data
                    Debug.LogWarning(
                        $"ThreadedChunkProcessor: saved change out of range for {coord} idx={idx} -> ({x},{y},{z})");
                }
            }
        }

        // ------------------------------------
        // 4. THREAD-SAFE BLOCK QUERY
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

        // ------------------------------------
        // 5. MESH GENERATION
        // ------------------------------------
        MeshData meshData;
        try
        {
            meshData = ChunkMeshGeneratorThreaded.GenerateMeshData(getBlock);
        }
        catch (Exception e)
        {
            Debug.LogError($"ThreadedChunkProcessor: mesher exception at {coord}: {e}");
            meshData = new MeshData(); // return empty mesh to avoid main-thread crash
        }

        // ------------------------------------
        // 6. RETURN RESULT
        // ------------------------------------
        return new ChunkGenResult(coord, center, meshData);
    }

    private static byte[,,] BuildPaddedBlocks(
        Vector3Int coord, Dictionary<Vector3Int, byte[,,]> neighbors)
    {
        int S = Chunk.CHUNK_SIZE;
        int P = S + 2;

        byte[,,] padded = new byte[P, P, P];

        foreach (var kv in neighbors)
        {
            Vector3Int delta = kv.Key - coord;
            byte[,,] src = kv.Value;

            int baseX = (delta.x + 1) * S;
            int baseY = (delta.y + 1) * S;
            int baseZ = (delta.z + 1) * S;

            for (int x = 0; x < S; x++)
            for (int y = 0; y < S; y++)
            for (int z = 0; z < S; z++)
            {
                int px = baseX + x - S + 1;
                int py = baseY + y - S + 1;
                int pz = baseZ + z - S + 1;

                if ((uint)px < P && (uint)py < P && (uint)pz < P)
                    padded[px, py, pz] = src[x, y, z];
            }
        }
        
        bool centerEmpty = true;
        for (int x = 1; x <= S && centerEmpty; x++)
        for (int y = 1; y <= S && centerEmpty; y++)
        for (int z = 1; z <= S; z++)
        {
            if (padded[x, y, z] != 0)
            {
                centerEmpty = false;
                break;
            }
        }

        if (centerEmpty)
        {
            byte[,,] gen = GenerateChunkBlocks(coord);
            for (int x = 0; x < S; x++)
            for (int y = 0; y < S; y++)
            for (int z = 0; z < S; z++)
                padded[x + 1, y + 1, z + 1] = gen[x, y, z];
        }


        return padded;
    }

    // Use same noise & rules as your Chunk.GenerateHeightMapData()
    // This function is only used if no padded blocks were passed in.
    public static byte[,,] GenerateChunkBlocks(Vector3Int coord)
    {
        var b = new byte[CHUNK_SIZE, CHUNK_SIZE, CHUNK_SIZE];

        for (int x = 0; x < CHUNK_SIZE; x++)
        for (int z = 0; z < CHUNK_SIZE; z++)
        {
            int worldX = coord.x * CHUNK_SIZE + x;
            int worldZ = coord.z * CHUNK_SIZE + z;

            float baseHeight = WorldNoise.GetHeight(worldX * 0.01f, worldZ * 0.01f) * 64f;
            baseHeight = Mathf.Max(baseHeight, 0f);
            baseHeight += 25f;

            float detail = WorldNoise.GetHeight(worldX * 0.1f, worldZ * 0.1f) * 4f;
            int height = Mathf.FloorToInt(baseHeight + detail);

            for (int y = 0; y < CHUNK_SIZE; y++)
            {
                int worldY = coord.y * CHUNK_SIZE + y;
                if (worldY > height)
                {
                    b[x, y, z] = 0;
                }
                else if (worldY == height)
                {
                    b[x, y, z] = BlockDataBase.GrassBlock.id;
                }
                else if (worldY > height - 3)
                {
                    b[x, y, z] = BlockDataBase.DirtBlock.id;
                }
                else
                {
                    b[x, y, z] = BlockDataBase.StoneBlock.id;
                }

                if (worldY == 0)
                    b[x, y, z] = BlockDataBase.StoneBlock.id;
            }
        }

        return b;
    }
}
