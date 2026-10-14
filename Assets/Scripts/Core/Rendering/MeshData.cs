using System;
using System.Collections.Generic;
using Core;
using Core.Block;
using UnityEngine;

//Todo In the lods, there CAN be a height bias for long distance chunks!

public class MeshData
{
    private readonly List<MeshUtilityCustom.ChunkVertex> vertices = new List<MeshUtilityCustom.ChunkVertex>();
    public readonly List<int> triangles = new List<int>();
    private Vector3 minPosition;
    private Vector3 maxPosition;

    public int VertexCount => vertices.Count;
    public Vector3 GetPosition(int index) => vertices[index].Position;

    public void AddVertex(Vector3 position, uint normal, uint uv, ushort atlasTile, byte light)
    {
        var vertex = new MeshUtilityCustom.ChunkVertex(position, normal, uv, atlasTile, light);
        // Use the quantized position for both culling bounds and geometry calculations.
        Vector3 decodedPosition = vertex.Position;
        if (vertices.Count == 0)
        {
            minPosition = decodedPosition;
            maxPosition = decodedPosition;
        }
        else
        {
            minPosition = Vector3.Min(minPosition, decodedPosition);
            maxPosition = Vector3.Max(maxPosition, decodedPosition);
        }
        vertices.Add(vertex);
    }

    public void SetFaceNormal(int firstVertex, int vertexCount, Vector3 normal)
    {
        uint packedNormal = MeshUtilityCustom.ChunkVertex.PackNormal(normal);
        for (int i = firstVertex; i < firstVertex + vertexCount; i++)
        {
            var vertex = vertices[i];
            vertex.normal = packedNormal;
            vertices[i] = vertex;
        }
    }

    public Bounds Bounds
    {
        get
        {
            if (vertices.Count == 0)
                return new Bounds(Vector3.zero, Vector3.zero);
            Bounds bounds = new Bounds();
            bounds.SetMinMax(minPosition, maxPosition);
            return bounds;
        }
    }
    
    public MeshUtilityCustom.ChunkVertex[] CopyVertices() => vertices.ToArray();
}

public static class ChunkMeshGeneratorThreaded
{
    private const int CHUNK_SIZE = Chunk.CHUNK_SIZE;
    private const int ATLAS_TILES = 16;
    private const float FullBlockEpsilon = 0.0001f;
    
    private struct MaskCell
    {
        public bool occluded;
        public int atlasIndex;
        public byte light;
    }
    
    public struct NeighborLODInfo
    {
        public int posX, negX;
        public int posY, negY;
        public int posZ, negZ;
    }
    
    public static MeshData GenerateMeshData(Func<int,int,int,byte> getBlock,
        Func<int, int, int, BlockStateContainer> getState, int lodScale, NeighborLODInfo neighbors,
        IReadOnlyCollection<Vector3Int> specialMeshBlocks = null,
        Func<int, int, int, byte> getSkyLight = null, Func<int, int, int, byte> getBlockLight = null)
    {
        var mesh = new MeshData();
        bool hasSpecialMeshBlocks = lodScale == 1 && specialMeshBlocks != null && specialMeshBlocks.Count > 0;
        bool assumeOpaqueCubeOnly = lodScale == 1 && !hasSpecialMeshBlocks;

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
            if (lodScale == 1)
            {
                Direction(getBlock, getState, getSkyLight, getBlockLight, dir, mesh, assumeOpaqueCubeOnly);
            }
            else
            {
                GreedyDirection(getBlock, getState, getSkyLight, getBlockLight, dir, mesh,mask, lodScale, neighbors, assumeOpaqueCubeOnly);
            }
        }

        if (hasSpecialMeshBlocks)
        {
            AppendStateDrivenMeshes(getBlock, getState, getSkyLight, getBlockLight, mesh, specialMeshBlocks);
        }
        return mesh;
    }
    
    // LOD0 keeps full block-face detail: visible cube faces are emitted one block at a time
    // instead of being combined by the greedy merger used for higher LODs.
    private static void Direction(Func<int,int,int,byte> getBlock, Func<int, int, int, BlockStateContainer> getState,
        Func<int, int, int, byte> getSkyLight, Func<int, int, int, byte> getBlockLight,
        Vector3Int dir, MeshData mesh, bool assumeOpaqueCubeOnly)
    {
        for (int w = 0; w < CHUNK_SIZE; w++)
        {
            for (int u = 0; u < CHUNK_SIZE; u++)
            {
                for (int v = 0; v < CHUNK_SIZE; v++)
                {
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
                    else
                    {
                        x = u;
                        y = v;
                        z = w;
                    }

                    byte current = getBlock(x, y, z);
                    byte neighbor = getBlock(x + dir.x, y + dir.y, z + dir.z);
                    Block currentBlock = null;
                    bool renderFace;

                    if (assumeOpaqueCubeOnly)
                    {
                        if (current == 0)
                            continue;

                        renderFace = neighbor == 0;
                    }
                    else
                    {
                        if (!ShouldGreedyMeshBlock(current, 1, out currentBlock))
                            continue;

                        renderFace = ShouldRenderGreedyFace(neighbor, false, 1);
                    }

                    if (!renderFace)
                        continue;

                    currentBlock ??= GetBlockInfo(current);
                    if (currentBlock == null)
                        continue;

                    BlockStateContainer currentState = null;
                    if (ShouldReadFacingState(currentBlock, dir))
                    {
                        currentState = getState?.Invoke(x, y, z);
                    }

                    int atlasIdx = GetAtlasIndex(currentBlock, currentState, dir);
                    if (atlasIdx >= 0)
                    {
                        byte light = SampleFaceLight(getSkyLight, getBlockLight, x + dir.x, y + dir.y, z + dir.z, x, y, z);
                        AddQuadFromMask(u, v, 1, 1, w, dir, atlasIdx, mesh, 1, light);
                    }
                }
            }
        }
    }

    
    // Greedy direction implementation adapted to be fully data-only and match original behavior
    private static void GreedyDirection(Func<int,int,int,byte> getBlock, Func<int, int, int, BlockStateContainer> getState,
        Func<int, int, int, byte> getSkyLight, Func<int, int, int, byte> getBlockLight, 
        Vector3Int dir, MeshData mesh,MaskCell[,] mask, int lodScale, NeighborLODInfo neighbors, bool assumeOpaqueCubeOnly)
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
                    mask[mu, mv].light = 0;

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
                    byte neighbor = SampleLodFaceBlock(getBlock, x, y, z, dir, lodScale);
                    
                    bool forceFace = isBorderSlice && neighborScale < lodScale;
                    Block currentBlock = null;
                    bool renderFace;
                    if (assumeOpaqueCubeOnly)
                    {
                        if (current == 0)
                            continue;

                        renderFace = forceFace || neighbor == 0;
                    }
                    else
                    {
                        if (!ShouldGreedyMeshBlock(current, lodScale, out currentBlock))
                            continue;

                        renderFace = ShouldRenderGreedyFace(neighbor, forceFace, lodScale);
                    }

                    if (!renderFace)
                        continue;

                    if (current != 0)
                    {
                        currentBlock ??= GetBlockInfo(current);
                        if (currentBlock == null)
                            continue;

                        BlockStateContainer currentState = null;
                        if (ShouldReadFacingState(currentBlock, dir))
                        {
                            currentState = getState?.Invoke(x, y, z);
                        }

                        int atlasIdx = GetAtlasIndex(currentBlock, currentState, dir);

                        if (atlasIdx >= 0)
                        {
                            mask[mu, mv].occluded = true;
                            mask[mu, mv].atlasIndex = atlasIdx;
                            // Lod skirt, not real voxel. Cheap and takes light sample form neighboring
                            //chunk. Treat the skirts as exposed to skylight and omits blocks
                            //not using block lighting.
                            mask[mu, mv].light = forceFace
                                ? VoxelLight.Pack(VoxelLight.Max, VoxelLight.Min)
                                : SampleLodFaceLight(getSkyLight, getBlockLight,
                                    x, y, z, dir, lodScale);
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
                    byte light = mask[u, v].light;

                    // extend width (v direction)
                    int width = 1;
                    while (v + width < mSize && mask[u, v + width].occluded &&
                           mask[u, v + width].atlasIndex == atlasIndex &&
                           mask[u, v + width].light == light)
                        width++;

                    // extend height (u direction)
                    int height = 1;
                    bool done = false;
                    while (u + height < mSize && !done)
                    {
                        for (int k = 0; k < width; k++)
                        {
                            if (!mask[u + height, v + k].occluded || mask[u + height, v + k].atlasIndex != atlasIndex ||
                                mask[u + height, v + k].light != light)
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
                    AddQuadFromMask(u, v, width, height, w, dir, atlasIndex, mesh, lodScale, light);
                    
                    // advance v cursor
                    v += width;
                }
            }

        } // end w loop
    }
    
    private static bool ShouldGreedyMeshBlock(byte blockId,int lodScale, out Block block)
    {
        block = null;
        if (blockId == 0)
            return false;

        block = GetBlockInfo(blockId);
        if (block == null)
            return false;

        if (block.shapeIndex != (int)BlockShapes.Cube)
            return false;

        if (block.isTransparent && !ShouldTreatTransparentAsSolid(lodScale))
            return false;

        return true;
    }

    private static bool ShouldRenderGreedyFace(byte neighborId, bool forceFace, int lodScale)
    {
        if (forceFace)
            return true;

        if (neighborId == 0)
            return true;

        Block neighbor = GetBlockInfo(neighborId);
        if (neighbor == null)
            return false;

        if (neighbor.shapeIndex != (int)BlockShapes.Cube)
            return true;

        if (neighbor.isTransparent)
            return !ShouldTreatTransparentAsSolid(lodScale);

        return false;
    }

    private static bool ShouldTreatTransparentAsSolid(int lodScale)
    {
        return lodScale > 1;
    }

    private static bool HasCustomShape(byte blockId ,BlockStateContainer state)
    {
        if (state == null || state.StateCount <= 0)
            return false;

        return TryGetCustomBounds(blockId ,state, out _, out _);
    }

    private static bool ShouldUseStateDrivenMesh(byte blockId)
    {
        if (blockId == 0)
            return false;

        if (IsTransparent(blockId))
            return true;

        Block block = GetBlockInfo(blockId);
        return block != null && block.shapeIndex != (int)BlockShapes.Cube;
    }

    private static bool TryGetCustomBounds(byte blockId ,BlockStateContainer state, out Vector3 min, out Vector3 max)
    {
        min = Vector3.zero;
        max = Vector3.one;

        if (state == null || state.StateCount <= 0)
            return false;

        TryGetHeight(state, out float height);
        TryGetWidth(state, out float width);

        bool isFullHeight = height >= 1f - FullBlockEpsilon;
        bool isFullWidth = width >= 1f - FullBlockEpsilon;
        Block block = GetBlockInfo(blockId);
        bool isCenteredBlock = block != null && block.isCentered;

        if (isFullHeight && isFullWidth)
            return false;

        if (isCenteredBlock)
        {
            float widthInset = (1f - width) * 0.5f;
            
            //Lies on the ground!
            min = new Vector3(widthInset, 0f, widthInset);
            max = new Vector3(1f - widthInset, height, 1f - widthInset);
            return true;
        }
        
        string facing = GetFacing(state);
        switch (facing)
        {
            case "down":
                min.y = 1f - height;
                break;
            case "east":
                min.x = 1f - height;
                break;
            case "west":
                max.x = height;
                break;
            case "north":
                min.z = 1f - height;
                break;
            case "south":
                max.z = height;
                break;
            default:
                max.y = height;
                break;
        }
        
        if (!isFullWidth)
            ApplyCenteredWidth(facing, width, ref min, ref max);

        return true;
    }

    private static bool TryGetHeight(BlockStateContainer state, out float height)
    {
        height = 1f;
        string rawHeight = state?.GetState(BlockStateKeys.HeightState);
        if (string.IsNullOrWhiteSpace(rawHeight))
            return false;

        if (!float.TryParse(rawHeight, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out height))
        {
            height = 1f;
            return false;
        }

        height = Mathf.Clamp01(height);
        return true;
    }
    
    private static bool TryGetWidth(BlockStateContainer state, out float width)
    {
        width = 1f;
        string rawWidth = state?.GetState(BlockStateKeys.WidthState);
        if (string.IsNullOrWhiteSpace(rawWidth))
            return false;

        if (!float.TryParse(rawWidth, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out width))
        {
            width = 1f;
            return false;
        }

        width = Mathf.Clamp01(width);
        return true;
    }

    private static void ApplyCenteredWidth(string facing, float width, ref Vector3 min, ref Vector3 max)
    {
        float inset = (1f - width) * 0.5f;
        float centeredMin = inset;
        float centeredMax = 1f - inset;

        switch (facing)
        {
            case "east":
            case "west":
                min.y = Mathf.Max(min.y, centeredMin);
                max.y = Mathf.Min(max.y, centeredMax);
                min.z = Mathf.Max(min.z, centeredMin);
                max.z = Mathf.Min(max.z, centeredMax);
                break;
            case "north":
            case "south":
                min.x = Mathf.Max(min.x, centeredMin);
                max.x = Mathf.Min(max.x, centeredMax);
                min.y = Mathf.Max(min.y, centeredMin);
                max.y = Mathf.Min(max.y, centeredMax);
                break;
            default:
                min.x = Mathf.Max(min.x, centeredMin);
                max.x = Mathf.Min(max.x, centeredMax);
                min.z = Mathf.Max(min.z, centeredMin);
                max.z = Mathf.Min(max.z, centeredMax);
                break;
        }
    }

    private static string GetFacing(BlockStateContainer state)
    {
        string facing = state?.GetState(BlockStateKeys.DirectionalFacing);
        if (!string.IsNullOrWhiteSpace(facing))
            return facing;

        return "up";
    }

    private static string GetFrontTextureFacing(BlockStateContainer state)
    {
        string facing = state?.GetState(BlockStateKeys.DirectionalFacing);
        
        if (facing == DirectionalFacing.North ||
            facing == DirectionalFacing.South ||
            facing == DirectionalFacing.East ||
            facing == DirectionalFacing.West)
        {
            return facing;
        }

        return DirectionalFacing.North;
    }

    private static bool IsTransparent(byte blockId)
    {
        Block block = GetBlockInfo(blockId);
        return block != null && block.isTransparent;
    }
    
    private static bool IsStretchy(byte blockId)
    {
        Block block = GetBlockInfo(blockId);
        return block != null && block.isStrechy;
    }

    private static bool IsNonCubeShape(byte blockId)
    {
        Block block = GetBlockInfo(blockId);
        return block != null && block.shapeIndex != (int)BlockShapes.Cube;
    }

    private static Block GetBlockInfo(byte blockId)
    {
        var tbi = BlockRegistry.ThreadBlockInfo;
        if (tbi != null && blockId >= 0 && blockId < tbi.Length)
            return tbi[blockId];

        return BlockRegistry.GetBlock((int)blockId);
    }
    
    private static bool ShouldReadFacingState(Block block, Vector3Int dir)
    {
        if (block == null)
            return false;

        if (dir == Vector3Int.up || dir == Vector3Int.down)
            return false;

        return block.frontIndex >= 0;
    }
    
    private static int GetAtlasIndex(byte blockId, BlockStateContainer state, Vector3Int dir)
    {
        Block block = GetBlockInfo(blockId);
        return GetAtlasIndex(block, state, dir);
    }

    private static int GetAtlasIndex(Block block, BlockStateContainer state, Vector3Int dir)
    {
        if (block == null)
            return -1;

        if (dir == Vector3Int.up)
            return block.topIndex;

        if (dir == Vector3Int.down)
            return block.bottomIndex;

        int atlasIndex = block.sideIndex;
        string facing = GetFrontTextureFacing(state);
        bool isFront =
            (dir == Vector3Int.forward && facing == "north") ||
            (dir == Vector3Int.back && facing == "south") ||
            (dir == Vector3Int.right && facing == "east") ||
            (dir == Vector3Int.left && facing == "west");

        if (isFront && block.frontIndex >= 0)
            atlasIndex = block.frontIndex;

        return atlasIndex;
    }

    private static void AppendStateDrivenMeshes(
        Func<int, int, int, byte> getBlock,
        Func<int, int, int, BlockStateContainer> getState,
        Func<int, int, int, byte> getSkyLight,
        Func<int, int, int, byte> getBlockLight,
        MeshData mesh, IReadOnlyCollection<Vector3Int> specialMeshBlocks)
    {
        Vector3Int[] dirs =
        {
            Vector3Int.right, Vector3Int.left,
            Vector3Int.up, Vector3Int.down,
            Vector3Int.forward, Vector3Int.back
        };

        foreach (Vector3Int localPos in specialMeshBlocks)
        {
            int x = localPos.x;
            int y = localPos.y;
            int z = localPos.z;

            byte blockId = getBlock(x, y, z);
            if (blockId == 0)
                continue;

            BlockStateContainer state = getState?.Invoke(x, y, z);
            if (!ShouldUseStateDrivenMesh(blockId))
                continue;
            
            Block block = GetBlockInfo(blockId);
            if (block != null && block.shapeIndex == (int)BlockShapes.Triangle)
            {
                AddTrianglePrismMesh(getBlock, getState, getSkyLight, getBlockLight, x, y, z, blockId, state, mesh);
                continue;
            }
            if (block != null && block.shapeIndex == (int)BlockShapes.Pyramid)
            {
                AddPyramidMesh(getBlock, getState, getSkyLight, getBlockLight, x, y, z, blockId, state, mesh);
                continue;
            }

            if (block != null && block.shapeIndex == (int)BlockShapes.CornerTriangle)
            {
                AddCornerRampMesh(getBlock, getState, getSkyLight, getBlockLight, x, y, z, blockId, state, mesh);
                continue;
            }
            if (block != null && block.shapeIndex == (int)BlockShapes.InvertedCornerTriangle)
            {
                AddInvertedCornerRampMesh(getBlock, getState, getSkyLight, getBlockLight, x, y, z, blockId, state, mesh);
                continue;
            }


            if (!TryGetStateDrivenBounds(blockId, state, out Vector3 min, out Vector3 max))
                continue;

            foreach (Vector3Int dir in dirs)
            {
                if (!ShouldRenderStateDrivenFace(getBlock, getState, x, y, z, dir, min, max))
                    continue;

                int atlasIndex = GetAtlasIndex(blockId, state, dir);
                if (atlasIndex < 0)
                    continue;

                byte light = SampleCustomFaceLight(getSkyLight, getBlockLight, x, y, z, dir);
                AddStateDrivenQuad(new Vector3(x, y, z), min, max, dir, atlasIndex, mesh, blockId, state, light);
            }
        }


    }
    
    private static void AddCornerRampMesh(
        Func<int, int, int, byte> getBlock,
        Func<int, int, int, BlockStateContainer> getState,
        Func<int, int, int, byte> getSkyLight,
        Func<int, int, int, byte> getBlockLight,
        int x,
        int y,
        int z,
        byte blockId,
        BlockStateContainer state,
        MeshData mesh)
    {
        Vector3 blockOffset = new Vector3(x, y, z);
        string facing = GetFacing(state);
        int turns = GetTurnsFromNorth(facing);

        Vector3[] baseVertices =
        {
            new Vector3(0f, 0f, 0f), // 0
            new Vector3(1f, 0f, 0f), // 1
            new Vector3(0f, 0f, 1f), // 2
            new Vector3(1f, 0f, 1f), // 3 (new 4th bottom corner)
            new Vector3(1f, 1f, 1f), // 4 top corner
        };

        for (int i = 0; i < baseVertices.Length; i++)
            baseVertices[i] = RotatePointClockwise(baseVertices[i], turns);

        if (ShouldRenderTrianglePrismBoundaryFace(getBlock, getState, x, y, z, Vector3Int.down))
        {
            AddTrianglePrismFace(mesh, blockOffset, baseVertices, new[] { 0, 1, 3, 2 }, Vector3Int.down, 
                GetAtlasIndex(blockId, state, Vector3Int.down), blockId, SampleCustomFaceLight(getSkyLight, getBlockLight, x, y, z, Vector3Int.down));
        }

        // Same layout as the working 3-corner variant, with one extra bottom corner.
        AddTrianglePrismFace(mesh, blockOffset, baseVertices, new[] { 0, 2, 4 }, Vector3Int.left,
            GetAtlasIndex(blockId, state, Vector3Int.left), blockId, SampleCustomFaceLight(getSkyLight, getBlockLight, x, y, z, Vector3Int.left));
        AddTrianglePrismFace(mesh, blockOffset, baseVertices, new[] { 0, 4, 1 }, Vector3Int.up,
            GetAtlasIndex(blockId, state, Vector3Int.up), blockId, SampleCustomFaceLight(getSkyLight, getBlockLight, x, y, z, Vector3Int.up));
        AddTrianglePrismFace(mesh, blockOffset, baseVertices, new[] { 1, 4, 3 }, Vector3Int.right,
            GetAtlasIndex(blockId, state, Vector3Int.right), blockId, SampleCustomFaceLight(getSkyLight, getBlockLight, x, y, z, Vector3Int.right));
        AddTrianglePrismFace(mesh, blockOffset, baseVertices, new[] { 2, 3, 4 }, Vector3Int.forward,
            GetAtlasIndex(blockId, state, Vector3Int.forward), blockId, SampleCustomFaceLight(getSkyLight, getBlockLight, x, y, z, Vector3Int.forward));
    }
    
    private static void AddInvertedCornerRampMesh(
        Func<int, int, int, byte> getBlock,
        Func<int, int, int, BlockStateContainer> getState,
        Func<int, int, int, byte> getSkyLight,
        Func<int, int, int, byte> getBlockLight,
        int x,
        int y,
        int z,
        byte blockId,
        BlockStateContainer state,
        MeshData mesh)
    {
        Vector3 blockOffset = new Vector3(x, y, z);
        string facing = GetFacing(state);
        int turns = GetTurnsFromNorth(facing);

        Vector3[] verts =
        {
            new Vector3(0f, 0f, 0f), // 0 bottom-front-left
            new Vector3(1f, 0f, 0f), // 1 bottom-front-right
            new Vector3(1f, 0f, 1f), // 2 bottom-back-right
            new Vector3(0f, 0f, 1f), // 3 bottom-back-left
            new Vector3(0f, 1f, 1f), // 4 top-back-left
            new Vector3(1f, 1f, 0f), // 5 top-front-right
            new Vector3(1f, 1f, 1f), // 6 top-back-right
        };

        for (int i = 0; i < verts.Length; i++)
            verts[i] = RotatePointClockwise(verts[i], turns);

        // Flat bottom
        if (ShouldRenderTrianglePrismBoundaryFace(getBlock, getState, x, y, z, Vector3Int.down))
        {
            AddTrianglePrismFace(mesh, blockOffset, verts, new[] { 0, 1, 3, 2 }, Vector3Int.down,
                GetAtlasIndex(blockId, state, Vector3Int.down), blockId, SampleCustomFaceLight(getSkyLight, getBlockLight, x, y, z, Vector3Int.down));
        }

        // Top with 3 corners
        AddTrianglePrismFace(mesh, blockOffset, verts, new[] { 5, 4, 6 }, Vector3Int.up,
            GetAtlasIndex(blockId, state, Vector3Int.up), blockId, SampleCustomFaceLight(getSkyLight, getBlockLight, x, y, z, Vector3Int.up));

        // Two flat sides
        AddTrianglePrismFace(mesh, blockOffset, verts, new[] { 1, 5, 2, 6 }, Vector3Int.right,
            GetAtlasIndex(blockId, state, Vector3Int.right), blockId, SampleCustomFaceLight(getSkyLight, getBlockLight, x, y, z, Vector3Int.right));
        AddTrianglePrismFace(mesh, blockOffset, verts, new[] { 3, 2, 4, 6 }, Vector3Int.forward,
            GetAtlasIndex(blockId, state, Vector3Int.forward), blockId, SampleCustomFaceLight(getSkyLight, getBlockLight, x, y, z, Vector3Int.forward));

        // Remaining ramped faces
        AddTrianglePrismFace(mesh, blockOffset, verts, new[] { 0, 5, 1 }, Vector3Int.back,
            GetAtlasIndex(blockId, state, Vector3Int.back), blockId, SampleCustomFaceLight(getSkyLight, getBlockLight, x, y, z, Vector3Int.back));
        AddTrianglePrismFace(mesh, blockOffset, verts, new[] { 0, 3, 4 }, Vector3Int.left,
            GetAtlasIndex(blockId, state, Vector3Int.left), blockId, SampleCustomFaceLight(getSkyLight, getBlockLight, x, y, z, Vector3Int.left));
        AddTrianglePrismFace(mesh, blockOffset, verts, new[] { 0, 4, 5 }, Vector3Int.up,
            GetAtlasIndex(blockId, state, Vector3Int.up), blockId, SampleCustomFaceLight(getSkyLight, getBlockLight, x, y, z, Vector3Int.up));
    }

    private static void AddPyramidMesh(
        Func<int, int, int, byte> getBlock,
        Func<int, int, int, BlockStateContainer> getState,
        Func<int, int, int, byte> getSkyLight,
        Func<int, int, int, byte> getBlockLight,
        int x,
        int y,
        int z,
        byte blockId,
        BlockStateContainer state,
        MeshData mesh)
    {
        Vector3 blockOffset = new Vector3(x, y, z);
        Vector3[] pyramidVerts =
        {
            new Vector3(0f, 0f, 0f), // 0
            new Vector3(1f, 0f, 0f), // 1
            new Vector3(1f, 0f, 1f), // 2
            new Vector3(0f, 0f, 1f), // 3
            new Vector3(0.5f, 1f, 0.5f), // 4 apex
        };

        if (ShouldRenderTrianglePrismBoundaryFace(getBlock, getState, x, y, z, Vector3Int.down))
        {
            AddTrianglePrismFace(mesh, blockOffset, pyramidVerts, new[] { 0, 1, 3, 2 }, Vector3Int.down,
                GetAtlasIndex(blockId, state, Vector3Int.down), blockId, SampleCustomFaceLight(getSkyLight, getBlockLight, x, y, z, Vector3Int.down));
        }

        // Pyramid side faces are sloped and should always render, even with neighbors.
        AddTrianglePrismFace(mesh, blockOffset, pyramidVerts, new[] { 0, 4, 1 }, Vector3Int.back,
            GetAtlasIndex(blockId, state, Vector3Int.back), blockId, SampleCustomFaceLight(getSkyLight, getBlockLight, x, y, z, Vector3Int.back));
        AddTrianglePrismFace(mesh, blockOffset, pyramidVerts, new[] { 1, 4, 2 }, Vector3Int.right,
            GetAtlasIndex(blockId, state, Vector3Int.right), blockId, SampleCustomFaceLight(getSkyLight, getBlockLight, x, y, z, Vector3Int.right));
        AddTrianglePrismFace(mesh, blockOffset, pyramidVerts, new[] { 2, 4, 3 }, Vector3Int.forward,
            GetAtlasIndex(blockId, state, Vector3Int.forward), blockId, SampleCustomFaceLight(getSkyLight, getBlockLight, x, y, z, Vector3Int.forward));
        AddTrianglePrismFace(mesh, blockOffset, pyramidVerts, new[] { 3, 4, 0 }, Vector3Int.left,
            GetAtlasIndex(blockId, state, Vector3Int.left), blockId, SampleCustomFaceLight(getSkyLight, getBlockLight, x, y, z, Vector3Int.left));
    }

    private static void AddTrianglePrismMesh(
        Func<int, int, int, byte> getBlock,
        Func<int, int, int, BlockStateContainer> getState,
        Func<int, int, int, byte> getSkyLight,
        Func<int, int, int, byte> getBlockLight,
        int x,
        int y,
        int z,
        byte blockId,
        BlockStateContainer state,
        MeshData mesh)
    {
        Vector3 blockOffset = new Vector3(x, y, z);
        string facing = GetFacing(state);
        int turns = GetTurnsFromNorth(facing);

        Vector3[] baseVertices =
        {
            new Vector3(0f, 0f, 0f), // 0
            new Vector3(1f, 0f, 0f), // 1
            new Vector3(0f, 0f, 1f), // 2
            new Vector3(1f, 0f, 1f), // 3
            new Vector3(0f, 1f, 1f), // 4
            new Vector3(1f, 1f, 1f), // 5
        };

        for (int i = 0; i < baseVertices.Length; i++)
            baseVertices[i] = RotatePointClockwise(baseVertices[i], turns);

        // bottom face
        if (ShouldRenderTrianglePrismBoundaryFace(getBlock, getState, x, y, z, Vector3Int.down))
        {
            AddTrianglePrismFace(mesh, blockOffset, baseVertices, new[] { 0, 1, 2, 3 }, Vector3Int.down,
                GetAtlasIndex(blockId, state, Vector3Int.down), blockId, SampleCustomFaceLight(getSkyLight, getBlockLight, x, y, z, Vector3Int.down));
        }

        // vertical back face
        Vector3Int backDir = RotateDirClockwise(Vector3Int.forward, turns);
        if (ShouldRenderTrianglePrismBoundaryFace(getBlock, getState, x, y, z, backDir))
        {
            AddTrianglePrismFace(mesh, blockOffset, baseVertices, new[] { 2, 3, 4, 5 }, backDir,
                GetAtlasIndex(blockId, state, backDir), blockId, SampleCustomFaceLight(getSkyLight, getBlockLight, x, y, z, backDir));
        }

        // side triangle faces
        Vector3Int leftDir = RotateDirClockwise(Vector3Int.left, turns);
        if (ShouldRenderTrianglePrismBoundaryFace(getBlock, getState, x, y, z, leftDir))
        {
            AddTrianglePrismFace(mesh, blockOffset, baseVertices, new[] { 0, 2, 4 }, leftDir,
                GetAtlasIndex(blockId, state, leftDir), blockId, SampleCustomFaceLight(getSkyLight, getBlockLight, x, y, z, leftDir));
        }

        Vector3Int rightDir = RotateDirClockwise(Vector3Int.right, turns);
        if (ShouldRenderTrianglePrismBoundaryFace(getBlock, getState, x, y, z, rightDir))
        {
            AddTrianglePrismFace(mesh, blockOffset, baseVertices, new[] { 1, 5, 3 }, rightDir,
                GetAtlasIndex(blockId, state, rightDir), blockId, SampleCustomFaceLight(getSkyLight, getBlockLight, x, y, z, rightDir));
        }

        // sloped face is always rendered
        AddTrianglePrismFace(mesh, blockOffset, baseVertices, new[] { 0, 4, 1, 5 }, Vector3Int.up,
            GetAtlasIndex(blockId, state, Vector3Int.up), blockId, SampleCustomFaceLight(getSkyLight, getBlockLight, x, y, z, Vector3Int.up));
    }

    private static bool ShouldRenderTrianglePrismBoundaryFace(
        Func<int, int, int, byte> getBlock,
        Func<int, int, int, BlockStateContainer> getState,
        int x,
        int y,
        int z,
        Vector3Int dir)
    {
        byte neighborId = getBlock(x + dir.x, y + dir.y, z + dir.z);
        return ShouldRenderGreedyFace(neighborId, false, 1);
    }

    private static void AddTrianglePrismFace(
        MeshData mesh,
        Vector3 blockOffset,
        Vector3[] prismVerts,
        int[] indices,
        Vector3Int fallbackNormal,
        int atlasIndex,
        byte blockId,
        byte light)
    {
        if (atlasIndex < 0)
            return;

        int baseIndex = mesh.VertexCount;
        ushort tileIndex = PackAtlasTileIndex(atlasIndex);
        for (int i = 0; i < indices.Length; i++)
        {
            uint uv = MeshUtilityCustom.ChunkVertex.PackUv(i & 1, i >> 1);
            mesh.AddVertex(blockOffset + prismVerts[indices[i]], 0u, uv, tileIndex, light);
        }
        
        Vector3 faceNormal = ComputeFaceNormal(mesh, baseIndex, indices.Length, fallbackNormal);
        mesh.SetFaceNormal(baseIndex, indices.Length, faceNormal);

        if (indices.Length == 4)
        {
            AddFaceTriangles(mesh.triangles, baseIndex, 0, 1, 2, 3, faceNormal, false, mesh, baseIndex);
        }
        else if (indices.Length == 3)
        {
            AddTriangle(mesh.triangles, baseIndex, 0, 1, 2, faceNormal, false, mesh, baseIndex);
        }
        
    }

    private static ushort PackAtlasTileIndex(int textureID)
    {
        return (ushort)Mathf.Clamp(textureID, 0, ushort.MaxValue);
    }

    private static Vector3 ComputeFaceNormal(MeshData mesh, int baseIndex, int count, Vector3Int fallbackNormal)
    {
        if (count < 3)
            return fallbackNormal;

        Vector3 a = mesh.GetPosition(baseIndex + 1) - mesh.GetPosition(baseIndex);
        Vector3 b = mesh.GetPosition(baseIndex + 2) - mesh.GetPosition(baseIndex);
        Vector3 normal = Vector3.Cross(a, b).normalized;
        if (normal.sqrMagnitude <= 0f)
            return fallbackNormal;

        return normal;
    }

    private static void AddFaceTriangles(List<int> destination, int baseIndex, int i0, int i1, int i2, int i3, Vector3 normal, bool useAreaCheck = false, MeshData mesh = null, int vertexBase = 0)
    {
        if (Vector3.Dot(Vector3.Cross(
                mesh != null ? mesh.GetPosition(vertexBase + i1) - mesh.GetPosition(vertexBase + i0) : Vector3.zero,
                mesh != null ? mesh.GetPosition(vertexBase + i2) - mesh.GetPosition(vertexBase + i0) : Vector3.zero), normal) < 0f)
        {
            int t = i1;
            i1 = i2;
            i2 = t;
        }

        if (!useAreaCheck || IsTriangleValid(mesh, vertexBase, i0, i1, i2))
        {
            destination.Add(baseIndex + i0);
            destination.Add(baseIndex + i1);
            destination.Add(baseIndex + i2);
        }

        if (!useAreaCheck || IsTriangleValid(mesh, vertexBase, i2, i1, i3))
        {
            destination.Add(baseIndex + i2);
            destination.Add(baseIndex + i1);
            destination.Add(baseIndex + i3);
        }
    }

    private static void AddTriangle(List<int> destination, int baseIndex, int i0, int i1, int i2, Vector3 normal, bool useAreaCheck = false, MeshData mesh = null, int vertexBase = 0)
    {
        if (Vector3.Dot(Vector3.Cross(
                mesh != null ? mesh.GetPosition(vertexBase + i1) - mesh.GetPosition(vertexBase + i0) : Vector3.zero,
                mesh != null ? mesh.GetPosition(vertexBase + i2) - mesh.GetPosition(vertexBase + i0) : Vector3.zero), normal) < 0f)
        {
            int t = i1;
            i1 = i2;
            i2 = t;
        }

        if (!useAreaCheck || IsTriangleValid(mesh, vertexBase, i0, i1, i2))
        {
            destination.Add(baseIndex + i0);
            destination.Add(baseIndex + i1);
            destination.Add(baseIndex + i2);
        }
    }

    private static bool IsTriangleValid(MeshData mesh, int vertexBase, int i0, int i1, int i2)
    {
        const float areaEpsilon = 1e-6f;
        if (mesh == null)
            return true;

        Vector3 a = mesh.GetPosition(vertexBase + i1) - mesh.GetPosition(vertexBase + i0);
        Vector3 b = mesh.GetPosition(vertexBase + i2) - mesh.GetPosition(vertexBase + i0);
        return Vector3.Cross(a, b).sqrMagnitude * 0.25f > areaEpsilon;
    }

    private static int GetTurnsFromNorth(string facing)
    {
        switch (facing)
        {
            case "east":
                return 1;
            case "south":
                return 2;
            case "west":
                return 3;
            default:
                return 0;
        }
    }

    private static Vector3 RotatePointClockwise(Vector3 point, int turns)
    {
        turns = ((turns % 4) + 4) % 4;
        Vector3 rotated = point;
        for (int i = 0; i < turns; i++)
            rotated = new Vector3(rotated.z, rotated.y, 1f - rotated.x);

        return rotated;
    }

    private static Vector3Int RotateDirClockwise(Vector3Int dir, int turns)
    {
        turns = ((turns % 4) + 4) % 4;
        Vector3Int rotated = dir;
        for (int i = 0; i < turns; i++)
            rotated = new Vector3Int(rotated.z, rotated.y, -rotated.x);

        return rotated;
    }
    private static bool TryGetStateDrivenBounds(byte blockId, BlockStateContainer state, out Vector3 min, out Vector3 max)
    {
        if (TryGetCustomBounds(blockId, state, out min, out max))
            return true;

        if (IsTransparent(blockId))
        {
            min = Vector3.zero;
            max = Vector3.one;
            return true;
        }

        min = Vector3.zero;
        max = Vector3.one;
        return false;
    }

    private static bool ShouldRenderStateDrivenFace(
        Func<int, int, int, byte> getBlock,
        Func<int, int, int, BlockStateContainer> getState,
        int x, int y, int z,
        Vector3Int dir,
        Vector3 min,
        Vector3 max)
    {
        bool touchesBoundary =
            (dir == Vector3Int.left && Mathf.Approximately(min.x, 0f)) ||
            (dir == Vector3Int.right && Mathf.Approximately(max.x, 1f)) ||
            (dir == Vector3Int.down && Mathf.Approximately(min.y, 0f)) ||
            (dir == Vector3Int.up && Mathf.Approximately(max.y, 1f)) ||
            (dir == Vector3Int.back && Mathf.Approximately(min.z, 0f)) ||
            (dir == Vector3Int.forward && Mathf.Approximately(max.z, 1f));

        if (!touchesBoundary)
            return true;

        byte neighborId = getBlock(x + dir.x, y + dir.y, z + dir.z);
        return ShouldRenderGreedyFace(neighborId, false, 1);
    }

    private static void AddStateDrivenQuad(
        Vector3 blockOffset,
        Vector3 min,
        Vector3 max,
        Vector3Int dir,
        int atlasIndex,
        MeshData mesh, byte blockId, BlockStateContainer state,
        byte light)
    {
        Vector3[] quad =
        {
            Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero
        };

        if (dir == Vector3Int.right)
        {
            quad[0] = new Vector3(max.x, min.y, max.z);
            quad[1] = new Vector3(max.x, min.y, min.z);
            quad[2] = new Vector3(max.x, max.y, max.z);
            quad[3] = new Vector3(max.x, max.y, min.z);
        }
        else if (dir == Vector3Int.left)
        {
            quad[0] = new Vector3(min.x, min.y, min.z);
            quad[1] = new Vector3(min.x, min.y, max.z);
            quad[2] = new Vector3(min.x, max.y, min.z);
            quad[3] = new Vector3(min.x, max.y, max.z);
        }
        else if (dir == Vector3Int.up)
        {
            quad[0] = new Vector3(min.x, max.y, min.z);
            quad[1] = new Vector3(min.x, max.y, max.z);
            quad[2] = new Vector3(max.x, max.y, min.z);
            quad[3] = new Vector3(max.x, max.y, max.z);
        }
        else if (dir == Vector3Int.down)
        {
            quad[0] = new Vector3(min.x, min.y, max.z);
            quad[1] = new Vector3(min.x, min.y, min.z);
            quad[2] = new Vector3(max.x, min.y, max.z);
            quad[3] = new Vector3(max.x, min.y, min.z);
        }
        else if (dir == Vector3Int.forward)
        {
            quad[0] = new Vector3(min.x, min.y, max.z);
            quad[1] = new Vector3(max.x, min.y, max.z);
            quad[2] = new Vector3(min.x, max.y, max.z);
            quad[3] = new Vector3(max.x, max.y, max.z);
        }
        else if (dir == Vector3Int.back)
        {
            quad[0] = new Vector3(max.x, min.y, min.z);
            quad[1] = new Vector3(min.x, min.y, min.z);
            quad[2] = new Vector3(max.x, max.y, min.z);
            quad[3] = new Vector3(min.x, max.y, min.z);
        }

        int baseIndex = mesh.VertexCount;
        bool stretchTexture = IsStretchy(blockId);
        AddStateDrivenFaceVertices(dir, quad, blockOffset, atlasIndex, stretchTexture, GetFacing(state), light, mesh);

        int i0 = baseIndex + 0;
        int i1 = baseIndex + 1;
        int i2 = baseIndex + 2;
        int i3 = baseIndex + 3;

        int t0a = i0, t0b = i2, t0c = i1;
        int t1a = i2, t1b = i3, t1c = i1;

        Vector3 A = mesh.GetPosition(t0b) - mesh.GetPosition(t0a);
        Vector3 B = mesh.GetPosition(t0c) - mesh.GetPosition(t0a);
        Vector3 faceNormal = Vector3.Cross(A, B).normalized;

        if (faceNormal.sqrMagnitude <= 0f)
            faceNormal = dir;
        
        if (Vector3.Dot(faceNormal, (Vector3)dir) < 0f)
        {
            t0b = i1;
            t0c = i2;
            t1b = i1;
            t1c = i3;
            faceNormal = -faceNormal;
        }
        
        mesh.SetFaceNormal(baseIndex, 4, faceNormal);

        mesh.triangles.Add(t0a);
        mesh.triangles.Add(t0b);
        mesh.triangles.Add(t0c);
        mesh.triangles.Add(t1a);
        mesh.triangles.Add(t1b);
        mesh.triangles.Add(t1c);
    }

    private static void AddStateDrivenFaceVertices(
        Vector3Int dir,
        Vector3[] quad,
        Vector3 blockOffset,
        int textureID,
        bool stretchTexture,
        string facing,
        byte light,
        MeshData mesh)
    {
        
        int turnsToEast = GetHorizontalTurnsToEastReference(facing);
        Vector3Int localDir = RotateDirToEastReference(dir, turnsToEast);
        Vector3[] localQuad = new Vector3[quad.Length];
        
        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float minZ = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;
        float maxZ = float.NegativeInfinity;

        for (int i = 0; i < quad.Length; i++)
        {
            Vector3 rotated = RotatePointToEastReference(quad[i], turnsToEast);
            localQuad[i] = rotated;

            minX = Mathf.Min(minX, rotated.x);
            minY = Mathf.Min(minY, rotated.y);
            minZ = Mathf.Min(minZ, rotated.z);
            maxX = Mathf.Max(maxX, rotated.x);
            maxY = Mathf.Max(maxY, rotated.y);
            maxZ = Mathf.Max(maxZ, rotated.z);
        }
        
        float uScale = 1f;
        float vScale = 1f;
        bool flipForDownFacing = facing == DirectionalFacing.Down;

        if (!stretchTexture)
        {
            if (localDir.x != 0)
            {
                uScale = maxZ - minZ;
                vScale = maxY - minY;
            }
            else if (localDir.y != 0)
            {
                uScale = maxZ - minZ;
                vScale = maxX - minX;
            }
            else
            {
                uScale = maxX - minX;
                vScale = maxY - minY;
            }
        }

        ushort tileIndex = PackAtlasTileIndex(textureID);
        for (int i = 0; i < localQuad.Length; i++)
        {
            Vector3 p = localQuad[i];
            float u;
            float v;

            if (localDir.x > 0)
            {
                u = Mathf.InverseLerp(maxZ, minZ, p.z);
                v = Mathf.InverseLerp(minY, maxY, p.y);
            }
            else if (localDir.x < 0)
            {
                u = Mathf.InverseLerp(minZ, maxZ, p.z);
                v = Mathf.InverseLerp(minY, maxY, p.y);
            }
            else if (localDir.y > 0)
            {
                u = Mathf.InverseLerp(minZ, maxZ, p.z);
                v = Mathf.InverseLerp(maxX, minX, p.x);
            }
            else if (localDir.y < 0)
            {
                u = Mathf.InverseLerp(maxZ, minZ, p.z);
                v = Mathf.InverseLerp(maxX, minX, p.x);
            }
            else
            {
                u = Mathf.InverseLerp(maxX, minX, p.x);
                v = Mathf.InverseLerp(minY, maxY, p.y);
            }

            if (flipForDownFacing)
                v = 1f - v;

            uint uv = MeshUtilityCustom.ChunkVertex.PackUv(u * uScale, v * vScale);
            mesh.AddVertex(blockOffset + quad[i], 0u, uv, tileIndex, light);
        }
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
    
    private static byte SampleLodFaceBlock(
        Func<int, int, int, byte> getBlock,
        int x, int y, int z,
        Vector3Int dir,
        int scale)
    {
        GetLodFaceSampleBounds(x, y, z, dir, scale,
            out int startX, out int startY, out int startZ,
            out int sizeX, out int sizeY, out int sizeZ);

        for (int dx = 0; dx < sizeX; dx++)
        for (int dy = sizeY - 1; dy >= 0; dy--)
        for (int dz = 0; dz < sizeZ; dz++)
        {
            byte block = getBlock(startX + dx, startY + dy, startZ + dz);
            if (block != 0)
                return block;
        }

        return 0;
    }

    private static byte SampleLodFaceLight(
        Func<int, int, int, byte> getSkyLight,
        Func<int, int, int, byte> getBlockLight,
        int x, int y, int z,
        Vector3Int dir,
        int scale)
    {
        GetLodFaceSampleBounds(x, y, z, dir, scale,
            out int startX, out int startY, out int startZ,
            out int sizeX, out int sizeY, out int sizeZ);

        byte sky = VoxelLight.Min;
        byte block = VoxelLight.Min;
        for (int dx = 0; dx < sizeX; dx++)
        for (int dy = 0; dy < sizeY; dy++)
        for (int dz = 0; dz < sizeZ; dz++)
        {
            int sampleX = startX + dx;
            int sampleY = startY + dy;
            int sampleZ = startZ + dz;
            if (getSkyLight != null)
                sky = Math.Max(sky, getSkyLight(sampleX, sampleY, sampleZ));
            if (getBlockLight != null)
                block = Math.Max(block, getBlockLight(sampleX, sampleY, sampleZ));
        }

        if (getSkyLight == null)
            sky = VoxelLight.Max;

        return VoxelLight.Pack(sky, block);
    }

    private static void GetLodFaceSampleBounds(
        int x, int y, int z,
        Vector3Int dir,
        int scale,
        out int startX, out int startY, out int startZ,
        out int sizeX, out int sizeY, out int sizeZ)
    {
        // Sample the single plane directly outside the coarse cell. Its tangential
        // dimensions cover the entire LOD face, while its normal dimension stays
        // within the mesher's one-voxel padded chunk border.
        startX = x + (dir.x > 0 ? scale : dir.x < 0 ? -1 : 0);
        startY = y + (dir.y > 0 ? scale : dir.y < 0 ? -1 : 0);
        startZ = z + (dir.z > 0 ? scale : dir.z < 0 ? -1 : 0);
        sizeX = dir.x == 0 ? scale : 1;
        sizeY = dir.y == 0 ? scale : 1;
        sizeZ = dir.z == 0 ? scale : 1;
    }
    
    private static void AddQuadFromMask(int u, int v, int width, int height, int w, Vector3Int dir, int atlasIndex, MeshData mesh, int lodScale,
        byte light)
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


        int baseIndex = mesh.VertexCount;
        
        uint packedNormal = MeshUtilityCustom.ChunkVertex.PackNormal(dir);
        ushort tileIndex = PackAtlasTileIndex(atlasIndex);
        uint packedU = MeshUtilityCustom.ChunkVertex.PackUv(dir.z != 0 ? height : width, 0f);
        uint packedV = MeshUtilityCustom.ChunkVertex.PackUv(0f, dir.z != 0 ? width : height);

        // Emit complete CPU/GPU vertices directly, packing shared face attributes once.
        for (int i = 0; i < faceVerts.Length; i++)
        {
            Vector3 vert = faceVerts[i];
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
            
            uint uv = ((i & 1) != 0 ? packedU : 0u) | ((i & 2) != 0 ? packedV : 0u);
            mesh.AddVertex(pos, packedNormal, uv, tileIndex, light);
        }


        // Triangles (winding check)
        int i0 = baseIndex + 0;
        int i1 = baseIndex + 1;
        int i2 = baseIndex + 2;
        int i3 = baseIndex + 3;

        int t0a = i0, t0b = i2, t0c = i1;
        int t1a = i2, t1b = i3, t1c = i1;

        Vector3 A = mesh.GetPosition(t0b) - mesh.GetPosition(t0a);
        Vector3 B = mesh.GetPosition(t0c) - mesh.GetPosition(t0a);
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
        
    }
    
    
    private static int GetHorizontalTurnsToEastReference(string facing)
    {
        switch (facing)
        {
            case "north":
                return 1;
            case "west":
                return 2;
            case "south":
                return 3;
            default:
                return 0;
        }
    }

    private static Vector3 RotatePointToEastReference(Vector3 point, int turns)
    {
        turns = ((turns % 4) + 4) % 4;
        switch (turns)
        {
            case 1:
                return new Vector3(point.z, point.y, 1f - point.x);
            case 2:
                return new Vector3(1f - point.x, point.y, 1f - point.z);
            case 3:
                return new Vector3(1f - point.z, point.y, point.x);
            default:
                return point;
        }
    }

    private static Vector3Int RotateDirToEastReference(Vector3Int dir, int turns)
    {
        turns = ((turns % 4) + 4) % 4;
        switch (turns)
        {
            case 1:
                return new Vector3Int(dir.z, dir.y, -dir.x);
            case 2:
                return new Vector3Int(-dir.x, dir.y, -dir.z);
            case 3:
                return new Vector3Int(-dir.z, dir.y, dir.x);
            default:
                return dir;
        }
    }
    
    private static byte SampleFaceLight(Func<int, int, int, byte> getSkyLight, Func<int, int, int, byte> getBlockLight,
        int lightX, int lightY, int lightZ, int fallbackX, int fallbackY, int fallbackZ)
    {
        byte sky = getSkyLight != null ? getSkyLight(lightX, lightY, lightZ) : VoxelLight.Max;
        byte block = getBlockLight != null ? getBlockLight(lightX, lightY, lightZ) : VoxelLight.Min;

        if (sky == VoxelLight.Min && block == VoxelLight.Min)
        {
            sky = getSkyLight != null ? getSkyLight(fallbackX, fallbackY, fallbackZ) : VoxelLight.Max;
            block = getBlockLight != null ? getBlockLight(fallbackX, fallbackY, fallbackZ) : VoxelLight.Min;
        }

        return VoxelLight.Pack(sky, block);
    }
    
    // Custom faces are not necessarily located on a full voxel boundary. Sample
    // the cell outside the face first, just like a cube face, and fall back to the
    // custom block's own (transparent) light value for inset and sloped surfaces.
    private static byte SampleCustomFaceLight(
        Func<int, int, int, byte> getSkyLight,
        Func<int, int, int, byte> getBlockLight,
        int x, int y, int z,
        Vector3Int outwardDirection)
    {
        return SampleFaceLight(
            getSkyLight, getBlockLight,
            x + outwardDirection.x, y + outwardDirection.y, z + outwardDirection.z,
            x, y, z);
    }
    
}


