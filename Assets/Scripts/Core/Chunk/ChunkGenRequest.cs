using System.Collections.Generic;
using Core.Block;
using UnityEngine;

public class ChunkGenRequest
{
    public Vector3Int coord;
    public readonly int lodScale;
    public ChunkMeshGeneratorThreaded.NeighborLODInfo neighborLods;

    public byte[,,] blocks;
    public BlockStateContainer[,,] states;
    public bool meshOnly; // true = skips terrain gen
    public Dictionary<Vector3Int, byte[,,]> neighborBlocks;
    public Dictionary<Vector3Int, BlockStateContainer[,,]> neighborStates;
    public HashSet<Vector3Int> specialMeshBlocks;

    public ChunkGenRequest(Vector3Int coord, 
        int lodScale, ChunkMeshGeneratorThreaded.NeighborLODInfo neighborLods,
        byte[,,] blocks, BlockStateContainer[,,] states, bool meshOnly, Dictionary<Vector3Int, byte[,,]> neighborBlocks,
        Dictionary<Vector3Int, BlockStateContainer[,,]> neighborStates, HashSet<Vector3Int> specialMeshBlocks)
    {
        this.coord = coord;
        this.lodScale = lodScale;
        this.neighborLods = neighborLods;
        this.blocks = blocks;
        this.states = states;
        this.meshOnly = meshOnly;
        this.neighborBlocks = neighborBlocks;
        this.neighborStates = neighborStates;
        this.specialMeshBlocks = specialMeshBlocks;
    }
}
