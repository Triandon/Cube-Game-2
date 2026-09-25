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
        private int nextPageId;
        private bool disposed;

        public int PageCount => pages.Count;
        public int ResidentChunkCount => allocations.Count;
        public bool CanRender => indirectMaterial != null;

        public PagedTerrainMeshStorage(int vertexCapacity, int indexCapacity, int commandCapacity,
            Material indirectMaterialTemplate)
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
            replacement.Bounds = TranslateBounds(meshData.bounds, worldOrigin);

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

        public void Render()
        {
            if (disposed || indirectMaterial == null)
                return;

            for (int i = 0; i < pages.Count; i++)
                pages[i].Render(indirectMaterial);
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

        private static Bounds TranslateBounds(Bounds localBounds, Vector3 origin)
        {
            localBounds.center += origin;
            return localBounds;
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
            private readonly GraphicsBuffer commandBuffer;
            private readonly MaterialPropertyBlock materialProperties = new MaterialPropertyBlock();
            private readonly GraphicsBuffer.IndirectDrawIndexedArgs[] commandScratch =
                new GraphicsBuffer.IndirectDrawIndexedArgs[1];
            private int[] rebasedIndexScratch = Array.Empty<int>();
            private uint[] vertexInstanceScratch = Array.Empty<uint>();
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
                commandBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments,
                    commandCapacity, GraphicsBuffer.IndirectDrawIndexedArgs.size);

                // GraphicsBuffer contents are undefined after creation. Every unused
                // command must explicitly start with zero instances.
                commandBuffer.SetData(new GraphicsBuffer.IndirectDrawIndexedArgs[commandCapacity]);
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
                commandBuffer.SetData(commandScratch, 0, handle.CommandSlot, 1);
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
                commandScratch[0] = default;
                commandBuffer.SetData(commandScratch, 0, commandSlot, 1);
                activeCommandCount = Math.Max(0, activeCommandCount - 1);
                // Keeping a conservative high-water mark avoids scanning all live
                // allocations on every removal. Zeroed holes are skipped by the GPU.
            }

            public void Render(Material material)
            {
                if (activeCommandCount == 0 || highestActiveCommand < 0 || !hasWorldBounds)
                    return;

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

                RenderParams renderParams = new RenderParams(material)
                {
                    matProps = materialProperties,
                    worldBounds = worldBounds,
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
                commandBuffer?.Dispose();
            }

            private void EnsureUploadScratchCapacity(int vertexCount, int indexCount)
            {
                if (vertexInstanceScratch.Length < vertexCount)
                    vertexInstanceScratch = new uint[Mathf.NextPowerOfTwo(vertexCount)];
                if (rebasedIndexScratch.Length < indexCount)
                    rebasedIndexScratch = new int[Mathf.NextPowerOfTwo(indexCount)];
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
    }
}
