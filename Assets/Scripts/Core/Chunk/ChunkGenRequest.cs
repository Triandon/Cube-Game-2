using System.Collections.Generic;
using Core.Block;
using UnityEngine;

public class ChunkGenRequest
{
    public Vector3Int coord;
    public readonly int lodScale;
    public ChunkMeshGeneratorThreaded.NeighborLODInfo neighborLods;

    public byte[,,] blocks;
    public bool meshOnly; // true = skips terrain gen
    public Dictionary<Vector3Int, byte[,,]> neighborBlocks;

    public ChunkGenRequest(Vector3Int coord, 
        int lodScale, ChunkMeshGeneratorThreaded.NeighborLODInfo neighborLods,
        byte[,,] blocks, bool meshOnly, Dictionary<Vector3Int, byte[,,]> neighborBlocks)
    {
        this.coord = coord;
        this.lodScale = lodScale;
        this.neighborLods = neighborLods;
        this.blocks = blocks;
        this.meshOnly = meshOnly;
        this.neighborBlocks = neighborBlocks;
    }
}
