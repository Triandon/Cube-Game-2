using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Core.Block;
using UnityEngine;

public class ChunkMeshGenerator
{
    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();
    private List<Vector2> uvs = new List<Vector2>();
    private List<Vector4> uvMetaList = new List<Vector4>();

    private List<Vector3> colVertices = new List<Vector3>();
    private List<int> colTriangles = new List<int>();

    private Chunk chunk;
    private byte[,,] blocks;

    private struct MaskCell
    {
        public bool occluded; // false = empty, true = has face
        public int atlasIndex;
    }

    private bool flipTrianglesForPositive = false;

    public ChunkRendering.ChunkMeshData GenerateMesh(byte[,,] blocks, Chunk owner)
    {
        this.blocks = blocks;
        this.chunk = owner;

        vertices.Clear();
        triangles.Clear();
        uvs.Clear();
        uvMetaList.Clear();

        colVertices.Clear();
        colTriangles.Clear();

        // Run greedy for each of the six directions
        // Note: Vector3Int.right = +X, left = -X, up = +Y, down = -Y, forward = +Z, back = -Z
        GreedyDirection(Vector3Int.right);
        GreedyDirection(Vector3Int.left);
        GreedyDirection(Vector3Int.up);
        GreedyDirection(Vector3Int.down);
        GreedyDirection(Vector3Int.forward);
        GreedyDirection(Vector3Int.back);

        // Build render mesh
        Mesh renderMesh = new Mesh();
        renderMesh.Clear();
        renderMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        renderMesh.SetVertices(vertices);
        renderMesh.SetTriangles(triangles, 0);
        renderMesh.SetUVs(0, uvs);
        renderMesh.SetUVs(1, uvMetaList);
        renderMesh.RecalculateNormals();
        renderMesh.RecalculateTangents();
        renderMesh.RecalculateBounds();

        // Build collider mesh (reuse vertex winding from render mesh)
        Mesh colliderMesh = new Mesh();
        colliderMesh.Clear();
        colliderMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        colliderMesh.SetVertices(colVertices);
        colliderMesh.SetTriangles(colTriangles, 0);
        colliderMesh.RecalculateNormals();
        colliderMesh.RecalculateTangents();
        colliderMesh.RecalculateBounds();

        return new ChunkRendering.ChunkMeshData
        {
            renderingMesh = renderMesh,
            colliderMesh = colliderMesh
        };
    }

    // The core greedy function for a single axis direction
    private void GreedyDirection(Vector3Int dir)
    {
        int size = Chunk.CHUNK_SIZE;

        // We'll create a 2D mask with axes u (height) and v (width) depending on dir.
        // w is the slice index along the direction axis.
        // Mapping: For dir.x != 0, w -> x, u -> y, v -> z
        //          For dir.y != 0, w -> y, u -> x, v -> z
        //          For dir.z != 0, w -> z, u -> x, v -> y

        int uMax = size;
        int vMax = size;
        int wMax = size;

        MaskCell[,] mask = new MaskCell[uMax, vMax];

        for (int w = 0; w < wMax; w++)
        {
            // build mask for this slice
            for (int u = 0; u < uMax; u++)
            {
                for (int v = 0; v < vMax; v++)
                {
                    mask[u, v].occluded = false;
                    mask[u, v].atlasIndex = -1;

                    // convert local (u,v,w) into world/local chunk coordinates x,y,z
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
                    else // dir.z != 0
                    {
                        x = u;
                        y = v;
                        z = w;
                    }

                    // bounds check for reading blocks
                    // if outside chunk we will ask neighbor through Chunk.GetBlock which already handles neighbor coords
                    byte current = chunk.GetBlock(x, y, z);
                    byte neighbor = chunk.GetBlock(x + dir.x, y + dir.y, z + dir.z);

                    // A face exists here if current is solid (non-zero) and neighbor is air (zero)
                    if (current != 0 && neighbor == 0)
                    {
                        // get block info & atlas index for this face
                        Block block = BlockRegistry.GetBlock(current);
                        if (block == null)
                        {
                            mask[u, v].occluded = false;
                        }
                        else
                        {
                            int atlasIdx = block.sideIndex;
                            if (dir == Vector3Int.up) atlasIdx = block.topIndex;
                            if (dir == Vector3Int.down) atlasIdx = block.bottomIndex;

                            mask[u, v].occluded = true;
                            mask[u, v].atlasIndex = atlasIdx;
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
                    AddQuadFromMask(u, v, width, height, w, dir, atlasIndex);

                    // advance v cursor
                    v += width;
                }
            }
        }
    }

    // Add quad for merged mask rectangle. Positions are in chunk-local coordinates (0..CHUNK_SIZE).
    // Add quad for merged mask rectangle. Positions are in chunk-local coordinates (0..CHUNK_SIZE).
    private void AddQuadFromMask(int u, int v, int width, int height, int w, Vector3Int dir, int atlasIndex)
    {
        if (width <= 0 || height <= 0) return;

        Vector3[] faceVerts = VoxelData.GetFaceVertices(dir);
        if (faceVerts.Length != 4) return;

        // Compute offsets for greedy rectangle (same idea as your current code)
        Vector3 offset = Vector3.zero;

        if (dir.x != 0)
            offset = new Vector3(w + (dir.x > 0 ? 1f : 0f), u, v);
        else if (dir.y != 0)
            offset = new Vector3(u, w + (dir.y > 0 ? 1f : 0f), v);
        else // dir.z != 0
            offset = new Vector3(u, v, w + (dir.z > 0 ? 1f : 0f));

        int baseIndex = vertices.Count;

        // Build the 4 world positions for this quad. We keep your position math
        foreach (Vector3 vert in faceVerts)
        {
            Vector3 pos;
            if (dir.x != 0)
                pos = offset + vert.y * Vector3.up * height + vert.z * Vector3.forward * width;
            else if (dir.y != 0)
                pos = offset + vert.x * Vector3.right * height + vert.z * Vector3.forward * width;
            else // dir.z != 0
                pos = offset + vert.x * Vector3.right * height + vert.y * Vector3.up * width;

            vertices.Add(pos);
        }

        // --- Compute winding so the face normal points in the same direction as `dir` ---
        // We'll form triangles and then check the resulting face normal; if it points opposite dir,
        // we flip triangle winding.
        int i0 = baseIndex + 0;
        int i1 = baseIndex + 1;
        int i2 = baseIndex + 2;
        int i3 = baseIndex + 3;

        // Provisional triangles (these correspond to your previous ordering)
        int t0a = i0, t0b = i2, t0c = i1;
        int t1a = i2, t1b = i3, t1c = i1;

        // compute triangle normal (use first triangle)
        Vector3 A = vertices[t0b] - vertices[t0a];
        Vector3 B = vertices[t0c] - vertices[t0a];
        Vector3 triNormal = Vector3.Cross(A, B);

        // If dot(triNormal, dir) < 0 -> normal points opposite desired direction -> flip triangles
        if (Vector3.Dot(triNormal, (Vector3)dir) < 0f)
        {
            // flip both triangles' winding (swap two indices)
            t0b = i1;
            t0c = i2;
            t1b = i1;
            t1c = i3;
            // now t0 = (i0, i1, i2) ; t1 = (i2, i1, i3)
        }

        // Add triangles to render list
        triangles.Add(t0a);
        triangles.Add(t0b);
        triangles.Add(t0c);
        triangles.Add(t1a);
        triangles.Add(t1b);
        triangles.Add(t1c);

        // Add UVs (uses your existing AddFaceUV method)
        AddFaceUV(dir,atlasIndex, width, height);

        // --- Collider: add a quad into the collider lists but skip degenerate triangles ---
        int colBase = colVertices.Count;
        colVertices.Add(vertices[baseIndex + 0]);
        colVertices.Add(vertices[baseIndex + 1]);
        colVertices.Add(vertices[baseIndex + 2]);
        colVertices.Add(vertices[baseIndex + 3]);

        // Compute collider triangle indices relative to colBase
        int c0 = colBase + 0;
        int c1 = colBase + 1;
        int c2 = colBase + 2;
        int c3 = colBase + 3;

        // Two triangles to test for area
        // triA: (c0,c2,c1)
        // triB: (c2,c3,c1)
        // Compute areas (via cross) and only add non-degenerate triangles.
        const float areaEpsilon = 1e-6f;

        Vector3 caA = colVertices[c2] - colVertices[c0];
        Vector3 cbA = colVertices[c1] - colVertices[c0];
        float areaA = Vector3.Cross(caA, cbA).sqrMagnitude * 0.25f; // squared area proxy

        if (areaA > areaEpsilon)
        {
            colTriangles.Add(c0);
            colTriangles.Add(c1);
            colTriangles.Add(c2);
        }

        Vector3 caB = colVertices[c3] - colVertices[c2];
        Vector3 cbB = colVertices[c1] - colVertices[c2];
        float areaB = Vector3.Cross(caB, cbB).sqrMagnitude * 0.25f;

        if (areaB > areaEpsilon)
        {
            colTriangles.Add(c2);
            colTriangles.Add(c1);
            colTriangles.Add(c3);
        }
    }


    private bool ISZFace(Vector3Int dir)
    {
        return dir.z != 0;
    }



    // Add UVs for a single quad but scale the UV area so a tile repeats across width/height.
// This prevents texture stretching across merged faces.
    // Choose an offset multiplier larger than max possible repeat (safe: 1000)

    private void AddFaceUV(Vector3Int dir,int textureID, int width, int height)
    {
        int tiles = 16; // atlas dimension
        float tileSize = 1f / tiles;

        int col = textureID % tiles;
        int row = textureID / tiles;

        float uMin = col * tileSize;
        float vMax = 1f - row * tileSize; // top
        float vMin = vMax - tileSize; // bottom

        //Default scaling (x faces & Y faces)
        float uScale = width;
        float vScale = height;

        if (ISZFace(dir))
        {   //Just swapps the sides
            uScale = height;
            vScale = width;
        }

        // UV0: block-space coordinates so frac(uv0) will repeat each 1.0 unit
        uvs.Add(new Vector2(0f, 0f)); // vertex 0
        uvs.Add(new Vector2(uScale, 0f)); // vertex 1
        uvs.Add(new Vector2(0f, vScale)); // vertex 2
        uvs.Add(new Vector2(uScale, vScale)); // vertex 3

        // UV meta (Vector4): (uMin, vMin, tileSizeX, tileSizeY)
        Vector4 meta = new Vector4(uMin, vMin, tileSize, tileSize);
        uvMetaList.Add(meta);
        uvMetaList.Add(meta);
        uvMetaList.Add(meta);
        uvMetaList.Add(meta);
    }

}