using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Core.Rendering
{
    /// <summary>
    /// Persistent, dynamically growing GPU storage for chunk meshes. This Phase 3
    /// implementation owns the terrain GPU resources and renders all resident chunks
    /// through indexed indirect page submissions.
    /// </summary>
    public sealed class PagedTerrainMeshStorage : IDisposable
    {
        private const int DeferredFreeFrames = 3;
        private readonly int standardVertexCapacity;
        private readonly int standardIndexCapacity;
        private readonly int standardCommandCapacity;
        private readonly List<TerrainMeshPage> pages = new List<TerrainMeshPage>();
        private readonly Dictionary<Vector3Int, ChunkMeshHandle> allocations =
            new Dictionary<Vector3Int, ChunkMeshHandle>();
        private readonly Queue<RetiredAllocation> retiredAllocations = new Queue<RetiredAllocation>();
        private readonly Material indirectMaterial;
        private readonly ComputeShader frustumCullingShader;
        private readonly int hierarchyCullingKernel = -1;
        private readonly int frustumCullingKernel = -1;
        private readonly Plane[] frustumPlaneScratch = new Plane[6];
        private readonly Vector4[] frustumPlaneVectorScratch = new Vector4[6];
        private int nextPageId;
        private bool disposed;

        public int PageCount => pages.Count;
        public int ResidentChunkCount => allocations.Count;
        public bool CanRender => indirectMaterial != null && frustumCullingShader != null;

        public PagedTerrainMeshStorage(int vertexCapacity, int indexCapacity, int commandCapacity,
            Material indirectMaterialTemplate, ComputeShader frustumCullingShaderTemplate)
        {
            standardVertexCapacity = Math.Max(1, vertexCapacity);
            standardIndexCapacity = Math.Max(1, indexCapacity);
            standardCommandCapacity = Math.Max(1, commandCapacity);

            if (indirectMaterialTemplate != null)
            {
                indirectMaterial = new Material(indirectMaterialTemplate)
                {
                    name = "PagedTerrainMaterial",
                    enableInstancing = true
                };
            }

            frustumCullingShader = frustumCullingShaderTemplate;
            if (frustumCullingShader != null)
            {
                hierarchyCullingKernel = frustumCullingShader.FindKernel("CullHierarchy");
                frustumCullingKernel = frustumCullingShader.FindKernel("CullChunks");
            }
                
        }

        public bool TryUpload(Vector3Int coordinate, int revision,
            MeshUtilityCustom.ChunkMeshUploadData meshData, Vector3 worldOrigin)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(PagedTerrainMeshStorage));
            if (meshData == null)
                return false;

            if (meshData.vertices.Length == 0 || meshData.indices.Length == 0)
            {
                Remove(coordinate);
                return true;
            }

            if (!TryCreateAllocation(meshData.vertices.Length, meshData.indices.Length,
                    out TerrainMeshPage page, out ChunkMeshHandle replacement))
                return false;

            replacement.Coordinate = coordinate;
            replacement.Revision = revision;
            // Cull against a stable logical chunk volume. Mesh bounds can change as
            // neighboring chunks, lighting, or LOD rebuild the visible faces; using
            // those transient bounds made chunks flicker at the edge of the frustum.
            replacement.Bounds = CreateChunkCullingBounds(worldOrigin);

            try
            {
                page.Upload(replacement, meshData, worldOrigin);
            }
            catch
            {
                page.DisableCommand(replacement.CommandSlot);
                page.Free(replacement);
                throw;
            }

            if (allocations.TryGetValue(coordinate, out ChunkMeshHandle oldAllocation))
            {
                FindPage(oldAllocation.PageId)?.DisableCommand(oldAllocation.CommandSlot);
                Retire(oldAllocation);
            }

            allocations[coordinate] = replacement;
            return true;
        }

        public void Render(Camera camera, Camera previewCamera = null)
        {
            if (disposed || indirectMaterial == null || frustumCullingShader == null
                || camera == null)
                return;
            
            GeometryUtility.CalculateFrustumPlanes(camera, frustumPlaneScratch);
            for (int i = 0; i < frustumPlaneScratch.Length; i++)
            {
                Plane plane = frustumPlaneScratch[i];
                Vector3 normal = plane.normal;
                frustumPlaneVectorScratch[i] =
                    new Vector4(normal.x, normal.y, normal.z, plane.distance);
            }
            
            for (int i = 0; i < pages.Count; i++) 
                pages[i].Render(indirectMaterial, frustumCullingShader,
                    hierarchyCullingKernel, frustumCullingKernel, camera, previewCamera,
                    frustumPlaneVectorScratch);
        }

        public void Remove(Vector3Int coordinate)
        {
            if (!allocations.TryGetValue(coordinate, out ChunkMeshHandle allocation))
                return;

            allocations.Remove(coordinate);
            FindPage(allocation.PageId)?.DisableCommand(allocation.CommandSlot);
            Retire(allocation);
        }

        public void ProcessDeferredFrees(int frame)
        {
            while (retiredAllocations.Count > 0 && retiredAllocations.Peek().ReleaseFrame <= frame)
            {
                RetiredAllocation retired = retiredAllocations.Dequeue();
                TerrainMeshPage page = FindPage(retired.Handle.PageId);
                page?.Free(retired.Handle);
            }

            ReleaseSurplusEmptyPages();
        }

        public Statistics GetStatistics()
        {
            long vertexCapacity = 0;
            long indexCapacity = 0;
            long freeVertices = 0;
            long freeIndices = 0;
            for (int i = 0; i < pages.Count; i++)
            {
                vertexCapacity += pages[i].VertexCapacity;
                indexCapacity += pages[i].IndexCapacity;
                freeVertices += pages[i].FreeVertices;
                freeIndices += pages[i].FreeIndices;
            }

            return new Statistics(pages.Count, allocations.Count, vertexCapacity,
                indexCapacity, freeVertices, freeIndices);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            for (int i = 0; i < pages.Count; i++)
                pages[i].Dispose();
            pages.Clear();
            allocations.Clear();
            retiredAllocations.Clear();
            if (indirectMaterial != null)
                UnityEngine.Object.Destroy(indirectMaterial);
        }

        private bool TryCreateAllocation(int vertexCount, int indexCount,
            out TerrainMeshPage selectedPage, out ChunkMeshHandle handle)
        {
            for (int i = 0; i < pages.Count; i++)
            {
                TerrainMeshPage page = pages[i];
                if (page.LargestFreeVertexRange < vertexCount ||
                    page.LargestFreeIndexRange < indexCount || page.FreeCommandSlots == 0)
                    continue;

                if (page.TryAllocate(vertexCount, indexCount, out handle))
                {
                    selectedPage = page;
                    return true;
                }
            }

            // Exceptional meshes get a dedicated-capacity page rather than being lost.
            int vertexCapacity = Math.Max(standardVertexCapacity, NextPowerOfTwo(vertexCount));
            int indexCapacity = Math.Max(standardIndexCapacity, NextPowerOfTwo(indexCount));
            int commandCapacity = vertexCapacity == standardVertexCapacity &&
                                  indexCapacity == standardIndexCapacity
                ? standardCommandCapacity
                : 1;

            selectedPage = new TerrainMeshPage(nextPageId++, vertexCapacity,
                indexCapacity, commandCapacity);
            pages.Add(selectedPage);
            return selectedPage.TryAllocate(vertexCount, indexCount, out handle);
        }

        private void Retire(ChunkMeshHandle handle)
        {
            retiredAllocations.Enqueue(new RetiredAllocation(
                handle, Time.frameCount + DeferredFreeFrames));
        }

        private TerrainMeshPage FindPage(int id)
        {
            for (int i = 0; i < pages.Count; i++)
                if (pages[i].Id == id)
                    return pages[i];
            return null;
        }

        private void ReleaseSurplusEmptyPages()
        {
            bool keptSpare = false;
            for (int i = pages.Count - 1; i >= 0; i--)
            {
                TerrainMeshPage page = pages[i];
                if (!page.IsEmpty)
                    continue;

                if (!keptSpare && page.VertexCapacity == standardVertexCapacity &&
                    page.IndexCapacity == standardIndexCapacity)
                {
                    keptSpare = true;
                    continue;
                }

                page.Dispose();
                pages.RemoveAt(i);
            }
        }

        private static Bounds CreateChunkCullingBounds(Vector3 worldOrigin)
        {
            const float cullingPadding = 1f;
            float chunkSize = Chunk.CHUNK_SIZE;
            return new Bounds(
                worldOrigin + Vector3.one * (chunkSize * 0.5f),
                Vector3.one * (chunkSize + cullingPadding * 2f));
        }

        private static int NextPowerOfTwo(int value)
        {
            int result = 1;
            while (result < value && result <= 1 << 29)
                result <<= 1;
            return Math.Max(result, value);
        }

        public readonly struct Statistics
        {
            public readonly int PageCount;
            public readonly int ResidentChunks;
            public readonly long VertexCapacity;
            public readonly long IndexCapacity;
            public readonly long FreeVertices;
            public readonly long FreeIndices;

            public Statistics(int pageCount, int residentChunks, long vertexCapacity,
                long indexCapacity, long freeVertices, long freeIndices)
            {
                PageCount = pageCount;
                ResidentChunks = residentChunks;
                VertexCapacity = vertexCapacity;
                IndexCapacity = indexCapacity;
                FreeVertices = freeVertices;
                FreeIndices = freeIndices;
            }
        }

        private readonly struct RetiredAllocation
        {
            public readonly ChunkMeshHandle Handle;
            public readonly int ReleaseFrame;

            public RetiredAllocation(ChunkMeshHandle handle, int releaseFrame)
            {
                Handle = handle;
                ReleaseFrame = releaseFrame;
            }
        }

        private sealed class TerrainMeshPage : IDisposable
        {
            private readonly ContiguousRangeAllocator vertexAllocator;
            private readonly ContiguousRangeAllocator indexAllocator;
            private readonly Stack<int> freeCommandSlots;
            private readonly GraphicsBuffer vertexBuffer;
            private readonly GraphicsBuffer vertexInstanceBuffer;
            private readonly GraphicsBuffer indexBuffer;
            private readonly GraphicsBuffer chunkDataBuffer;
            private readonly GraphicsBuffer sourceCommandBuffer;
            private readonly GraphicsBuffer commandBuffer;
            private readonly GraphicsBuffer hierarchyNodeBuffer;
            private readonly GraphicsBuffer hierarchyStateBuffer;
            private readonly GraphicsBuffer commandLeafBuffer;
            private readonly MaterialPropertyBlock materialProperties = new MaterialPropertyBlock();
            private readonly GraphicsBuffer.IndirectDrawIndexedArgs[] commandScratch =
                new GraphicsBuffer.IndirectDrawIndexedArgs[1];
            private int[] rebasedIndexScratch = Array.Empty<int>();
            private uint[] vertexInstanceScratch = Array.Empty<uint>();
            private readonly Dictionary<int, Vector3Int> coordinatesBySlot =
                new Dictionary<int, Vector3Int>();
            private readonly int[] hierarchyLevelOffsets = new int[4];
            private readonly int[] hierarchyLevelCounts = new int[4];
            private bool hierarchyDirty = true;
            private int liveAllocationCount;
            private int activeCommandCount;
            private int highestActiveCommand = -1;
            private Bounds worldBounds;
            private bool hasWorldBounds;

            public int Id { get; }
            public int VertexCapacity { get; }
            public int IndexCapacity { get; }
            public int FreeVertices => vertexAllocator.TotalFree;
            public int FreeIndices => indexAllocator.TotalFree;
            public int LargestFreeVertexRange => vertexAllocator.LargestFreeRange;
            public int LargestFreeIndexRange => indexAllocator.LargestFreeRange;
            public int FreeCommandSlots => freeCommandSlots.Count;
            public bool IsEmpty => liveAllocationCount == 0;

            public TerrainMeshPage(int id, int vertexCapacity, int indexCapacity, int commandCapacity)
            {
                Id = id;
                VertexCapacity = vertexCapacity;
                IndexCapacity = indexCapacity;
                vertexAllocator = new ContiguousRangeAllocator(vertexCapacity);
                indexAllocator = new ContiguousRangeAllocator(indexCapacity);
                freeCommandSlots = new Stack<int>(commandCapacity);
                for (int i = commandCapacity - 1; i >= 0; i--)
                    freeCommandSlots.Push(i);

                vertexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                    vertexCapacity, MeshUtilityCustom.ChunkVertexStride);
                vertexInstanceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                    vertexCapacity, sizeof(uint));
                indexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Index,
                    indexCapacity, sizeof(int));
                chunkDataBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                    commandCapacity, Marshal.SizeOf<ChunkGpuData>());
                sourceCommandBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                    commandCapacity, GraphicsBuffer.IndirectDrawIndexedArgs.size);
                commandBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw,
                    commandCapacity, GraphicsBuffer.IndirectDrawIndexedArgs.size);
                hierarchyNodeBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                    commandCapacity * 4, Marshal.SizeOf<HierarchyNodeGpuData>());
                hierarchyStateBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                    commandCapacity * 4, sizeof(uint));
                commandLeafBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                    commandCapacity, sizeof(int));

                // GraphicsBuffer contents are undefined after creation. Every unused
                // command must explicitly start with zero instances.
                var emptyCommands = new GraphicsBuffer.IndirectDrawIndexedArgs[commandCapacity];
                sourceCommandBuffer.SetData(emptyCommands);
                commandBuffer.SetData(emptyCommands);
            }

            public bool TryAllocate(int vertexCount, int indexCount, out ChunkMeshHandle handle)
            {
                handle = default;
                if (freeCommandSlots.Count == 0 ||
                    !vertexAllocator.TryAllocate(vertexCount, out var vertices))
                    return false;

                if (!indexAllocator.TryAllocate(indexCount, out var indices))
                {
                    vertexAllocator.Free(vertices);
                    return false;
                }

                handle = new ChunkMeshHandle
                {
                    PageId = Id,
                    VertexRange = vertices,
                    IndexRange = indices,
                    CommandSlot = freeCommandSlots.Pop()
                };
                liveAllocationCount++;
                return true;
            }

            public void Upload(ChunkMeshHandle handle,
                MeshUtilityCustom.ChunkMeshUploadData meshData, Vector3 worldOrigin)
            {
                vertexBuffer.SetData(meshData.vertices, 0, handle.VertexRange.Offset,
                    handle.VertexRange.Length);

                EnsureUploadScratchCapacity(handle.VertexRange.Length, handle.IndexRange.Length);
                for (int i = 0; i < handle.VertexRange.Length; i++)
                    vertexInstanceScratch[i] = (uint)handle.CommandSlot;
                vertexInstanceBuffer.SetData(vertexInstanceScratch, 0,
                    handle.VertexRange.Offset, handle.VertexRange.Length);

                // Store absolute page vertex indices. This avoids relying on the
                // graphics backend to apply baseVertexIndex consistently to
                // SV_VertexID for multi-command indirect draws.
                for (int i = 0; i < handle.IndexRange.Length; i++)
                    rebasedIndexScratch[i] = meshData.indices[i] + handle.VertexRange.Offset;
                indexBuffer.SetData(rebasedIndexScratch, 0, handle.IndexRange.Offset,
                    handle.IndexRange.Length);

                ChunkGpuData[] data =
                {
                    new ChunkGpuData(worldOrigin, handle.Bounds)
                };
                chunkDataBuffer.SetData(data, 0, handle.CommandSlot, 1);

                commandScratch[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
                {
                    indexCountPerInstance = (uint)handle.IndexRange.Length,
                    instanceCount = 1,
                    startIndex = (uint)handle.IndexRange.Offset,
                    baseVertexIndex = 0,
                    startInstance = 0
                };
                sourceCommandBuffer.SetData(commandScratch, 0, handle.CommandSlot, 1);
                commandBuffer.SetData(commandScratch, 0, handle.CommandSlot, 1);
                coordinatesBySlot[handle.CommandSlot] = handle.Coordinate;
                hierarchyDirty = true;
                activeCommandCount++;
                highestActiveCommand = Math.Max(highestActiveCommand, handle.CommandSlot);

                if (!hasWorldBounds)
                {
                    worldBounds = handle.Bounds;
                    hasWorldBounds = true;
                }
                else
                {
                    worldBounds.Encapsulate(handle.Bounds);
                }
            }

            public void DisableCommand(int commandSlot)
            {
                coordinatesBySlot.Remove(commandSlot);
                hierarchyDirty = true;
                commandScratch[0] = default;
                sourceCommandBuffer.SetData(commandScratch, 0, commandSlot, 1);
                commandBuffer.SetData(commandScratch, 0, commandSlot, 1);
                activeCommandCount = Math.Max(0, activeCommandCount - 1);
                // Keeping a conservative high-water mark avoids scanning all live
                // allocations on every removal. Zeroed holes are skipped by the GPU.
            }

            public void Render(Material material, ComputeShader cullingShader,
                int hierarchyKernel, int cullingKernel, Camera camera, Camera previewCamera,
                Vector4[] frustumPlanes)
            {
                if (activeCommandCount == 0 || highestActiveCommand < 0 || !hasWorldBounds)
                    return;

                CullCommands(cullingShader, hierarchyKernel, cullingKernel, frustumPlanes);

                materialProperties.Clear();
                materialProperties.SetBuffer("_Vertices", vertexBuffer);
                materialProperties.SetBuffer("_VertexInstance", vertexInstanceBuffer);
                materialProperties.SetBuffer("_ChunkData", chunkDataBuffer);
                // Bind on the material as well as the per-draw property block. Some
                // D3D12 paths validate required SRVs before applying RenderParams'
                // MaterialPropertyBlock, which otherwise causes the draw to be skipped.
                material.SetBuffer("_Vertices", vertexBuffer);
                material.SetBuffer("_VertexInstance", vertexInstanceBuffer);
                material.SetBuffer("_ChunkData", chunkDataBuffer);

                SubmitDraw(material, camera);
                if (previewCamera != null && previewCamera != camera)
                    SubmitDraw(material, previewCamera);
            }

            private void SubmitDraw(Material material, Camera targetCamera)
            {
                RenderParams renderParams = new RenderParams(material)
                {
                    matProps = materialProperties,
                    worldBounds = worldBounds,
                    camera = targetCamera,
                    // This procedural shader does not have a ShadowCaster pass yet.
                    // Requesting one can schedule another draw without the page SRVs.
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = true
                };
                Graphics.RenderPrimitivesIndexedIndirect(renderParams, MeshTopology.Triangles,
                    indexBuffer, commandBuffer, highestActiveCommand + 1);

            }
            
            public void Free(ChunkMeshHandle handle)
            {
                vertexAllocator.Free(handle.VertexRange);
                indexAllocator.Free(handle.IndexRange);
                freeCommandSlots.Push(handle.CommandSlot);
                liveAllocationCount--;
            }

            public void Dispose()
            {
                vertexBuffer?.Dispose();
                vertexInstanceBuffer?.Dispose();
                indexBuffer?.Dispose();
                chunkDataBuffer?.Dispose();
                sourceCommandBuffer?.Dispose();
                commandBuffer?.Dispose();
                hierarchyNodeBuffer?.Dispose();
                hierarchyStateBuffer?.Dispose();
                commandLeafBuffer?.Dispose();
            }
            
            private void CullCommands(ComputeShader shader, int hierarchyKernel, int chunkKernel,
                Vector4[] frustumPlanes)
            {
                const int threadGroupSize = 64;
                
                if (hierarchyDirty)
                    RebuildHierarchy();

                shader.SetVectorArray("_FrustumPlanes", frustumPlanes);
                shader.SetBuffer(hierarchyKernel, "_HierarchyNodes", hierarchyNodeBuffer);
                shader.SetBuffer(hierarchyKernel, "_HierarchyStates", hierarchyStateBuffer);
                for (int level = 0; level < hierarchyLevelOffsets.Length; level++)
                {
                    int nodeCount = hierarchyLevelCounts[level];
                    if (nodeCount == 0)
                        continue;
                    shader.SetInt("_NodeOffset", hierarchyLevelOffsets[level]);
                    shader.SetInt("_NodeCount", nodeCount);
                    shader.Dispatch(hierarchyKernel,
                        (nodeCount + threadGroupSize - 1) / threadGroupSize, 1, 1);
                }
                
                int commandCount = highestActiveCommand + 1;
                shader.SetInt("_CommandCount", commandCount);
                shader.SetBuffer(chunkKernel, "_ChunkData", chunkDataBuffer);
                shader.SetBuffer(chunkKernel, "_SourceCommands", sourceCommandBuffer);
                shader.SetBuffer(chunkKernel, "_VisibleCommands", commandBuffer);
                shader.SetBuffer(chunkKernel, "_HierarchyStates", hierarchyStateBuffer);
                shader.SetBuffer(chunkKernel, "_CommandLeafNodes", commandLeafBuffer);
                shader.Dispatch(chunkKernel,
                    (commandCount + threadGroupSize - 1) / threadGroupSize, 1, 1);
            }
            
            private void RebuildHierarchy()
            {
                int[] sizes = { 32, 16, 8, 4 };
                var keysByLevel = new HashSet<HierarchyNodeKey>[sizes.Length];
                for (int level = 0; level < sizes.Length; level++)
                    keysByLevel[level] = new HashSet<HierarchyNodeKey>();

                foreach (Vector3Int coordinate in coordinatesBySlot.Values)
                {
                    for (int level = 0; level < sizes.Length; level++)
                        keysByLevel[level].Add(new HierarchyNodeKey(
                            AlignDown(coordinate, sizes[level]), sizes[level]));
                }

                int nodeCount = 0;
                for (int level = 0; level < sizes.Length; level++)
                {
                    hierarchyLevelOffsets[level] = nodeCount;
                    hierarchyLevelCounts[level] = keysByLevel[level].Count;
                    nodeCount += keysByLevel[level].Count;
                }

                var nodes = new HierarchyNodeGpuData[nodeCount];
                var indices = new Dictionary<HierarchyNodeKey, int>(nodeCount);
                int writeIndex = 0;
                for (int level = 0; level < sizes.Length; level++)
                {
                    foreach (HierarchyNodeKey key in keysByLevel[level])
                    {
                        int parentIndex = -1;
                        if (level > 0)
                        {
                            var parentKey = new HierarchyNodeKey(
                                AlignDown(key.Origin, sizes[level - 1]), sizes[level - 1]);
                            parentIndex = indices[parentKey];
                        }

                        nodes[writeIndex] = new HierarchyNodeGpuData(key.Origin, key.Size,
                            parentIndex);
                        indices.Add(key, writeIndex++);
                    }
                }

                int[] commandLeaves = new int[freeCommandSlots.Count + liveAllocationCount];
                foreach (KeyValuePair<int, Vector3Int> entry in coordinatesBySlot)
                {
                    var leafKey = new HierarchyNodeKey(AlignDown(entry.Value, 4), 4);
                    commandLeaves[entry.Key] = indices[leafKey];
                }

                if (nodeCount > 0)
                    hierarchyNodeBuffer.SetData(nodes);
                commandLeafBuffer.SetData(commandLeaves);
                hierarchyDirty = false;
            }

            private static Vector3Int AlignDown(Vector3Int coordinate, int size)
            {
                return new Vector3Int(FloorToMultiple(coordinate.x, size),
                    FloorToMultiple(coordinate.y, size), FloorToMultiple(coordinate.z, size));
            }

            private static int FloorToMultiple(int value, int size)
            {
                int remainder = value % size;
                return value - (remainder < 0 ? remainder + size : remainder);
            }
            

            private void EnsureUploadScratchCapacity(int vertexCount, int indexCount)
            {
                if (vertexInstanceScratch.Length < vertexCount)
                    vertexInstanceScratch = new uint[Mathf.NextPowerOfTwo(vertexCount)];
                if (rebasedIndexScratch.Length < indexCount)
                    rebasedIndexScratch = new int[Mathf.NextPowerOfTwo(indexCount)];
            }
            
            private readonly struct HierarchyNodeKey : IEquatable<HierarchyNodeKey>
            {
                public readonly Vector3Int Origin;
                public readonly int Size;

                public HierarchyNodeKey(Vector3Int origin, int size)
                {
                    Origin = origin;
                    Size = size;
                }

                public bool Equals(HierarchyNodeKey other)
                {
                    return Origin == other.Origin && Size == other.Size;
                }

                public override bool Equals(object obj)
                {
                    return obj is HierarchyNodeKey other && Equals(other);
                }

                public override int GetHashCode()
                {
                    unchecked
                    {
                        return (Origin.GetHashCode() * 397) ^ Size;
                    }
                }
            }

        }

        private struct ChunkMeshHandle
        {
            public Vector3Int Coordinate;
            public int PageId;
            public ContiguousRangeAllocator.Allocation VertexRange;
            public ContiguousRangeAllocator.Allocation IndexRange;
            public int CommandSlot;
            public int Revision;
            public Bounds Bounds;
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct ChunkGpuData
        {
            private readonly Vector4 origin;
            private readonly Vector4 boundsCenter;
            private readonly Vector4 boundsExtents;

            public ChunkGpuData(Vector3 worldOrigin, Bounds bounds)
            {
                origin = new Vector4(worldOrigin.x, worldOrigin.y, worldOrigin.z, 0f);
                boundsCenter = new Vector4(bounds.center.x, bounds.center.y, bounds.center.z, 0f);
                boundsExtents = new Vector4(bounds.extents.x, bounds.extents.y, bounds.extents.z, 0f);
            }
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private readonly struct HierarchyNodeGpuData
        {
            private readonly Vector4 boundsCenter;
            private readonly Vector4 boundsExtents;
            private readonly Vector4 metadata;

            public HierarchyNodeGpuData(Vector3Int chunkOrigin, int size, int parentIndex)
            {
                float worldSize = size * Chunk.CHUNK_SIZE;
                Vector3 minimum = (Vector3)(chunkOrigin * Chunk.CHUNK_SIZE);
                Vector3 center = minimum + Vector3.one * (worldSize * 0.5f);
                const float padding = 1f;
                boundsCenter = new Vector4(center.x, center.y, center.z, 0f);
                boundsExtents = new Vector4(worldSize * 0.5f + padding,
                    worldSize * 0.5f + padding, worldSize * 0.5f + padding, 0f);
                metadata = new Vector4(parentIndex, 0f, 0f, 0f);
            }
        }

    }
}
