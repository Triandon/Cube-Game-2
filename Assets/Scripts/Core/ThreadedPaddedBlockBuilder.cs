using System;
using System.Collections.Concurrent;
using System.Threading;
using Core;
using UnityEngine;

public class ThreadedPaddedBlockBuilder
{
    private ChunkManager chunkManager;
    
    
    //Legacy class
    
    public byte[,,] BuildPaddedBlocks(Vector3Int coord)
    {
            int S = Chunk.CHUNK_SIZE;
            int P = S + 2;
            byte[,,] padded = new byte[P, P, P];

            // For offsets -1..+1 in x,y,z
            for (int ox = -1; ox <= 1; ox++)
            for (int oy = -1; oy <= 1; oy++)
            for (int oz = -1; oz <= 1; oz++)
            {
                Chunk neighbor = chunkManager.GetChunk(coord + new Vector3Int(ox, oy, oz));
                if (neighbor?.blocks == null) continue;

                byte[,,] src = neighbor.blocks;

                // Destination origin in the large 3xS grid: (-1->0, 0->S, +1->2S)
                int baseDstX = (ox + 1) * S;
                int baseDstY = (oy + 1) * S;
                int baseDstZ = (oz + 1) * S;

                for (int x = 0; x < S; x++)
                for (int y = 0; y < S; y++)
                for (int z = 0; z < S; z++)
                {
                    int bigX = baseDstX + x;
                    int bigY = baseDstY + y;
                    int bigZ = baseDstZ + z;

                    // Map the 3xS^3 -> compact [1..S] by subtracting S and adding 1
                    int cx = bigX - S + 1;
                    int cy = bigY - S + 1;
                    int cz = bigZ - S + 1;

                    if (cx >= 0 && cx < P && cy >= 0 && cy < P && cz >= 0 && cz < P)
                        padded[cx, cy, cz] = src[x, y, z];
                }
            }

            // --- Ensure the center is always filled ---
            bool centerEmpty = true;
            for (int x = 1; x <= S; x++)
            for (int y = 1; y <= S; y++)
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
                // Use the existing thread-safe generator from ThreadedChunkProcessor
                //byte[,,] generated = ThreadedChunkProcessor.GenerateChunkBlocks(coord);
                //for (int x = 0; x < S; x++)
                //for (int y = 0; y < S; y++)
                //for (int z = 0; z < S; z++)
                    //padded[x + 1, y + 1, z + 1] = generated[x, y, z];
            }
 

            return padded;
    }

}
