using System.Collections.Generic;
using Core.Block;
using UnityEngine;

public class ChunkGenRequest
{
    public Vector3Int coord;
    public Dictionary<int, byte> savedBlocks;

    public Dictionary<int, BlockStateContainer> savedStates;
    // paddedBlocks: [CHUNK_SIZE + 2, CHUNK_SIZE + 2, CHUNK_SIZE + 2]
    // center chunk is at offset +1 in each axis.
    public Dictionary<Vector3Int, byte[,,]> neighborSnapshots;
    public readonly int lodScale;
    public ChunkMeshGeneratorThreaded.NeighborLODInfo neighborLods;

    public ChunkGenRequest(Vector3Int coord, Dictionary<int, byte> savedBlocks, 
        Dictionary<int, BlockStateContainer> savedStates,
        Dictionary<Vector3Int, byte[,,]> neighborSnapshots, int lodScale, ChunkMeshGeneratorThreaded.NeighborLODInfo neighborLods)
    {
        this.coord = coord;
        this.savedBlocks = savedBlocks;
        this.savedStates = savedStates;
        this.neighborSnapshots = neighborSnapshots;
        this.lodScale = lodScale;
        this.neighborLods = neighborLods;
    }
}
