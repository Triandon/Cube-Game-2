using System.Collections.Generic;
using UnityEngine;

public class ChunkGenResult
{
    public Vector3Int coord;
    public byte[,,] blocks; //Chunk_Size^3
    public MeshData meshData;
    public List<Vector3Int> blockEntityLocals;

    public ChunkGenResult(Vector3Int coord, byte[,,] blocks, MeshData meshData,
        List<Vector3Int> blockEntityLocals)
    {
        this.coord = coord;
        this.blocks = blocks;
        this.meshData = meshData;
        this.blockEntityLocals = blockEntityLocals;
    }
}
