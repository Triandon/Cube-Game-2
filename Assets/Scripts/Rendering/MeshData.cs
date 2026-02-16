using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Core.Block;
using UnityEngine;

//Todo In the lods, there CAN be a height bias for long distance chunks!

public class MeshData
{
    public List<Vector3> vertices = new List<Vector3>();
    public List<int> triangles = new List<int>();
    public List<Vector2> uvs = new List<Vector2>();
    public List<Vector4> uvMeta = new List<Vector4>();

    public List<Vector3> colliderVertices = new List<Vector3>();
    public List<int> colliderTriangles = new List<int>();
}

public static class ChunkMeshGeneratorThreaded
{
    private const int CHUNK_SIZE = Chunk.CHUNK_SIZE;
    private const int ATLAS_TILES = 16;
    
    private struct MaskCell
    {
        public bool occluded;
        public int atlasIndex;
    }
    
    public struct NeighborLODInfo
    {
        public int posX, negX;
        public int posY, negY;
        public int posZ, negZ;
    }
    
    public static MeshData GenerateMeshData(Func<int,int,int,byte> getBlock,
        Func<int, int, int, BlockStateContainer> getState, int lodScale, NeighborLODInfo neighbors)
    {
        var mesh = new MeshData();

        // Local re-usable structures for mask and loops
        MaskCell[,] mask = new MaskCell[CHUNK_SIZE, CHUNK_SIZE];

        Vector3Int[] dirs = new Vector3Int[]
        {
            Vector3Int.right, Vector3Int.left,
            Vector3Int.up, Vector3Int.down,
            Vector3Int.forward, Vector3Int.back
        };

        foreach (var dir in dirs)
        {
            GreedyDirection(getBlock, getState, dir, mesh,mask, lodScale, neighbors);
        }
        return mesh;
    }
    
    // Greedy direction implementation adapted to be fully data-only and match original behavior
    private static void GreedyDirection(Func<int,int,int,byte> getBlock, Func<int, int, int, BlockStateContainer> getState, 
        Vector3Int dir, MeshData mesh,MaskCell[,] mask, int lodScale, NeighborLODInfo neighbors)
    {
        int neighborScale =
            dir == Vector3Int.right   ? neighbors.posX :
            dir == Vector3Int.left    ? neighbors.negX :
            dir == Vector3Int.up      ? neighbors.posY :
            dir == Vector3Int.down    ? neighbors.negY :
            dir == Vector3Int.forward ? neighbors.posZ :
            neighbors.negZ;
        
        int uMax = CHUNK_SIZE;
        int vMax = CHUNK_SIZE;
        int wMax = CHUNK_SIZE;

        int mSize = CHUNK_SIZE / lodScale;

        for (int w = 0; w < wMax; w += lodScale)
        {
            bool isBorderSlice =
                (dir == Vector3Int.left     && w == 0) ||
                (dir == Vector3Int.right    && w == wMax - lodScale) ||
                (dir == Vector3Int.down     && w == 0) ||
                (dir == Vector3Int.up       && w == wMax - lodScale) ||
                (dir == Vector3Int.back     && w == 0) ||
                (dir == Vector3Int.forward  && w == wMax - lodScale);

            
            // build mask for this slice
            for (int u = 0; u < uMax; u += lodScale)
            {
                int mu = u / lodScale;
                
                for (int v = 0; v < vMax; v += lodScale)
                {
                    int mv = v / lodScale;
                    
                    mask[mu, mv].occluded = false;
                    mask[mu, mv].atlasIndex = -1;

                    int x = 0, y = 0, z = 0;
                    if (dir.x != 0)
                    {
                        x = w;
                        y = u;
                        z = v;
                    }
                    else if (dir.y != 0)
                    {
                        x = u;
                        y = w;
                        z = v;
                    }
                    else // z != 0
                    {
                        x = u;
                        y = v;
                        z = w;
                    }

                    byte current = SampleBlock(getBlock, x, y, z, lodScale);
                    byte neighbor = SampleBlock(
                        getBlock,
                        x + dir.x * lodScale,
                        y + dir.y * lodScale,
                        z + dir.z * lodScale,
                        lodScale
                    );

                    bool forceFace = isBorderSlice && neighborScale < lodScale;

                    if (current != 0 && (neighbor == 0 || forceFace))
                    {
                        // Use the thread-safe BlockInfo table if available, fallback safe handling
                        int atlasIdx = -1;
                        var tbi = BlockRegistry.ThreadBlockInfo;
                        if (tbi != null && current >= 0 && current < tbi.Length)
                        {
                            var block = tbi[current];
                            BlockStateContainer state = getState?.Invoke(x, y, z);

                            if (block != null)
                            {
                                if (dir == Vector3Int.up) atlasIdx = block.topIndex;
                                else if (dir == Vector3Int.down) atlasIdx = block.bottomIndex;
                                else
                                {
                                    // Default to sides
                                    atlasIdx = block.sideIndex;
                                    
                                    //Check if the block has facing state
                                    string facing = state?.GetState("facing");
                                    bool isFront = false;
                                    if (facing != null)
                                    {
                                        if ((dir == Vector3Int.forward && facing == "north") ||
                                            (dir == Vector3Int.back && facing == "south") ||
                                            (dir == Vector3Int.right && facing == "east") ||
                                            (dir == Vector3Int.left && facing == "west"))
                                        {
                                            isFront = true;
                                        }

                                        atlasIdx = isFront ? block.frontIndex : block.sideIndex;
                                    }
                                }
                            }


                            // Legacy code;
                            //var info = tbi[current];
                            //atlasIdx = info.sideIndex;
                            //if (dir == Vector3Int.up) atlasIdx = info.topIndex;
                            //if (dir == Vector3Int.down) atlasIdx = info.bottomIndex;
                        }
                        else
                        {
                            // As a fallback (if ThreadBlockInfo not set), try to use main-thread BlockRegistry safely.
                            // WARNING: calling BlockRegistry.GetBlock on a worker thread is unsafe.
                            // On main thread it will work but in worker threads you must ensure ThreadBlockInfo is built.
                            var block = BlockRegistry.GetBlock((int)current);
                            if (block != null)
                            {
                                atlasIdx = block.sideIndex;
                                if (dir == Vector3Int.up) atlasIdx = block.topIndex;
                                if (dir == Vector3Int.down) atlasIdx = block.bottomIndex;
                            }
                        }

                        if (atlasIdx >= 0)
                        {
                            mask[mu, mv].occluded = true;
                            mask[mu, mv].atlasIndex = atlasIdx;
                        }
                        else
                        {
                            mask[mu, mv].occluded = false;
                        }
                    }
                }
            }

            // Greedy merge the mask into quads
            for (int u = 0; u < mSize; u++)
            {
                for (int v = 0; v < mSize;)
                {
                    if (!mask[u, v].occluded)
                    {
                        v++;
                        continue;
                    }

                    int atlasIndex = mask[u, v].atlasIndex;

                    // extend width (v direction)
                    int width = 1;
                    while (v + width < mSize && mask[u, v + width].occluded &&
                           mask[u, v + width].atlasIndex == atlasIndex)
                        width++;

                    // extend height (u direction)
                    int height = 1;
                    bool done = false;
                    while (u + height < mSize && !done)
                    {
                        for (int k = 0; k < width; k++)
                        {
                            if (!mask[u + height, v + k].occluded || mask[u + height, v + k].atlasIndex != atlasIndex)
                            {
                                done = true;
                                break;
                            }
                        }

                        if (!done) height++;
                    }

                    // clear mask region
                    for (int du = 0; du < height; du++)
                        for (int dv = 0; dv < width; dv++)
                            mask[u + du, v + dv].occluded = false;

                    // Add the merged quad: compute its 4 corner positions in chunk-local space
                    AddQuadFromMask(u, v, width, height, w, dir, atlasIndex, mesh, lodScale);
                    
                    // advance v cursor
                    v += width;
                }
            }

        } // end w loop
    }
    
    private static byte SampleBlock(
        Func<int,int,int,byte> getBlock,
        int x, int y, int z,
        int scale)
    {
        // search from TOP to BOTTOM
        for (int dy = scale - 1; dy >= 0; dy--)
        for (int dx = 0; dx < scale; dx++)
        for (int dz = 0; dz < scale; dz++)
        {
            byte b = getBlock(x + dx, y + dy, z + dz);
            if (b != 0)
                return b;
        }

        return 0;
    }
    

    
    private static void AddQuadFromMask(int u, int v, int width, int height, int w, Vector3Int dir, int atlasIndex, MeshData mesh, int lodScale)
    {
        if (width <= 0 || height <= 0) return;

        Vector3[] faceVerts = VoxelData.GetFaceVertices(dir);
        if (faceVerts == null || faceVerts.Length != 4) return;

        int bu = u * lodScale;
        int bv = v * lodScale;
        int bw = w;

        int blockWidth = width * lodScale;
        int blockHeight = height * lodScale;
        
        Vector3 offset;

        int faceOffset = dir.x + dir.y + dir.z > 0 ? lodScale : 0;
        
        if (dir.x != 0)
            offset = new Vector3(bw + faceOffset, bu, bv);
        else if (dir.y != 0)
            offset = new Vector3(bu, bw + faceOffset, bv);
        else
            offset = new Vector3(bu, bv, bw + faceOffset);


        int baseIndex = mesh.vertices.Count;

        // Build the 4 world positions for this quad. Match original position math
        foreach (Vector3 vert in faceVerts)
        {
            Vector3 pos;
            
            if (dir.x != 0)
                pos = offset
                      + vert.y * Vector3.up * blockHeight
                      + vert.z * Vector3.forward * blockWidth;
            else if (dir.y != 0)
                pos = offset
                      + vert.x * Vector3.right * blockHeight
                      + vert.z * Vector3.forward * blockWidth;
            else // dir.z != 0
                pos = offset
                      + vert.x * Vector3.right * blockHeight
                      + vert.y * Vector3.up * blockWidth;
            
            mesh.vertices.Add(pos);
        }


        // Triangles (winding check)
        int i0 = baseIndex + 0;
        int i1 = baseIndex + 1;
        int i2 = baseIndex + 2;
        int i3 = baseIndex + 3;

        int t0a = i0, t0b = i2, t0c = i1;
        int t1a = i2, t1b = i3, t1c = i1;

        Vector3 A = mesh.vertices[t0b] - mesh.vertices[t0a];
        Vector3 B = mesh.vertices[t0c] - mesh.vertices[t0a];
        Vector3 triNormal = Vector3.Cross(A, B);

        if (Vector3.Dot(triNormal, (Vector3)dir) < 0f)
        {
            // flip both triangles' winding (swap two indices)
            t0b = i1;
            t0c = i2;
            t1b = i1;
            t1c = i3;
        }

        mesh.triangles.Add(t0a);
        mesh.triangles.Add(t0b);
        mesh.triangles.Add(t0c);
        mesh.triangles.Add(t1a);
        mesh.triangles.Add(t1b);
        mesh.triangles.Add(t1c);

        // UVs and UV meta
        AddFaceUV(dir, atlasIndex, width, height, mesh);

        // Collider: add vertices then compute triangle areas and add non-degenerate triangles
        int colBase = mesh.colliderVertices.Count;
        mesh.colliderVertices.Add(mesh.vertices[baseIndex + 0]);
        mesh.colliderVertices.Add(mesh.vertices[baseIndex + 1]);
        mesh.colliderVertices.Add(mesh.vertices[baseIndex + 2]);
        mesh.colliderVertices.Add(mesh.vertices[baseIndex + 3]);

        int c0 = colBase + 0;
        int c1 = colBase + 1;
        int c2 = colBase + 2;
        int c3 = colBase + 3;

        const float areaEpsilon = 1e-6f;
        Vector3 caA = mesh.colliderVertices[c2] - mesh.colliderVertices[c0];
        Vector3 cbA = mesh.colliderVertices[c1] - mesh.colliderVertices[c0];
        float areaA = Vector3.Cross(caA, cbA).sqrMagnitude * 0.25f;

        if (areaA > areaEpsilon)
        {
            mesh.colliderTriangles.Add(c0);
            mesh.colliderTriangles.Add(c1);
            mesh.colliderTriangles.Add(c2);
        }

        Vector3 caB = mesh.colliderVertices[c3] - mesh.colliderVertices[c2];
        Vector3 cbB = mesh.colliderVertices[c1] - mesh.colliderVertices[c2];
        float areaB = Vector3.Cross(caB, cbB).sqrMagnitude * 0.25f;

        if (areaB > areaEpsilon)
        {
            mesh.colliderTriangles.Add(c2);
            mesh.colliderTriangles.Add(c1);
            mesh.colliderTriangles.Add(c3);
        }
    }



    private static bool ISZFace(Vector3Int dir) => dir.z != 0;

    private static void AddFaceUV(Vector3Int dir, int textureID, int width, int height, MeshData mesh)
    {
        int tiles = ATLAS_TILES;
        float tileSize = 1f / tiles;

        int col = textureID % tiles;
        int row = textureID / tiles;

        float uMin = col * tileSize;
        float vMax = 1f - row * tileSize; // top
        float vMin = vMax - tileSize; // bottom

        float uScale = width;
        float vScale = height;

        if (ISZFace(dir))
        {
            uScale = height;
            vScale = width;
        }

        mesh.uvs.Add(new Vector2(0f, 0f)); // vertex 0
        mesh.uvs.Add(new Vector2(uScale, 0f)); // vertex 1
        mesh.uvs.Add(new Vector2(0f, vScale)); // vertex 2
        mesh.uvs.Add(new Vector2(uScale, vScale)); // vertex 3

        Vector4 meta = new Vector4(uMin, vMin, tileSize, tileSize);
        mesh.uvMeta.Add(meta);
        mesh.uvMeta.Add(meta);
        mesh.uvMeta.Add(meta);
        mesh.uvMeta.Add(meta);
    }
}

public class ChunkMeshGenerator
{
    
    // Internal threaded mesher uses delegate getBlock. For compatibility we provide a simple wrapper that
    // uses the chunk.GetBlock method on the main thread (same behavior as original).
    public ChunkRendering.ChunkMeshData GenerateMesh(byte[,,] blocks, Chunk owner)
    {
        int lodScale = owner != null ? owner.GetLodScale() : 1;
        
        ChunkMeshGeneratorThreaded.NeighborLODInfo neighbors = new ChunkMeshGeneratorThreaded.NeighborLODInfo
        {
            posX = owner.chunkManager.GetChunk(owner.coord + Vector3Int.right)?.GetLodScale() ?? lodScale,
            negX = owner.chunkManager.GetChunk(owner.coord + Vector3Int.left )?.GetLodScale() ?? lodScale,
            posY = owner.chunkManager.GetChunk(owner.coord + Vector3Int.up   )?.GetLodScale() ?? lodScale,
            negY = owner.chunkManager.GetChunk(owner.coord + Vector3Int.down )?.GetLodScale() ?? lodScale,
            posZ = owner.chunkManager.GetChunk(owner.coord + Vector3Int.forward)?.GetLodScale() ?? lodScale,
            negZ = owner.chunkManager.GetChunk(owner.coord + Vector3Int.back   )?.GetLodScale() ?? lodScale,
        };
        
        // Provide the worker thread only a plain byte array (blocks)
        Func<int,int,int,byte> getBlock = (x,y,z) =>
        {
            if (owner != null)
            {
                return owner.GetBlock(x, y, z);
            }

            return 0;
        };

        Func<int, int, int, BlockStateContainer> getState = (x, y, z) =>
        {
            if (owner != null)
            {
                return owner.GetStateAt(x, y, z);
            }

            return null;
        };

        // Use threaded mesher to produce plain MeshData (runs on main thread here)
        MeshData meshData = ChunkMeshGeneratorThreaded.GenerateMeshData(getBlock,getState,lodScale, neighbors);

        // Convert MeshData to Unity Mesh objects (this MUST be done on main thread)
        return ConvertToChunkMeshData(meshData);
    }

    // Converts the plain MeshData -> ChunkRendering.ChunkMeshData with Unity Mesh objects.
    // This mirrors exactly your previous mesh creation steps (SetVertices, SetTriangles, SetUVs, UV1, Recalculate normals/tangents/bounds)
    private ChunkRendering.ChunkMeshData ConvertToChunkMeshData(MeshData md)
    {
        Mesh renderMesh = new Mesh();
        renderMesh.Clear();
        renderMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        renderMesh.SetVertices(md.vertices);
        renderMesh.SetTriangles(md.triangles, 0);
        renderMesh.SetUVs(0, md.uvs);
        renderMesh.SetUVs(1, md.uvMeta);
        renderMesh.RecalculateNormals();
        renderMesh.RecalculateTangents();
        renderMesh.RecalculateBounds();

        Mesh colliderMesh = new Mesh();
        colliderMesh.Clear();
        colliderMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        colliderMesh.SetVertices(md.colliderVertices);
        colliderMesh.SetTriangles(md.colliderTriangles, 0);
        colliderMesh.RecalculateNormals();
        colliderMesh.RecalculateTangents();
        colliderMesh.RecalculateBounds();

        return new ChunkRendering.ChunkMeshData
        {
            renderingMesh = renderMesh,
            colliderMesh = colliderMesh
        };
    }
}


