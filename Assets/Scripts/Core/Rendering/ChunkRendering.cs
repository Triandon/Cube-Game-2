using System;
using Core;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ChunkRendering : MonoBehaviour
{
    /// Lightweight host for a logical chunk GameObject. Terrain rendering is owned
    /// entirely by PagedTerrainMeshStorage; this component remains only because chunk
    /// pooling, transforms, and block-entity parenting use the host GameObject.
    public Chunk Chunk { get; private set; }

    public void SetChunkData(Chunk chunkData)
    {
        Chunk = chunkData;
    }
}
