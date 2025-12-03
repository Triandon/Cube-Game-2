using UnityEngine;

public class ChunkGenResult
{
    public Vector3Int coord;
    public byte[,,] blocks; //Chunk_Size^3
    public MeshData meshData;

    public ChunkGenResult(Vector3Int coord, byte[,,] blocks, MeshData meshData)
    {
        this.coord = coord;
        this.blocks = blocks;
        this.meshData = meshData;
    }
}
