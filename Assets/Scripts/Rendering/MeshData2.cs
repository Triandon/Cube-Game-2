using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Core.Block;
using UnityEngine;

public static class ChunkMeshGeneratorThreaded
{
    private const int CHUNK_SIZE = Chunk.CHUNK_SIZE;
    private const int ATLAS_TILES = 16;
    
    private struct MaskCell
    {
        public bool occluded;
        public int atlasIndex;
    }
    
    // Voxel face corner coords helper: same concept as VoxelData.GetFaceVertices(dir)
    // We'll inline a small helper to return the same 4 corner vectors for each face:
    private static Vector3[] GetFaceVerts(Vector3Int dir)
    {
        // The original code used VoxelData.GetFaceVertices(dir).
        // This returns 4 vectors in a particular ordering that your original mesher expects.
        // We'll return the same ordering:
        // For a face oriented towards +X (right): the four corners in local chunk coordinates
        // (these assume unit cube corners at (0 or 1) in each axis)
        if (dir == Vector3Int.right)
        {
            // +X face: corners (1,0,0), (1,0,1), (1,1,0), (1,1,1) but we must match the original ordering used.
            return new Vector3[]
            {
                new Vector3(1f, 0f, 0f),
                new Vector3(1f, 0f, 1f),
                new Vector3(1f, 1f, 0f),
                new Vector3(1f, 1f, 1f)
            };
        }
        if (dir == Vector3Int.left)
        {
            return new Vector3[]
            {
                new Vector3(0f, 0f, 1f),
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 1f, 1f),
                new Vector3(0f, 1f, 0f)
            };
        }
        if (dir == Vector3Int.up)
        {
            return new Vector3[]
            {
                new Vector3(0f, 1f, 0f),
                new Vector3(1f, 1f, 0f),
                new Vector3(0f, 1f, 1f),
                new Vector3(1f, 1f, 1f)
            };
        }
        if (dir == Vector3Int.down)
        {
            return new Vector3[]
            {
                new Vector3(0f, 0f, 1f),
                new Vector3(1f, 0f, 1f),
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f)
            };
        }
        if (dir == Vector3Int.forward)
        {
            return new Vector3[]
            {
                new Vector3(0f, 0f, 1f),
                new Vector3(1f, 0f, 1f),
                new Vector3(0f, 1f, 1f),
                new Vector3(1f, 1f, 1f)
            };
        }
        // back
        return new Vector3[]
        {
            new Vector3(1f, 0f, 0f),
            new Vector3(0f, 0f, 0f),
            new Vector3(1f, 1f, 0f),
            new Vector3(0f, 1f, 0f)
        };
    }
    
    public static MeshData GenerateMeshData(Func<int,int,int,byte> getBlock)
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
            GreedyDirection(getBlock, dir, mesh, mask);
        }

        return mesh;
    }
    
    // Greedy direction implementation adapted to be fully data-only and match original behavior
    private static void GreedyDirection(Func<int,int,int,byte> getBlock, Vector3Int dir, MeshData mesh, MaskCell[,] mask)
    {
        int uMax = CHUNK_SIZE;
        int vMax = CHUNK_SIZE;
        int wMax = CHUNK_SIZE;

        for (int w = 0; w < wMax; w++)
        {
            // build mask for this slice
            for (int u = 0; u < uMax; u++)
            {
                for (int v = 0; v < vMax; v++)
                {
                    mask[u, v].occluded = false;
                    mask[u, v].atlasIndex = -1;

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

                    byte current = getBlock(x, y, z);
                    byte neighbor = getBlock(x + dir.x, y + dir.y, z + dir.z);

                    if (current != 0 && neighbor == 0)
                    {
                        // Use the thread-safe BlockInfo table if available, fallback safe handling
                        int atlasIdx = -1;
                        var tbi = BlockRegistry.ThreadBlockInfo;
                        if (tbi != null && current >= 0 && current < tbi.Length)
                        {
                            var info = tbi[current];
                            atlasIdx = info.sideIndex;
                            if (dir == Vector3Int.up) atlasIdx = info.topIndex;
                            if (dir == Vector3Int.down) atlasIdx = info.bottomIndex;
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
                            mask[u, v].occluded = true;
                            mask[u, v].atlasIndex = atlasIdx;
                        }
                        else
                        {
                            mask[u, v].occluded = false;
                        }
                    }
                }
            }

            // Greedy merge the mask into quads
            for (int u = 0; u < uMax; u++)
            {
                for (int v = 0; v < vMax;)
                {
                    if (!mask[u, v].occluded)
                    {
                        v++;
                        continue;
                    }

                    int atlasIndex = mask[u, v].atlasIndex;

                    // extend width (v direction)
                    int width = 1;
                    while (v + width < vMax && mask[u, v + width].occluded &&
                           mask[u, v + width].atlasIndex == atlasIndex)
                        width++;

                    // extend height (u direction)
                    int height = 1;
                    bool done = false;
                    while (u + height < uMax && !done)
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
                    AddQuadFromMask(u, v, width, height, w, dir, atlasIndex, mesh);

                    // advance v cursor
                    v += width;
                }
            }

        } // end w loop
    }
    
    private static void AddQuadFromMask(int u, int v, int width, int height, int w, Vector3Int dir, int atlasIndex, MeshData mesh)
    {
        if (width <= 0 || height <= 0) return;

        Vector3[] faceVerts = GetFaceVerts(dir);
        if (faceVerts == null || faceVerts.Length != 4) return;

        Vector3 offset = Vector3.zero;

        if (dir.x != 0)
            offset = new Vector3(w + (dir.x > 0 ? 1f : 0f), u, v);
        else if (dir.y != 0)
            offset = new Vector3(u, w + (dir.y > 0 ? 1f : 0f), v);
        else // dir.z != 0
            offset = new Vector3(u, v, w + (dir.z > 0 ? 1f : 0f));

        int baseIndex = mesh.vertices.Count;

        // Build the 4 world positions for this quad. Match original position math
        foreach (Vector3 vert in faceVerts)
        {
            Vector3 pos;
            if (dir.x != 0)
                pos = offset + vert.y * Vector3.up * height + vert.z * Vector3.forward * width;
            else if (dir.y != 0)
                pos = offset + vert.x * Vector3.right * height + vert.z * Vector3.forward * width;
            else // dir.z != 0
                pos = offset + vert.x * Vector3.right * height + vert.y * Vector3.up * width;

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
        // Create a getBlock delegate that calls the chunk's GetBlock method (same logic as before)
        Func<int,int,int,byte> getBlock = (x,y,z) =>
        {
            // chunk.GetBlock handles out-of-range neighbour queries by requesting neighbour chunks,
            // so this exactly matches your previous behavior when running on main thread.
            return owner.GetBlock(x,y,z);
        };

        // Use threaded mesher to produce plain MeshData (runs on main thread here)
        MeshData meshData = ChunkMeshGeneratorThreaded.GenerateMeshData(getBlock);

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
    
    public static ChunkRendering.ChunkMeshData ThreadedMeshDataToChunkMeshData(MeshData md)
    {
        var wrapper = new ChunkMeshGenerator();
        return wrapper.ConvertToChunkMeshData(md);
    }
}


