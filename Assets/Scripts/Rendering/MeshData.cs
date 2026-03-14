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
    private const float SHAPE_EPSILON = 0.0001f;
    private const float SLAB_HEIGHT = 0.5f;
    
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
        ShapeCacheCell[,,] shapeCache = BuildShapeCache(getBlock, getState, lodScale);
        bool[] borderGreedyOccluderCache = BuildBorderGreedyOccluderCache();

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
            GreedyDirection(getBlock, getState, dir, mesh, mask, lodScale, neighbors, shapeCache, borderGreedyOccluderCache);
        }
        
        AddCustomBlockMeshes(getBlock, getState, mesh, lodScale, shapeCache);
        return mesh;
    }
    
    // Greedy meshing path intentionally handles only full cube blocks.
    private static void GreedyDirection(Func<int,int,int,byte> getBlock,
            Func<int, int, int, BlockStateContainer> getState,
            Vector3Int dir, MeshData mesh, MaskCell[,] mask, int lodScale, NeighborLODInfo neighbors, ShapeCacheCell[,,] shapeCache,
            bool[] borderGreedyOccluder)
    {
        int neighborScale =
            dir == Vector3Int.right   ? neighbors.posX :
            dir == Vector3Int.left    ? neighbors.negX :
            dir == Vector3Int.up      ? neighbors.posY :
            dir == Vector3Int.down    ? neighbors.negY :
            dir == Vector3Int.forward ? neighbors.posZ :
            neighbors.negZ;

        int mSize = CHUNK_SIZE / lodScale;

        for (int w = 0; w < CHUNK_SIZE; w += lodScale)
        {
            bool isBorderSlice =
                (dir == Vector3Int.left && w == 0) ||
                (dir == Vector3Int.right && w == CHUNK_SIZE - lodScale) ||
                (dir == Vector3Int.down && w == 0) ||
                (dir == Vector3Int.up && w == CHUNK_SIZE - lodScale) ||
                (dir == Vector3Int.back && w == 0) ||
                (dir == Vector3Int.forward && w == CHUNK_SIZE - lodScale);
            
            // build mask for this slice
            for (int u = 0; u < CHUNK_SIZE; u += lodScale)
            {
                int mu = u / lodScale;
                
                for (int v = 0; v < CHUNK_SIZE; v += lodScale)
                {
                    int mv = v / lodScale;
                    
                    mask[mu, mv].occluded = false;
                    mask[mu, mv].atlasIndex = -1;
                    
                    int x, y, z;
                    if (dir.x != 0) { x = w; y = u; z = v; }
                    else if (dir.y != 0) { x = u; y = w; z = v; }
                    else { x = u; y = v; z = w; }

                    byte current = SampleBlock(getBlock, x, y, z, lodScale);
                    if (current == 0 || !IsGreedyFullCube(shapeCache, x, y, z))
                        continue;

                    int nx = x + dir.x * lodScale;
                    int ny = y + dir.y * lodScale;
                    int nz = z + dir.z * lodScale;

                    bool forceFace = isBorderSlice && neighborScale < lodScale;

                    bool neighborBlocksFace;
                    if (IsInsideChunk(nx, ny, nz))
                    {
                        neighborBlocksFace = IsGreedyFullCube(shapeCache, nx, ny, nz);
                    }
                    else
                    {
                        // Border neighbor fast path:
                        // only known always-full-cube blocks are allowed to occlude greedy faces.
                        // Blocks with dynamic shape state (slabs/scaffolding/etc.) are treated as non-occluding 
                        byte neighbor = getBlock(nx, ny, nz);
                        neighborBlocksFace = IsFastBorderGreedyOccluder(neighbor, borderGreedyOccluder);
                    }

                    if (neighborBlocksFace && !forceFace)
                        continue;

                    int atlasIdx = -1;
                    var tbi = BlockRegistry.ThreadBlockInfo;
                    BlockStateContainer currentState = getState?.Invoke(x, y, z);

                    if (tbi != null && current >= 0 && current < tbi.Length)
                    {
                        // Use the thread-safe BlockInfo table if available, fallback safe handling
                        var block = tbi[current];
                        if (block != null)
                        {
                            atlasIdx = GetFaceAtlasIndex(block, currentState, dir);
                        }
                    }
                    else
                    {
                        var block = BlockRegistry.GetBlock((int)current);
                        if (block != null)
                        {
                            atlasIdx = GetFaceAtlasIndex(block, currentState, dir);

                        }
                    }

                    if (atlasIdx >= 0)
                    {
                        mask[mu, mv].occluded = true;
                        mask[mu, mv].atlasIndex = atlasIdx;
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

    private static ShapeCacheCell[,,] BuildShapeCache(
        Func<int, int, int, byte> getBlock,
        Func<int, int, int, BlockStateContainer> getState, int lodScale)
    {
        ShapeCacheCell[,,] cache = new ShapeCacheCell[CHUNK_SIZE, CHUNK_SIZE, CHUNK_SIZE];

        for (int x = 0; x < CHUNK_SIZE; x++)
        for (int y = 0; y < CHUNK_SIZE; y++)
        for (int z = 0; z < CHUNK_SIZE; z++)
        {
            byte blockId = getBlock(x, y, z);
            if (blockId == 0)
                continue;

            BlockStateContainer state = getState?.Invoke(x, y, z);
            if (!TryGetBlockShape(blockId, state, out BlockShape shape))
                continue;

            bool isDirectional = state?.GetState("facing") != null;
            bool fullCube = IsFullShape(shape) && !isDirectional;

            if (lodScale > 1)
            {
                // Higher LODs intentionally collapse custom shapes (slabs, directional variants, etc.)
                // into regular cube voxels so they still participate in coarse greedy meshing.
                fullCube = true;
            }
            
            cache[x, y, z].hasShape = true;
            cache[x, y, z].shape = shape;
            cache[x, y, z].greedyFullCube = fullCube;
        }

        return cache;
    }
    
    private static bool IsInsideChunk(int x, int y, int z)
    {
        return x >= 0 && y >= 0 && z >= 0 &&
               x < CHUNK_SIZE && y < CHUNK_SIZE && z < CHUNK_SIZE;
    }

    private static bool IsGreedyFullCube(ShapeCacheCell[,,] shapeCache, int x, int y, int z)
    {
        return IsInsideChunk(x, y, z) && shapeCache[x, y, z].greedyFullCube;
    }

    
    private static bool[] BuildBorderGreedyOccluderCache()
    {
        var tbi = BlockRegistry.ThreadBlockInfo;
        if (tbi == null || tbi.Length == 0)
            return Array.Empty<bool>();

        bool[] cache = new bool[tbi.Length];
        for (int i = 1; i < tbi.Length; i++)
        {
            Block block = tbi[i];
            if (block == null)
                continue;

            // Conservative classification: if block type is known to have dynamic shape states,
            // do not allow it to occlude in fast border mode.
            if (block is SlabBlock || block is ScaffoldingBlock)
                continue;

            string height = block.GetState(BlockStateKeys.HeightState);
            if (!string.IsNullOrEmpty(height) && height != "1")
                continue;

            string facing = block.GetState(BlockStateKeys.DirectionalFacing);
            if (!string.IsNullOrEmpty(facing) && facing != "up")
                continue;

            cache[i] = true;
        }

        return cache;
    }

    private static bool IsFastBorderGreedyOccluder(byte blockId, bool[] borderGreedyOccluderCache)
    {
        return blockId > 0 && blockId < borderGreedyOccluderCache.Length && borderGreedyOccluderCache[blockId];
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
    
    private struct ShapeCacheCell
    {
        public bool hasShape;
        public bool greedyFullCube;
        public BlockShape shape;
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
        return Mathf.Abs(shape.min.x) < SHAPE_EPSILON && Mathf.Abs(shape.min.y) < SHAPE_EPSILON && Mathf.Abs(shape.min.z) < SHAPE_EPSILON &&
               Mathf.Abs(shape.max.x - 1f) < SHAPE_EPSILON && Mathf.Abs(shape.max.y - 1f) < SHAPE_EPSILON && Mathf.Abs(shape.max.z - 1f) < SHAPE_EPSILON;
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
        {
            if (h == "1")
                height = 1f;
            else if (h == "0.5")
                height = SLAB_HEIGHT;
            else
                float.TryParse(h, NumberStyles.Float, CultureInfo.InvariantCulture, out height);
        }


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
    
    private static void AddCustomBlockMeshes(Func<int,int,int,byte> getBlock,
        Func<int, int, int, BlockStateContainer> getState,
        MeshData mesh,
        int lodScale, ShapeCacheCell[,,] shapeCache)
    {
        if (lodScale != 1)
            return;
        
        for (int x = 0; x < CHUNK_SIZE; x++)
        for (int y = 0; y < CHUNK_SIZE; y++)
        for (int z = 0; z < CHUNK_SIZE; z++)
        {
            byte blockId = getBlock(x, y, z);
            if (blockId == 0)
                continue;

            ShapeCacheCell cell = shapeCache[x, y, z];
            if (!cell.hasShape || cell.greedyFullCube)
                continue;

            var block = BlockRegistry.ThreadBlockInfo[blockId];
            if (block == null)
                continue;

            BlockShape shape = cell.shape;
            
            Vector3Int pos = new Vector3Int(x, y, z);
            BlockStateContainer state = getState?.Invoke(x, y, z);

            AddShapeFace(getBlock, getState, mesh, shapeCache, pos, shape, Vector3Int.up, GetFaceAtlasIndex(block, state, Vector3Int.up));
            AddShapeFace(getBlock, getState, mesh, shapeCache, pos, shape, Vector3Int.down, GetFaceAtlasIndex(block, state, Vector3Int.down));
            AddShapeFace(getBlock, getState, mesh, shapeCache, pos, shape, Vector3Int.right, GetFaceAtlasIndex(block, state, Vector3Int.right));
            AddShapeFace(getBlock, getState, mesh, shapeCache, pos, shape, Vector3Int.left, GetFaceAtlasIndex(block, state, Vector3Int.left));
            AddShapeFace(getBlock, getState, mesh, shapeCache, pos, shape, Vector3Int.forward, GetFaceAtlasIndex(block, state, Vector3Int.forward));
            AddShapeFace(getBlock, getState, mesh, shapeCache, pos, shape, Vector3Int.back, GetFaceAtlasIndex(block, state, Vector3Int.back));

        }
    }
    
    private static int GetFaceAtlasIndex(Block block, BlockStateContainer state, Vector3Int dir)
    {
        if (block == null)
            return -1;

        if (dir == Vector3Int.up)
            return block.topIndex;

        if (dir == Vector3Int.down)
            return block.bottomIndex;

        if (block.frontIndex >= 0 && IsFrontFaceDirection(dir, state, block))
            return block.frontIndex;

        return block.sideIndex;
    }

    private static bool IsFrontFaceDirection(Vector3Int dir, BlockStateContainer state, Block block)
    {
        string facing = state?.GetState("facing");
        if (string.IsNullOrEmpty(facing))
            facing = GetStateValueOrDefault(block, state, BlockStateKeys.DirectionalFacing);

        if (string.IsNullOrEmpty(facing))
            facing = "north";

        Vector3Int frontDir = facing switch
        {
            "north" => Vector3Int.forward,
            "south" => Vector3Int.back,
            "east" => Vector3Int.right,
            "west" => Vector3Int.left,
            _ => Vector3Int.forward,
        };

        return dir == frontDir;
    }


    private static void AddShapeFace(Func<int,int,int,byte> getBlock,
        Func<int, int, int, BlockStateContainer> getState,
        MeshData mesh,
        ShapeCacheCell[,,] shapeCache,
        Vector3Int pos,
        BlockShape shape,
        Vector3Int dir,
        int atlasIndex)
    {
        if (atlasIndex < 0 || !ShouldRenderShapeFace(getBlock, getState, shapeCache, pos, shape, dir))
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
        ShapeCacheCell[,,] shapeCache,
        Vector3Int pos,
        BlockShape shape,
        Vector3Int dir)
    {
        Vector3Int neighborPos = pos + dir;
        byte neighborId = getBlock(neighborPos.x, neighborPos.y, neighborPos.z);
        if (neighborId == 0)
            return true;

        BlockShape neighborShape;
        if (IsInsideChunk(neighborPos.x, neighborPos.y, neighborPos.z))
        {
            ShapeCacheCell neighborCell = shapeCache[neighborPos.x, neighborPos.y, neighborPos.z];
            if (!neighborCell.hasShape)
                return true;

            neighborShape = neighborCell.shape;
        }
        else
        {
            // Cross-chunk lookup fallback for border faces.
            if (!TryGetBlockShape(neighborId, getState?.Invoke(neighborPos.x, neighborPos.y, neighborPos.z), out neighborShape))
                return true;
        }

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
        renderMesh.SetNormals(md.normals);
        //renderMesh.RecalculateTangents();
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


