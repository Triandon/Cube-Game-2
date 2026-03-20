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
    private const float MIN_SHAPE_HEIGHT = 0.1f;
    
    private struct MaskCell
    {
        public bool occluded;
        public int atlasIndex;
    }
    
    private struct BlockShape
    {
        public Vector3 min;
        public Vector3 max;
    }
    
    private struct ShapeCacheCell
    {
        public bool hasBlock;
        public BlockShape shape;
    }
    
    public struct NeighborLODInfo
    {
        public int posX, negX;
        public int posY, negY;
        public int posZ, negZ;
    }
    
    public static MeshData GenerateMeshData(Func<int, int, int, byte> getBlock,
        Func<int, int, int, BlockStateContainer> getState,
        int lodScale,
        NeighborLODInfo neighbors)
    {
        var mesh = new MeshData();

        if (lodScale <= 1)
        {
            ShapeCacheCell[,,] shapeCache = BuildShapeCache(getBlock, getState);
            AddDetailedBlockMeshes(getBlock, getState, mesh, shapeCache);
            return mesh;
        }

        MaskCell[,] mask = new MaskCell[CHUNK_SIZE, CHUNK_SIZE];
        Vector3Int[] dirs = new Vector3Int[]
        {
            Vector3Int.right, Vector3Int.left,
            Vector3Int.up, Vector3Int.down,
            Vector3Int.forward, Vector3Int.back
        };

        foreach (var dir in dirs)
        {
            GreedyDirectionFastBROOM(getBlock, getState, dir, mesh, mask, lodScale, neighbors);
        }

        return mesh;
    }
    
    // Fast greedy path for coarse LODs. This intentionally stays close to the old implementation
    // so distant chunks keep the cheap cube-only meshing behavior.
    private static void GreedyDirectionFastBROOM(Func<int, int, int, byte> getBlock,
        Func<int, int, int, BlockStateContainer> getState,
        Vector3Int dir,
        MeshData mesh,
        MaskCell[,] mask,
        int lodScale,
        NeighborLODInfo neighbors)
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
                    if (current == 0)
                        continue;
                    
                    int nx = x + dir.x * lodScale;
                    int ny = y + dir.y * lodScale;
                    int nz = z + dir.z * lodScale;

                    byte neighbor = SampleBlock(getBlock, nx, ny, nz,lodScale);

                    bool forceFace = isBorderSlice && neighborScale < lodScale;
                    if (neighbor != 0 && !forceFace)
                        continue;

                    int atlasIdx = GetGreedyAtlasIndex(current, dir, getState, x, y, z);
                    if (atlasIdx < 0)
                        continue;

                    mask[mu, mv].occluded = true;
                    mask[mu, mv].atlasIndex = atlasIdx;

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
                    {
                        width++;
                    }

                    // extend height (u direction)
                    int height = 1;
                    bool done = false;
                    while (u + height < mSize && !done)
                    {
                        for (int k = 0; k < width; k++)
                        {
                            if (!mask[u + height, v + k].occluded ||
                                mask[u + height, v + k].atlasIndex != atlasIndex)

                            {
                                done = true;
                                break;
                            }
                        }

                        if (!done) 
                            height++;
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
        Func<int, int, int, BlockStateContainer> getState)
    {
        ShapeCacheCell[,,] cache = new ShapeCacheCell[CHUNK_SIZE, CHUNK_SIZE, CHUNK_SIZE];

        for (int x = 0; x < CHUNK_SIZE; x++)
        for (int y = 0; y < CHUNK_SIZE; y++)
        for (int z = 0; z < CHUNK_SIZE; z++)
        {
            byte blockId = getBlock(x, y, z);
            if (blockId == 0)
                continue;

            cache[x, y, z].hasBlock = true;
            cache[x, y, z].shape = GetBlockShape(blockId, getState?.Invoke(x, y, z));
        }

        return cache;
    }
    
    private static void AddDetailedBlockMeshes(
        Func<int, int, int, byte> getBlock,
        Func<int, int, int, BlockStateContainer> getState,
        MeshData mesh,
        ShapeCacheCell[,,] shapeCache)
    {
        Vector3Int[] dirs =
        {
            Vector3Int.up, Vector3Int.down,
            Vector3Int.right, Vector3Int.left,
            Vector3Int.forward, Vector3Int.back
        };

        for (int x = 0; x < CHUNK_SIZE; x++)
        for (int y = 0; y < CHUNK_SIZE; y++)
        for (int z = 0; z < CHUNK_SIZE; z++)
        {
            byte blockId = getBlock(x, y, z);
            if (blockId == 0)
                continue;

            Block block = GetBlockInfo(blockId);
            if (block == null)
                continue;

            BlockStateContainer state = getState?.Invoke(x, y, z);
            BlockShape shape = shapeCache[x, y, z].shape;
            Vector3Int pos = new Vector3Int(x, y, z);

            foreach (Vector3Int dir in dirs)
            {
                int atlasIndex = GetDetailedAtlasIndex(block, state, dir);
                if (atlasIndex < 0)
                    continue;

                if (!ShouldRenderDetailedFace(getBlock, getState, shapeCache, pos, shape, dir))
                    continue;

                AddShapeFace(pos, shape, dir, atlasIndex, mesh);
            }
        }
    }

    private static bool ShouldRenderDetailedFace(
        Func<int, int, int, byte> getBlock,
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
            if (!neighborCell.hasBlock)
                return true;

            neighborShape = neighborCell.shape;
        }
        else
        {
            neighborShape = GetBlockShape(neighborId, getState?.Invoke(neighborPos.x, neighborPos.y, neighborPos.z));
        }

        return !IsFaceCovered(shape, neighborShape, dir);
    }

    private static bool IsFaceCovered(BlockShape self, BlockShape other, Vector3Int dir)
    {
        if (dir == Vector3Int.right)
        {
            return Mathf.Abs(self.max.x - (other.min.x + 1f)) < SHAPE_EPSILON &&
                   other.min.y <= self.min.y + SHAPE_EPSILON && other.max.y >= self.max.y - SHAPE_EPSILON &&
                   other.min.z <= self.min.z + SHAPE_EPSILON && other.max.z >= self.max.z - SHAPE_EPSILON;
        }

        if (dir == Vector3Int.left)
        {
            return Mathf.Abs(self.min.x - (other.max.x - 1f)) < SHAPE_EPSILON &&
                   other.min.y <= self.min.y + SHAPE_EPSILON && other.max.y >= self.max.y - SHAPE_EPSILON &&
                   other.min.z <= self.min.z + SHAPE_EPSILON && other.max.z >= self.max.z - SHAPE_EPSILON;
        }

        if (dir == Vector3Int.up)
        {
            return Mathf.Abs(self.max.y - (other.min.y + 1f)) < SHAPE_EPSILON &&
                   other.min.x <= self.min.x + SHAPE_EPSILON && other.max.x >= self.max.x - SHAPE_EPSILON &&
                   other.min.z <= self.min.z + SHAPE_EPSILON && other.max.z >= self.max.z - SHAPE_EPSILON;
        }

        if (dir == Vector3Int.down)
        {
            return Mathf.Abs(self.min.y - (other.max.y - 1f)) < SHAPE_EPSILON &&
                   other.min.x <= self.min.x + SHAPE_EPSILON && other.max.x >= self.max.x - SHAPE_EPSILON &&
                   other.min.z <= self.min.z + SHAPE_EPSILON && other.max.z >= self.max.z - SHAPE_EPSILON;
        }

        if (dir == Vector3Int.forward)
        {
            return Mathf.Abs(self.max.z - (other.min.z + 1f)) < SHAPE_EPSILON &&
                   other.min.x <= self.min.x + SHAPE_EPSILON && other.max.x >= self.max.x - SHAPE_EPSILON &&
                   other.min.y <= self.min.y + SHAPE_EPSILON && other.max.y >= self.max.y - SHAPE_EPSILON;
        }

        return Mathf.Abs(self.min.z - (other.max.z - 1f)) < SHAPE_EPSILON &&
               other.min.x <= self.min.x + SHAPE_EPSILON && other.max.x >= self.max.x - SHAPE_EPSILON &&
               other.min.y <= self.min.y + SHAPE_EPSILON && other.max.y >= self.max.y - SHAPE_EPSILON;
    }
    
    private static BlockShape GetBlockShape(byte blockId, BlockStateContainer state)
    {
        BlockShape shape = new BlockShape
        {
            min = Vector3.zero,
            max = Vector3.one
        };

        Block block = GetBlockInfo(blockId);
        float height = GetHeightValue(block, state);
        if (height >= 1f - SHAPE_EPSILON)
            return shape;

        string facing = GetFacingValue(block, state);
        if (string.IsNullOrEmpty(facing) || facing == "up")
        {
            shape.max.y = height;
            return shape;
        }

        switch (facing)
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
            default:
                shape.max.y = height;
                break;
        }

        return shape;
    }
    

    private static float GetHeightValue(Block block,BlockStateContainer state)
    {
        string heightValue = GetStateValueOrDefault(block, state, BlockStateKeys.HeightState);
        if (string.IsNullOrEmpty(heightValue))
            return 1f;

        if (!float.TryParse(heightValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float height))
            return 1f;

        return Mathf.Clamp(height, MIN_SHAPE_HEIGHT, 1f);
    }

    private static string GetFacingValue(Block block, BlockStateContainer state)
    {
        string facing = state?.GetState("facing");
        if (!string.IsNullOrEmpty(facing))
            return facing;
        
        facing = GetStateValueOrDefault(block, state, BlockStateKeys.DirectionalFacing);
        if (!string.IsNullOrEmpty(facing))
            return facing;

        return null;
    }

    private static bool IsInsideChunk(int x, int y, int z)
    {
        return x >= 0 && y >= 0 && z >= 0 &&
               x < CHUNK_SIZE && y < CHUNK_SIZE && z < CHUNK_SIZE;
    }

    private static Block GetBlockInfo(byte blockId)
    {
        Block[] tbi = BlockRegistry.ThreadBlockInfo;
        if (tbi != null && blockId >= 0 && blockId < tbi.Length && tbi[blockId] != null)
            return tbi[blockId];

        return BlockRegistry.GetBlock(blockId);
    }

    private static int GetGreedyAtlasIndex(
        byte blockId,
        Vector3Int dir,
        Func<int, int, int, BlockStateContainer> getState,
        int x,
        int y,
        int z)
    {
        Block block = GetBlockInfo(blockId);
        if (block == null)
            return -1;

        if (dir == Vector3Int.up)
            return block.topIndex;

        if (dir == Vector3Int.down)
            return block.bottomIndex;

        if (block.frontIndex >= 0)
        {
            BlockStateContainer state = getState?.Invoke(x, y, z);
            if (IsFrontFaceDirection(dir, state, block))
                return block.frontIndex;
        }

        return block.sideIndex;
    }

    private static int GetDetailedAtlasIndex(Block block, BlockStateContainer state, Vector3Int dir)
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
        string facing = GetFacingValue(block, state);
        if (string.IsNullOrEmpty(facing))
            return false;

        return (dir == Vector3Int.forward && facing == "north") ||
               (dir == Vector3Int.back && facing == "south") ||
               (dir == Vector3Int.right && facing == "east") ||
               (dir == Vector3Int.left && facing == "west");
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

    
    private static string GetStateValueOrDefault(Block block, BlockStateContainer state, string stateKey)
    {
        string value = state?.GetState(stateKey);
        if (!string.IsNullOrEmpty(value))
            return value;

        return block?.GetState(stateKey);
    }
    
    private static void AddShapeFace(Vector3Int blockPos, BlockShape shape, Vector3Int dir, int atlasIndex, MeshData mesh)
    {
        Vector3[] faceVerts = VoxelData.GetFaceVertices(dir);
        if (faceVerts == null || faceVerts.Length != 4)
            return;

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

            mesh.vertices.Add(blockPos + local);
            mesh.normals.Add(dir);
        }

        AddFaceTriangles(mesh, baseIndex, dir);

        int width = Mathf.RoundToInt(Mathf.Max(1f, dir.y != 0 ? scale.x : dir.x != 0 ? scale.z : scale.x));
        int height = Mathf.RoundToInt(Mathf.Max(1f, dir.y != 0 ? scale.z : scale.y));
        AddFaceUV(dir, atlasIndex, width, height, mesh);
        AddColliderFace(mesh, baseIndex, dir);
    }

    private static void AddQuadFromMask(int u, int v, int width, int height, int w, Vector3Int dir, int atlasIndex,
        MeshData mesh, int lodScale)
    {
        if (width <= 0 || height <= 0) return;

        Vector3[] faceVerts = VoxelData.GetFaceVertices(dir);
        if (faceVerts == null || faceVerts.Length != 4) return;

        int bu = u * lodScale;
        int bv = v * lodScale;
        int bw = w;

        int blockWidth = width * lodScale;
        int blockHeight = height * lodScale;

        int faceOffset = dir.x + dir.y + dir.z > 0 ? lodScale : 0;

        Vector3 offset = dir.x != 0
            ? new Vector3(bw + faceOffset, bu, bv)
            : dir.y != 0
                ? new Vector3(bu, bw + faceOffset, bv)
                : new Vector3(bu, bv, bw + faceOffset);

        int baseIndex = mesh.vertices.Count;

        // Build the 4 world positions for this quad. Match original position math
        foreach (Vector3 vert in faceVerts)
        {
            Vector3 pos = dir.x != 0
                ? offset + vert.y * Vector3.up * blockHeight + vert.z * Vector3.forward * blockWidth
                : dir.y != 0
                    ? offset + vert.x * Vector3.right * blockHeight + vert.z * Vector3.forward * blockWidth
                    : offset + vert.x * Vector3.right * blockHeight + vert.y * Vector3.up * blockWidth;

            mesh.vertices.Add(pos);
            mesh.normals.Add(dir);
        }

        AddFaceTriangles(mesh, baseIndex, dir);
        AddFaceUV(dir, atlasIndex, width, height, mesh);
        AddColliderFace(mesh, baseIndex, dir);
    }

    private static void AddFaceTriangles(MeshData mesh, int baseIndex, Vector3Int dir)
    {
        // Triangles (winding check)
        int i0 = baseIndex + 0;
        int i1 = baseIndex + 1;
        int i2 = baseIndex + 2;
        int i3 = baseIndex + 3;

        int t0a = i0;
        int t0b = i2;
        int t0c = i1;
        int t1a = i2;
        int t1b = i3;
        int t1c = i1;


        Vector3 a = mesh.vertices[t0b] - mesh.vertices[t0a];
        Vector3 b = mesh.vertices[t0c] - mesh.vertices[t0a];
        Vector3 triNormal = Vector3.Cross(a, b);

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
    }
    
    private static void AddColliderFace(MeshData mesh, int baseIndex, Vector3Int dir){
        
        // UVs and UV meta
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

        int t0a = c0;
        int t0b = c2;
        int t0c = c1;
        int t1a = c2;
        int t1b = c3;
        int t1c = c1;

        Vector3 a = mesh.colliderVertices[t0b] - mesh.colliderVertices[t0a];
        Vector3 b = mesh.colliderVertices[t0c] - mesh.colliderVertices[t0a];
        Vector3 triNormal = Vector3.Cross(a, b);

        if (Vector3.Dot(triNormal, (Vector3)dir) < 0f)
        {
            t0b = c1;
            t0c = c2;
            t1b = c1;
            t1c = c3;
        }

        const float areaEpsilon = 1e-6f;
        Vector3 areaA = Vector3.Cross(mesh.colliderVertices[t0b] - mesh.colliderVertices[t0a],
            mesh.colliderVertices[t0c] - mesh.colliderVertices[t0a]);
        if (areaA.sqrMagnitude * 0.25f > areaEpsilon)
        {
            mesh.colliderTriangles.Add(t0a);
            mesh.colliderTriangles.Add(t0b);
            mesh.colliderTriangles.Add(t0c);
        }
        
        Vector3 areaB = Vector3.Cross(mesh.colliderVertices[t1b] - mesh.colliderVertices[t1a],
            mesh.colliderVertices[t1c] - mesh.colliderVertices[t1a]);
        if (areaB.sqrMagnitude * 0.25f > areaEpsilon)
        {
            mesh.colliderTriangles.Add(t1a);
            mesh.colliderTriangles.Add(t1b);
            mesh.colliderTriangles.Add(t1c);
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
            negX = owner.chunkManager.GetChunk(owner.coord + Vector3Int.left)?.GetLodScale() ?? lodScale,
            posY = owner.chunkManager.GetChunk(owner.coord + Vector3Int.up)?.GetLodScale() ?? lodScale,
            negY = owner.chunkManager.GetChunk(owner.coord + Vector3Int.down)?.GetLodScale() ?? lodScale,
            posZ = owner.chunkManager.GetChunk(owner.coord + Vector3Int.forward)?.GetLodScale() ?? lodScale,
            negZ = owner.chunkManager.GetChunk(owner.coord + Vector3Int.back)?.GetLodScale() ?? lodScale,
        };

        // Use threaded mesher to produce plain MeshData (runs on main thread here)
        Func<int, int, int, byte> getBlock = (x, y, z) => owner != null ? owner.GetBlock(x, y, z) : (byte)0;
        Func<int, int, int, BlockStateContainer> getState = (x, y, z) => owner != null ? owner.GetStateAt(x, y, z) : null;

        // Convert MeshData to Unity Mesh objects (this MUST be done on main thread)
        MeshData meshData = ChunkMeshGeneratorThreaded.GenerateMeshData(getBlock, getState, lodScale, neighbors);
        
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
        //renderMesh.SetNormals(md.normals);
        colliderMesh.RecalculateTangents();
        colliderMesh.RecalculateBounds();

        return new ChunkRendering.ChunkMeshData
        {
            renderingMesh = renderMesh,
            colliderMesh = colliderMesh
        };
    }
}


