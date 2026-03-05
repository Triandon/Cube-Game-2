using System.Collections.Generic;
using UnityEngine;

public class ChunkGenResult
{
    public Vector3Int coord;
    public byte[,,] blocks; //Chunk_Size^3
    public MeshData meshData;
    public List<Vector3Int> blockEntityLocals;
    public bool isAllAir;
    
    //Tick system
    public List<Vector3Int> instantTickLocals;
    public List<Vector3Int> scheduledTickLocals;
    public List<Vector3Int> randomTickLocals;

    public ChunkGenResult(Vector3Int coord, byte[,,] blocks, MeshData meshData,
        List<Vector3Int> blockEntityLocals, bool isAllAir,List<Vector3Int> instantTickLocals = null,
        List<Vector3Int> scheduledTickLocals = null, List<Vector3Int> randomTickLocals = null)
    {
        this.coord = coord;
        this.blocks = blocks;
        this.meshData = meshData;
        this.blockEntityLocals = blockEntityLocals;
        this.isAllAir = isAllAir;
        this.instantTickLocals = instantTickLocals;
        this.scheduledTickLocals = scheduledTickLocals;
        this.randomTickLocals = randomTickLocals;
    }
}
