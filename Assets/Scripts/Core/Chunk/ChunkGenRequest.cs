using System.Collections.Generic;
using UnityEngine;

public class ChunkGenRequest
{
    public Vector3Int coord;
    public Dictionary<int, byte> savedChanges;
    // paddedBlocks: [CHUNK_SIZE + 2, CHUNK_SIZE + 2, CHUNK_SIZE + 2]
    // center chunk is at offset +1 in each axis.
    public byte[,,] paddedBlocks;

    public ChunkGenRequest(Vector3Int coord, Dictionary<int, byte> savedChanges, byte[,,] paddedBlocks)
    {
        this.coord = coord;
        this.savedChanges = savedChanges;
        this.paddedBlocks = paddedBlocks;
    }
}
