using System;
using System.Collections.Generic;
using System.Globalization;
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
    public List<Vector3> normals = new List<Vector3>();

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
        public float plane;
        public float height;
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
        
        AddCustomBlockMeshes(getBlock, getState, mesh, lodScale);
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

                    if (IsCustomMeshBlock(current, getState?.Invoke(x, y, z)))
                    {
                        continue;
                    }

                    bool neighborFullyCoversFace = false;
                    if (current != 0 && neighbor != 0)
                    {
                        BlockShape fullCube = new BlockShape { min = Vector3.zero, max = Vector3.one };
                        if (TryGetBlockShape(
                                neighbor,
                                getState?.Invoke(x + dir.x * lodScale, y + dir.y * lodScale, z + dir.z * lodScale),
                                out BlockShape neighborShape))
                        {
                            neighborFullyCoversFace = IsFaceCovered(fullCube, neighborShape, dir);
                        }
                    }



                    if (current != 0 && (neighbor == 0 || forceFace || !neighborFullyCoversFace))
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
    
    private struct BlockShape
    {
        public Vector3 min;
        public Vector3 max;
    }

    private static bool IsCustomMeshBlock(byte blockId, BlockStateContainer state)
    {
        if (blockId == 0) return false;
        var tbi = BlockRegistry.ThreadBlockInfo;
        if (tbi == null || blockId < 0 || blockId >= tbi.Length) return false;

        Block block = tbi[blockId];
        if (block == null) return false;

        if (TryGetBlockShape(blockId, state, out BlockShape shape))
            return !IsFullShape(shape);

        return false;
    }

    private static bool IsFullShape(BlockShape shape)
    {
        const float eps = 0.0001f;
        return Mathf.Abs(shape.min.x) < eps && Mathf.Abs(shape.min.y) < eps && Mathf.Abs(shape.min.z) < eps &&
               Mathf.Abs(shape.max.x - 1f) < eps && Mathf.Abs(shape.max.y - 1f) < eps && Mathf.Abs(shape.max.z - 1f) < eps;
    }

    private static bool TryGetBlockShape(byte blockId, BlockStateContainer state, out BlockShape shape)
    {
        shape = default;

        if (blockId == 0) return false;

        var tbi = BlockRegistry.ThreadBlockInfo;
        if (tbi == null || blockId < 0 || blockId >= tbi.Length)
            return false;

        Block block = tbi[blockId];
        if (block == null)
            return false;

        float height = 1f;
        string h = GetStateValueOrDefault(block, state, BlockStateKeys.HeightState);
        
        if (!string.IsNullOrEmpty(h))
            float.TryParse(h, NumberStyles.Float, CultureInfo.InvariantCulture, out height);

        height = Mathf.Clamp(height, 0.1f, 1f);

        string orientation = GetStateValueOrDefault(block, state, BlockStateKeys.DirectionalFacing) ?? "up";
        shape.min = Vector3.zero;
        shape.max = Vector3.one;

        switch (orientation)
        {
            case "down":
                shape.min.y = 1f - height;
                break;
            case "north":
                shape.min.z = 1f - height;
                break;
            case "south":
                shape.max.z = height;
                break;
            case "east":
                shape.min.x = 1f - height;
                break;
            case "west":
                shape.max.x = height;
                break;
            default: // up
                shape.max.y = height;
                break;
        }

        return true;
    }
    
    private static string GetStateValueOrDefault(Block block, BlockStateContainer state, string stateKey)
    {
        string value = state?.GetState(stateKey);
        if (!string.IsNullOrEmpty(value))
            return value;

        return block?.GetState(stateKey);
    }

    
    private static bool IsHorizontalSlabCandidate(BlockShape shape)
    {
        const float eps = 0.0001f;
        return Mathf.Abs(shape.min.x) < eps && Mathf.Abs(shape.max.x - 1f) < eps &&
               Mathf.Abs(shape.min.z) < eps && Mathf.Abs(shape.max.z - 1f) < eps &&
               shape.max.y - shape.min.y < 1f - eps;
    }

    private static bool IsVerticalSideGreedyCandidate(BlockShape shape, Vector3Int dir)
    {
        const float eps = 0.0001f;
        bool fullY = Mathf.Abs(shape.min.y) < eps && Mathf.Abs(shape.max.y - 1f) < eps;
        if (!fullY)
            return false;

        if (dir.x != 0)
        {
            bool fullZ = Mathf.Abs(shape.min.z) < eps && Mathf.Abs(shape.max.z - 1f) < eps;
            float xThickness = shape.max.x - shape.min.x;
            return fullZ && xThickness < 1f - eps;
        }

        if (dir.z != 0)
        {
            bool fullX = Mathf.Abs(shape.min.x) < eps && Mathf.Abs(shape.max.x - 1f) < eps;
            float zThickness = shape.max.z - shape.min.z;
            return fullX && zThickness < 1f - eps;
        }

        return false;
    }

    private static void AddGreedyHorizontalSlabFaces(
        Func<int, int, int, byte> getBlock,
        Func<int, int, int, BlockStateContainer> getState,
        MeshData mesh)
    {
        int mSize = CHUNK_SIZE;
        MaskCell[,] mask = new MaskCell[mSize, mSize];
        Vector3Int[] dirs = { Vector3Int.up, Vector3Int.down };

        foreach (Vector3Int dir in dirs)
        {
            for (int y = 0; y < CHUNK_SIZE; y++)
            {
                for (int x = 0; x < CHUNK_SIZE; x++)
                for (int z = 0; z < CHUNK_SIZE; z++)
                {
                    mask[x, z].occluded = false;
                    mask[x, z].atlasIndex = -1;
                    mask[x, z].plane = 0f;
                    mask[x, z].height = 0f;

                    byte blockId = getBlock(x, y, z);
                    if (blockId == 0 || !TryGetBlockShape(blockId, getState?.Invoke(x, y, z), out BlockShape shape))
                        continue;
                    if (!IsHorizontalSlabCandidate(shape))
                        continue;
                    if (!ShouldRenderShapeFace(getBlock, getState, new Vector3Int(x, y, z), shape, dir))
                        continue;

                    Block block = BlockRegistry.ThreadBlockInfo[blockId];
                    if (block == null) continue;

                    mask[x, z].occluded = true;
                    mask[x, z].atlasIndex = dir == Vector3Int.up ? block.topIndex : block.bottomIndex;
                    mask[x, z].plane = y + (dir == Vector3Int.up ? shape.max.y : shape.min.y);
                    mask[x, z].height = shape.max.y - shape.min.y;
                }

                for (int x = 0; x < mSize; x++)
                {
                    for (int z = 0; z < mSize;)
                    {
                        if (!mask[x, z].occluded)
                        {
                            z++;
                            continue;
                        }

                        int atlas = mask[x, z].atlasIndex;
                        float plane = mask[x, z].plane;
                        float h = mask[x, z].height;

                        int width = 1;
                        while (z + width < mSize && mask[x, z + width].occluded &&
                               mask[x, z + width].atlasIndex == atlas &&
                               Mathf.Abs(mask[x, z + width].plane - plane) < 0.0001f &&
                               Mathf.Abs(mask[x, z + width].height - h) < 0.0001f)
                            width++;

                        int height = 1;
                        bool done = false;
                        while (x + height < mSize && !done)
                        {
                            for (int k = 0; k < width; k++)
                            {
                                MaskCell c = mask[x + height, z + k];
                                if (!c.occluded || c.atlasIndex != atlas ||
                                    Mathf.Abs(c.plane - plane) > 0.0001f ||
                                    Mathf.Abs(c.height - h) > 0.0001f)
                                {
                                    done = true;
                                    break;
                                }
                            }

                            if (!done) height++;
                        }

                        for (int dx = 0; dx < height; dx++)
                        for (int dz = 0; dz < width; dz++)
                            mask[x + dx, z + dz].occluded = false;

                        AddHorizontalShapeQuad(x, z, width, height, plane, dir, atlas, mesh);
                        z += width;
                    }
                }
            }
        }
    }

    private static void AddGreedyVerticalSideFaces(
        Func<int, int, int, byte> getBlock,
        Func<int, int, int, BlockStateContainer> getState,
        MeshData mesh)
    {
        int mSize = CHUNK_SIZE;
        MaskCell[,] mask = new MaskCell[mSize, mSize];

        // X-facing planes (merge in Y/Z)
        foreach (Vector3Int dir in new[] { Vector3Int.right, Vector3Int.left })
        {
            for (int x = 0; x < CHUNK_SIZE; x++)
            {
                for (int y = 0; y < CHUNK_SIZE; y++)
                for (int z = 0; z < CHUNK_SIZE; z++)
                {
                    mask[y, z].occluded = false;
                    mask[y, z].atlasIndex = -1;
                    mask[y, z].plane = 0f;
                    mask[y, z].height = 0f;

                    byte blockId = getBlock(x, y, z);
                    if (blockId == 0 || !TryGetBlockShape(blockId, getState?.Invoke(x, y, z), out BlockShape shape))
                        continue;
                    if (!IsVerticalSideGreedyCandidate(shape, dir))
                        continue;
                    if (!ShouldRenderShapeFace(getBlock, getState, new Vector3Int(x, y, z), shape, dir))
                        continue;

                    Block block = BlockRegistry.ThreadBlockInfo[blockId];
                    if (block == null) continue;

                    mask[y, z].occluded = true;
                    mask[y, z].atlasIndex = block.sideIndex;
                    mask[y, z].plane = x + (dir == Vector3Int.right ? shape.max.x : shape.min.x);
                    mask[y, z].height = shape.max.x - shape.min.x;
                }

                for (int y = 0; y < mSize; y++)
                {
                    for (int z = 0; z < mSize;)
                    {
                        if (!mask[y, z].occluded)
                        {
                            z++;
                            continue;
                        }

                        int atlas = mask[y, z].atlasIndex;
                        float plane = mask[y, z].plane;
                        float h = mask[y, z].height;

                        int width = 1;
                        while (z + width < mSize && mask[y, z + width].occluded &&
                               mask[y, z + width].atlasIndex == atlas &&
                               Mathf.Abs(mask[y, z + width].plane - plane) < 0.0001f &&
                               Mathf.Abs(mask[y, z + width].height - h) < 0.0001f)
                            width++;

                        int height = 1;
                        bool done = false;
                        while (y + height < mSize && !done)
                        {
                            for (int k = 0; k < width; k++)
                            {
                                MaskCell c = mask[y + height, z + k];
                                if (!c.occluded || c.atlasIndex != atlas ||
                                    Mathf.Abs(c.plane - plane) > 0.0001f ||
                                    Mathf.Abs(c.height - h) > 0.0001f)
                                {
                                    done = true;
                                    break;
                                }
                            }

                            if (!done) height++;
                        }

                        for (int dy = 0; dy < height; dy++)
                        for (int dz = 0; dz < width; dz++)
                            mask[y + dy, z + dz].occluded = false;

                        AddVerticalXShapeQuad(y, z, width, height, plane, dir, atlas, mesh);
                        z += width;
                    }
                }
            }
        }

        // Z-facing planes (merge in Y/X)
        foreach (Vector3Int dir in new[] { Vector3Int.forward, Vector3Int.back })
        {
            for (int z = 0; z < CHUNK_SIZE; z++)
            {
                for (int y = 0; y < CHUNK_SIZE; y++)
                for (int x = 0; x < CHUNK_SIZE; x++)
                {
                    mask[y, x].occluded = false;
                    mask[y, x].atlasIndex = -1;
                    mask[y, x].plane = 0f;
                    mask[y, x].height = 0f;

                    byte blockId = getBlock(x, y, z);
                    if (blockId == 0 || !TryGetBlockShape(blockId, getState?.Invoke(x, y, z), out BlockShape shape))
                        continue;
                    if (!IsVerticalSideGreedyCandidate(shape, dir))
                        continue;
                    if (!ShouldRenderShapeFace(getBlock, getState, new Vector3Int(x, y, z), shape, dir))
                        continue;

                    Block block = BlockRegistry.ThreadBlockInfo[blockId];
                    if (block == null) continue;

                    mask[y, x].occluded = true;
                    mask[y, x].atlasIndex = block.sideIndex;
                    mask[y, x].plane = z + (dir == Vector3Int.forward ? shape.max.z : shape.min.z);
                    mask[y, x].height = shape.max.z - shape.min.z;
                }

                for (int y = 0; y < mSize; y++)
                {
                    for (int x = 0; x < mSize;)
                    {
                        if (!mask[y, x].occluded)
                        {
                            x++;
                            continue;
                        }

                        int atlas = mask[y, x].atlasIndex;
                        float plane = mask[y, x].plane;
                        float h = mask[y, x].height;

                        int width = 1;
                        while (x + width < mSize && mask[y, x + width].occluded &&
                               mask[y, x + width].atlasIndex == atlas &&
                               Mathf.Abs(mask[y, x + width].plane - plane) < 0.0001f &&
                               Mathf.Abs(mask[y, x + width].height - h) < 0.0001f)
                            width++;

                        int height = 1;
                        bool done = false;
                        while (y + height < mSize && !done)
                        {
                            for (int k = 0; k < width; k++)
                            {
                                MaskCell c = mask[y + height, x + k];
                                if (!c.occluded || c.atlasIndex != atlas ||
                                    Mathf.Abs(c.plane - plane) > 0.0001f ||
                                    Mathf.Abs(c.height - h) > 0.0001f)
                                {
                                    done = true;
                                    break;
                                }
                            }

                            if (!done) height++;
                        }

                        for (int dy = 0; dy < height; dy++)
                        for (int dx = 0; dx < width; dx++)
                            mask[y + dy, x + dx].occluded = false;

                        AddVerticalZShapeQuad(y, x, width, height, plane, dir, atlas, mesh);
                        x += width;
                    }
                }
            }
        }
    }

    private static void AddHorizontalShapeQuad(int x, int z, int width, int height, float planeY, Vector3Int dir, int atlas, MeshData mesh)
    {
        Vector3[] verts =
        {
            new Vector3(x, planeY, z),
            new Vector3(x + height, planeY, z),
            new Vector3(x, planeY, z + width),
            new Vector3(x + height, planeY, z + width)
        };

        int baseIndex = mesh.vertices.Count;
        mesh.vertices.AddRange(verts);
        for (int i = 0; i < 4; i++) mesh.normals.Add(dir);

        int i0 = baseIndex + 0;
        int i1 = baseIndex + 1;
        int i2 = baseIndex + 2;
        int i3 = baseIndex + 3;

        int t0a = i0, t0b = i2, t0c = i1;
        int t1a = i2, t1b = i3, t1c = i1;

        Vector3 triA = mesh.vertices[t0b] - mesh.vertices[t0a];
        Vector3 triB = mesh.vertices[t0c] - mesh.vertices[t0a];
        Vector3 triNormal = Vector3.Cross(triA, triB);
        if (Vector3.Dot(triNormal, (Vector3)dir) < 0f)
        {
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
        AddFaceUV(dir, atlas, height, width, mesh);

        int cb = mesh.colliderVertices.Count;
        mesh.colliderVertices.AddRange(verts);

        int c0 = cb + 0;
        int c1 = cb + 1;
        int c2 = cb + 2;
        int c3 = cb + 3;

        // Match collider winding with the final render winding for correct one-sided collision.
        int ct0a = c0, ct0b = c2, ct0c = c1;
        int ct1a = c2, ct1b = c3, ct1c = c1;

        if (Vector3.Dot(triNormal, (Vector3)dir) < 0f)
        {
            ct0b = c1;
            ct0c = c2;
            ct1b = c1;
            ct1c = c3;
        }

        mesh.colliderTriangles.Add(ct0a);
        mesh.colliderTriangles.Add(ct0b);
        mesh.colliderTriangles.Add(ct0c);
        mesh.colliderTriangles.Add(ct1a);
        mesh.colliderTriangles.Add(ct1b);
        mesh.colliderTriangles.Add(ct1c);
    }

    private static void AddVerticalXShapeQuad(int y, int z, int widthZ, int heightY, float planeX, Vector3Int dir, int atlas, MeshData mesh)
    {
        // Vertex order is chosen so UV-U runs along Z (width) and UV-V runs along Y (height)
        // to match greedy tiling direction expectations (x x x across, not stacked vertically).
        Vector3[] verts =
        {
            new Vector3(planeX, y, z),
            new Vector3(planeX, y, z + widthZ),
            new Vector3(planeX, y + heightY, z),
            new Vector3(planeX, y + heightY, z + widthZ)
        };

        AddGreedyShapeQuad(verts, dir, atlas, widthZ, heightY, mesh);
    }

    private static void AddVerticalZShapeQuad(int y, int x, int widthX, int heightY, float planeZ, Vector3Int dir, int atlas, MeshData mesh)
    {
        Vector3[] verts =
        {
            new Vector3(x, y, planeZ),
            new Vector3(x + widthX, y, planeZ),
            new Vector3(x, y + heightY, planeZ),
            new Vector3(x + widthX, y + heightY, planeZ)
        };

        // Z-faces swap width/height in AddFaceUV internally, so feed swapped UV scales to preserve tiling.
        AddGreedyShapeQuad(verts, dir, atlas, heightY, widthX, mesh);
    }

    private static void AddGreedyShapeQuad(Vector3[] verts, Vector3Int dir, int atlas, int uvWidth, int uvHeight, MeshData mesh)
    {
        int baseIndex = mesh.vertices.Count;
        mesh.vertices.AddRange(verts);
        for (int i = 0; i < 4; i++) mesh.normals.Add(dir);

        int i0 = baseIndex + 0;
        int i1 = baseIndex + 1;
        int i2 = baseIndex + 2;
        int i3 = baseIndex + 3;

        int t0a = i0, t0b = i2, t0c = i1;
        int t1a = i2, t1b = i3, t1c = i1;

        Vector3 triA = mesh.vertices[t0b] - mesh.vertices[t0a];
        Vector3 triB = mesh.vertices[t0c] - mesh.vertices[t0a];
        Vector3 triNormal = Vector3.Cross(triA, triB);
        if (Vector3.Dot(triNormal, (Vector3)dir) < 0f)
        {
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
        AddFaceUV(dir, atlas, uvWidth, uvHeight, mesh);

        int cb = mesh.colliderVertices.Count;
        mesh.colliderVertices.AddRange(verts);

        int c0 = cb + 0;
        int c1 = cb + 1;
        int c2 = cb + 2;
        int c3 = cb + 3;

        // Match collider winding with the final render winding for correct one-sided collision.
        int ct0a = c0, ct0b = c2, ct0c = c1;
        int ct1a = c2, ct1b = c3, ct1c = c1;

        if (Vector3.Dot(triNormal, (Vector3)dir) < 0f)
        {
            ct0b = c1;
            ct0c = c2;
            ct1b = c1;
            ct1c = c3;
        }

        mesh.colliderTriangles.Add(ct0a);
        mesh.colliderTriangles.Add(ct0b);
        mesh.colliderTriangles.Add(ct0c);
        mesh.colliderTriangles.Add(ct1a);
        mesh.colliderTriangles.Add(ct1b);
        mesh.colliderTriangles.Add(ct1c);
    }
    
    private static void AddCustomBlockMeshes(Func<int,int,int,byte> getBlock,
        Func<int, int, int, BlockStateContainer> getState,
        MeshData mesh,
        int lodScale)
    {
        if (lodScale != 1)
            return;

        AddGreedyHorizontalSlabFaces(getBlock, getState, mesh);
        AddGreedyVerticalSideFaces(getBlock, getState, mesh);
        
        for (int x = 0; x < CHUNK_SIZE; x++)
        for (int y = 0; y < CHUNK_SIZE; y++)
        for (int z = 0; z < CHUNK_SIZE; z++)
        {
            byte blockId = getBlock(x, y, z);
            BlockStateContainer state = getState?.Invoke(x, y, z);
            if (!IsCustomMeshBlock(blockId, state))
                continue;

            var block = BlockRegistry.ThreadBlockInfo[blockId];
            if (!TryGetBlockShape(blockId, state, out BlockShape shape))
                continue;
            
            bool handledByHorizontalGreedy = IsHorizontalSlabCandidate(shape);
            bool handledByXSideGreedy = IsVerticalSideGreedyCandidate(shape, Vector3Int.right);
            bool handledByZSideGreedy = IsVerticalSideGreedyCandidate(shape, Vector3Int.forward);

            if (!handledByHorizontalGreedy)
            {
                AddShapeFace(getBlock, getState, mesh, new Vector3Int(x, y, z), shape, Vector3Int.up, block.topIndex);
                AddShapeFace(getBlock, getState, mesh, new Vector3Int(x, y, z), shape, Vector3Int.down, block.bottomIndex);
            }
            if (!handledByXSideGreedy)
            {
                AddShapeFace(getBlock, getState, mesh, new Vector3Int(x, y, z), shape, Vector3Int.right, block.sideIndex);
                AddShapeFace(getBlock, getState, mesh, new Vector3Int(x, y, z), shape, Vector3Int.left, block.sideIndex);
            }

            if (!handledByZSideGreedy)
            {
                AddShapeFace(getBlock, getState, mesh, new Vector3Int(x, y, z), shape, Vector3Int.forward, block.sideIndex);
                AddShapeFace(getBlock, getState, mesh, new Vector3Int(x, y, z), shape, Vector3Int.back, block.sideIndex);
            }

        }
    }

    private static void AddShapeFace(Func<int,int,int,byte> getBlock,
        Func<int, int, int, BlockStateContainer> getState,
        MeshData mesh,
        Vector3Int pos,
        BlockShape shape,
        Vector3Int dir,
        int atlasIndex)
    {
        if (atlasIndex < 0 || !ShouldRenderShapeFace(getBlock, getState, pos, shape, dir))
            return;

        Vector3[] faceVerts = VoxelData.GetFaceVertices(dir);
        if (faceVerts == null || faceVerts.Length != 4) return;

        Vector3 min = shape.min;
        Vector3 max = shape.max;

        Vector3 scale = max - min;
        int baseIndex = mesh.vertices.Count;
        foreach (Vector3 vert in faceVerts)
        {
            Vector3 local = new Vector3(
                min.x + vert.x * scale.x,
                min.y + vert.y * scale.y,
                min.z + vert.z * scale.z);
            mesh.vertices.Add(pos + local);
            mesh.normals.Add(dir);
        }

        int i0 = baseIndex + 0;
        int i1 = baseIndex + 1;
        int i2 = baseIndex + 2;
        int i3 = baseIndex + 3;

        int t0a = i0, t0b = i2, t0c = i1;
        int t1a = i2, t1b = i3, t1c = i1;

        Vector3 triA = mesh.vertices[t0b] - mesh.vertices[t0a];
        Vector3 triB = mesh.vertices[t0c] - mesh.vertices[t0a];
        Vector3 triNormal = Vector3.Cross(triA, triB);

        if (Vector3.Dot(triNormal, (Vector3)dir) < 0f)
        {
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

        int width = Mathf.RoundToInt(Mathf.Max(1f, (dir.y != 0 ? scale.x : dir.x != 0 ? scale.z : scale.x)));
        int height = Mathf.RoundToInt(Mathf.Max(1f, (dir.y != 0 ? scale.z : dir.x != 0 ? scale.y : scale.y)));
        AddFaceUV(dir, atlasIndex, width, height, mesh);

        int colBase = mesh.colliderVertices.Count;
        mesh.colliderVertices.Add(mesh.vertices[baseIndex + 0]);
        mesh.colliderVertices.Add(mesh.vertices[baseIndex + 1]);
        mesh.colliderVertices.Add(mesh.vertices[baseIndex + 2]);
        mesh.colliderVertices.Add(mesh.vertices[baseIndex + 3]);

        int c0 = colBase + 0;
        int c1 = colBase + 1;
        int c2 = colBase + 2;
        int c3 = colBase + 3;

        int ct0a = c0, ct0b = c2, ct0c = c1;
        int ct1a = c2, ct1b = c3, ct1c = c1;

        if (Vector3.Dot(triNormal, (Vector3)dir) < 0f)
        {
            ct0b = c1;
            ct0c = c2;
            ct1b = c1;
            ct1c = c3;
        }

        mesh.colliderTriangles.Add(ct0a);
        mesh.colliderTriangles.Add(ct0b);
        mesh.colliderTriangles.Add(ct0c);
        mesh.colliderTriangles.Add(ct1a);
        mesh.colliderTriangles.Add(ct1b);
        mesh.colliderTriangles.Add(ct1c);
    }
    
    private static bool ShouldRenderShapeFace(Func<int,int,int,byte> getBlock,
        Func<int, int, int, BlockStateContainer> getState,
        Vector3Int pos,
        BlockShape shape,
        Vector3Int dir)
    {
        Vector3Int neighborPos = pos + dir;
        byte neighborId = getBlock(neighborPos.x, neighborPos.y, neighborPos.z);
        if (neighborId == 0)
            return true;

        if (!TryGetBlockShape(neighborId, getState?.Invoke(neighborPos.x, neighborPos.y, neighborPos.z), out BlockShape neighborShape))
            return true;

        return !IsFaceCovered(shape, neighborShape, dir);
    }

    private static bool IsFaceCovered(BlockShape self, BlockShape other, Vector3Int dir)
    {
        const float eps = 0.0001f;

        if (dir == Vector3Int.right)
            return Mathf.Abs(self.max.x - (other.min.x + 1f)) < eps &&
                   other.min.y <= self.min.y + eps && other.max.y >= self.max.y - eps &&
                   other.min.z <= self.min.z + eps && other.max.z >= self.max.z - eps;
        if (dir == Vector3Int.left)
            return Mathf.Abs(self.min.x - (other.max.x - 1f)) < eps &&
                   other.min.y <= self.min.y + eps && other.max.y >= self.max.y - eps &&
                   other.min.z <= self.min.z + eps && other.max.z >= self.max.z - eps;
        if (dir == Vector3Int.up)
            return Mathf.Abs(self.max.y - (other.min.y + 1f)) < eps &&
                   other.min.x <= self.min.x + eps && other.max.x >= self.max.x - eps &&
                   other.min.z <= self.min.z + eps && other.max.z >= self.max.z - eps;
        if (dir == Vector3Int.down)
            return Mathf.Abs(self.min.y - (other.max.y - 1f)) < eps &&
                   other.min.x <= self.min.x + eps && other.max.x >= self.max.x - eps &&
                   other.min.z <= self.min.z + eps && other.max.z >= self.max.z - eps;
        if (dir == Vector3Int.forward)
            return Mathf.Abs(self.max.z - (other.min.z + 1f)) < eps &&
                   other.min.x <= self.min.x + eps && other.max.x >= self.max.x - eps &&
                   other.min.y <= self.min.y + eps && other.max.y >= self.max.y - eps;

        return Mathf.Abs(self.min.z - (other.max.z - 1f)) < eps &&
               other.min.x <= self.min.x + eps && other.max.x >= self.max.x - eps &&
               other.min.y <= self.min.y + eps && other.max.y >= self.max.y - eps;
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

        for (int i = 0; i < 4; i++)
        {
            mesh.normals.Add(dir);
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
        renderMesh.SetNormals(md.normals);
        colliderMesh.RecalculateTangents();
        colliderMesh.RecalculateBounds();

        return new ChunkRendering.ChunkMeshData
        {
            renderingMesh = renderMesh,
            colliderMesh = colliderMesh
        };
    }
}


