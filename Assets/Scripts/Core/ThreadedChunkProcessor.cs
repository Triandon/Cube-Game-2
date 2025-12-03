using Core;
using Core.Block;
using UnityEngine;
using System;

public static class ThreadedChunkProcessor
{
    private const int CHUNK_SIZE = Chunk.CHUNK_SIZE;

    //Entry point for the worker thread
    public static ChunkGenResult ProcessRequest(ChunkGenRequest req)
    {
        const int S = CHUNK_SIZE;
        Vector3Int coord = req.coord;

        // ------------------------------------
        // 1. PREPARE PADDED BLOCKS
        // ------------------------------------
        // We want: padded size = (S+2)^3
        byte[,,] padded = req.paddedBlocks;
        bool needGenerate = false;

        if (padded == null ||
            padded.GetLength(0) != S + 2 ||
            padded.GetLength(1) != S + 2 ||
            padded.GetLength(2) != S + 2)
        {
            needGenerate = true;
        }

        if (needGenerate)
        {
            padded = new byte[S + 2, S + 2, S + 2];

            // Generate center chunk from noise
            byte[,,] gen = GenerateChunkBlocks(coord);

            // Insert center into padded at [1..S]
            for (int x = 0; x < S; x++)
            for (int y = 0; y < S; y++)
            for (int z = 0; z < S; z++)
                padded[x + 1, y + 1, z + 1] = gen[x, y, z];

            // Neighbors remain air (0)
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

                int x = idx % S;
                int y = (idx / S) % S;
                int z = idx / (S * S);

                center[x, y, z] = id;
                padded[x + 1, y + 1, z + 1] = id; // Keep mesher consistent.
            }
        }

        // ------------------------------------
        // 4. THREAD-SAFE BLOCK QUERY
        // ------------------------------------
        // Mesher queries local coords [-1..S], we map to padded [0..S+1]
        Func<int, int, int, byte> getBlock = (lx, ly, lz) =>
        {
            int px = lx + 1;
            int py = ly + 1;
            int pz = lz + 1;

            if ((uint)px >= S + 2 || (uint)py >= S + 2 || (uint)pz >= S + 2)
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
            meshData = new MeshData(); // empty mesh to prevent main-thread crash
        }

        // ------------------------------------
        // 6. RETURN RESULT
        // ------------------------------------
        return new ChunkGenResult(coord, center, meshData);
    }


    // Use same noise & rules as your Chunk.GenerateHeightMapData()
    // This function is only used if no padded blocks were passed in.
    private static byte[,,] GenerateChunkBlocks(Vector3Int coord)
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
                    // Use thread-safe ID constants — avoids BlockRegistry on worker thread.
                    b[x, y, z] = ThreadConstants.GrassID;
                }
                else if (worldY > height - 3)
                {
                    b[x, y, z] = ThreadConstants.DirtID;
                }
                else
                {
                    b[x, y, z] = ThreadConstants.StoneID;
                }

                if (worldY == 0)
                    b[x, y, z] = ThreadConstants.StoneID;
            }
        }

        return b;
    }
}
