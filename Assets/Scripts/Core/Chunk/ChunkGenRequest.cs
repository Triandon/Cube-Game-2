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
    public bool allowDiskLoad;
    public string chunkSavePath;
    public long generationQueuedTicks;
    public long enqueueTicks;
    public long workerStartTicks;

    public ChunkGenRequest(Vector3Int coord, 
        int lodScale, ChunkMeshGeneratorThreaded.NeighborLODInfo neighborLods,
        byte[,,] blocks, BlockStateContainer[,,] states, bool meshOnly, Dictionary<Vector3Int, byte[,,]> neighborBlocks,
        Dictionary<Vector3Int, BlockStateContainer[,,]> neighborStates, HashSet<Vector3Int> specialMeshBlocks,
        bool allowDiskLoad = false, string chunkSavePath = null, long generationQueuedTicks = 0)
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
        this.allowDiskLoad = allowDiskLoad;
        this.chunkSavePath = chunkSavePath;
        enqueueTicks = System.Diagnostics.Stopwatch.GetTimestamp();
        this.generationQueuedTicks = generationQueuedTicks > 0 ? generationQueuedTicks : enqueueTicks;
    }
}
