using System;
using System.Collections.Generic;
using Core.Block;
using Core.TileEntities;
using UnityEngine;

namespace Core
{
    public class TickCaller : MonoBehaviour
    {
        [Header("Scheduled Tick Tuning")]
        [SerializeField] private int minimumScheduledCallsPerFrame = 16;
        
        [Header("Random Tick Tuning")]
        [SerializeField] private float randomTickIntervalSeconds = 0.2f;
        [SerializeField] private int randomTicksPerInterval = 3;

        private readonly HashSet<Vector3Int> instantTickBlocks = new HashSet<Vector3Int>();
        private readonly Dictionary<Vector3Int, ScheduledTick> scheduledTickBlocks =
            new Dictionary<Vector3Int, ScheduledTick>();
        private readonly HashSet<Vector3Int> randomTickBlocks = new HashSet<Vector3Int>();

        // The registration identity prevents stale queue entries from ticking a replacement.
        private sealed class ScheduledTick
        {
            public Vector3Int Position;
            public float LastTime;
        }

        private readonly Queue<ScheduledTick> scheduledQueue = new Queue<ScheduledTick>();

        private readonly Dictionary<Vector3Int, HashSet<Vector3Int>> chunkTickPositions =
            new Dictionary<Vector3Int, HashSet<Vector3Int>>();
        
        private readonly List<Vector3Int> randomTickBuffer = new List<Vector3Int>();
        private readonly List<Vector3Int> instantTickBuffer = new List<Vector3Int>();
        private readonly System.Random random = new System.Random();

        private ChunkManager chunkManager;
        private float randomTickTimer;

        public void Init(ChunkManager manager)
        {
            chunkManager = manager;
        }

        public void Tick(float frameDelta)
        {
            if (chunkManager == null)
            {
                chunkManager = FindAnyObjectByType<ChunkManager>();
                if (chunkManager == null)
                    return;
            }

            RunInstantTicks();
            RunScheduledTicks();
            RunRandomTicks(frameDelta);
        }

        public void RegisterChunk(Chunk chunk, List<Vector3Int> instantLocals,
            List<Vector3Int> scheduledLocals, List<Vector3Int> randomLocals)
        {
            if (chunk == null)
                return;

            ClearChunkRegistrations(chunk.coord);

            HashSet<Vector3Int> chunkPositions = GetOrCreateChunkSet(chunk.coord);
            
            Vector3Int origin = chunk.coord * Chunk.CHUNK_SIZE;
            
            RegisterTickPositions(origin, instantLocals, instantTickBlocks, chunkPositions);
            RegisterTickPositions(origin, randomLocals, randomTickBlocks, chunkPositions);

            if (scheduledLocals != null)
            {
                for (int i = 0; i < scheduledLocals.Count; i++)
                    RegisterScheduledPosition(origin + scheduledLocals[i]);
            }
            
            // Tile tick flags belong to per-position instances, created on the main thread.
            foreach (TileEntity tile in chunk.TileEntities.Values)
            {
                Block.Block block = BlockRegistry.GetBlock(tile.BlockId);
                if (block != null)
                    RegisterPositionForBlock(tile.Position, block);
            }
        }

        public void UnregisterChunk(Chunk chunk)
        {
            if (chunk == null)
                return;

            ClearChunkRegistrations(chunk.coord);
        }
        
        private static void RegisterTickPositions(Vector3Int origin, List<Vector3Int> locals,
            HashSet<Vector3Int> target, HashSet<Vector3Int> chunkPos)
        {
            if (locals == null || locals.Count == 0)
                return;

            for (int i = 0; i < locals.Count; i++)
            {
                Vector3Int worldPos = origin + locals[i];
                if (!target.Add(worldPos))
                    continue;

                chunkPos.Add(worldPos);
            }
        }

        public void OnBlockChanged(Vector3Int worldPos, byte previousId, byte newId)
        {
            if (previousId == newId)
                return;

            UnregisterPosition(worldPos);

            if (newId == 0)
                return;

            Block.Block block = BlockRegistry.GetBlock(newId);
            if (block == null)
                return;

            RegisterPositionForBlock(worldPos, block);
        }

        private void RegisterPositionForBlock(Vector3Int worldPos, Block.Block block)
        {
            TileEntity tile = chunkManager.GetTileEntity(worldPos);
            if (block.HasInstantTick || (tile?.HasInstantTick ?? false))
            {
                instantTickBlocks.Add(worldPos);
                TrackChunkPosition(worldPos);
            }

            if (block.HasScheduledTick || (tile?.HasScheduledTick ?? false))
                RegisterScheduledPosition(worldPos);

            if (block.HasRandomTick || (tile?.HasRandomTick ?? false))
            {
                randomTickBlocks.Add(worldPos);
                TrackChunkPosition(worldPos);
            }
        }
        
        private void RegisterScheduledPosition(Vector3Int worldPos)
        {
            if (scheduledTickBlocks.ContainsKey(worldPos))
                return;

            var entry = new ScheduledTick { Position = worldPos, LastTime = Time.time };
            scheduledTickBlocks.Add(worldPos, entry);
            scheduledQueue.Enqueue(entry);
            TrackChunkPosition(worldPos);
        }

        private void UnregisterPosition(Vector3Int worldPos)
        {
            instantTickBlocks.Remove(worldPos);
            scheduledTickBlocks.Remove(worldPos);
            randomTickBlocks.Remove(worldPos);

            Vector3Int chunkCoord = WorldToChunkCoord(worldPos);
            if (!chunkTickPositions.TryGetValue(chunkCoord, out HashSet<Vector3Int> chunkSet))
                return;

            chunkSet.Remove(worldPos);
            if (chunkSet.Count == 0)
            {
                chunkTickPositions.Remove(chunkCoord);
            }
        }
        
        private void ClearChunkRegistrations(Vector3Int chunkCoord)
        {
            if (!chunkTickPositions.TryGetValue(chunkCoord, out HashSet<Vector3Int> chunkSet))
                return;

            foreach (Vector3Int worldPos in chunkSet)
            {
                instantTickBlocks.Remove(worldPos);
                scheduledTickBlocks.Remove(worldPos);
                randomTickBlocks.Remove(worldPos);
            }

            chunkTickPositions.Remove(chunkCoord);
        }

        private HashSet<Vector3Int> GetOrCreateChunkSet(Vector3Int chunkCoord)
        {
            if (chunkTickPositions.TryGetValue(chunkCoord, out HashSet<Vector3Int> existing))
                return existing;

            var created = new HashSet<Vector3Int>();
            chunkTickPositions.Add(chunkCoord, created);
            return created;
        }

        private void TrackChunkPosition(Vector3Int worldPos)
        {
            Vector3Int chunkCoord = WorldToChunkCoord(worldPos);
            HashSet<Vector3Int> chunkSet = GetOrCreateChunkSet(chunkCoord);
            chunkSet.Add(worldPos);
        }

        private static Vector3Int WorldToChunkCoord(Vector3Int worldPos)
        {
            return new Vector3Int(
                Mathf.FloorToInt((float)worldPos.x / Chunk.CHUNK_SIZE),
                Mathf.FloorToInt((float)worldPos.y / Chunk.CHUNK_SIZE),
                Mathf.FloorToInt((float)worldPos.z / Chunk.CHUNK_SIZE));
        }

        private void RunInstantTicks()
        {
            if (instantTickBlocks.Count == 0)
                return;
            
            // Callbacks may place or remove blocks, changing the registrations.
            instantTickBuffer.Clear();
            instantTickBuffer.AddRange(instantTickBlocks);

            foreach (Vector3Int worldPos in instantTickBuffer)
            {
                if (!instantTickBlocks.Contains(worldPos))
                    continue;
                
                byte id = chunkManager.GetBlockAtWorldPos(worldPos);
                if (id == 0)
                    continue;

                Block.Block block = BlockRegistry.GetBlock(id);
                if (block == null)
                    continue;

                TileEntity tile = chunkManager.GetTileEntity(worldPos);
                if (block.HasInstantTick)
                    block.OnInstantTick(worldPos, chunkManager);
                
                if (IsCurrentTile(tile) && tile.HasInstantTick)
                    tile.OnInstantTick();
            }
        }

        private void RunScheduledTicks()
        {
            if (scheduledQueue.Count == 0)
                return;

            int calls = Math.Min(minimumScheduledCallsPerFrame, scheduledQueue.Count);

            for (int i = 0; i < calls; i++)
            {
                ScheduledTick entry = scheduledQueue.Dequeue();
                Vector3Int worldPos = entry.Position;

                if (!IsCurrentSchedule(entry))
                    continue;

                byte id = chunkManager.GetBlockAtWorldPos(worldPos);
                Block.Block block = BlockRegistry.GetBlock(id);
                TileEntity tile = chunkManager.GetTileEntity(worldPos);

                if (id == 0 || block == null ||
                    (!block.HasScheduledTick && !(tile?.HasScheduledTick ?? false)))
                {
                    scheduledTickBlocks.Remove(worldPos);
                    continue;
                }

                float now = Time.time;
                float delta = Mathf.Max(0f, now - entry.LastTime);
                if (block.HasScheduledTick)
                    block.OnScheduledTick(worldPos, delta, chunkManager);
                if (IsCurrentTile(tile) && tile.HasScheduledTick)
                    tile.OnScheduledTick(delta);
                
                if (IsCurrentSchedule(entry))
                {
                    entry.LastTime = now;
                    scheduledQueue.Enqueue(entry);
                }
            }
        }

        private void RunRandomTicks(float frameDelta)
        {
            if (randomTickBlocks.Count == 0)
                return;

            randomTickTimer += frameDelta;
            if (randomTickTimer < randomTickIntervalSeconds)
                return;

            randomTickTimer = 0f;

            randomTickBuffer.Clear();
            randomTickBuffer.AddRange(randomTickBlocks);

            int count = Math.Min(randomTicksPerInterval, randomTickBuffer.Count);
            for (int i = 0; i < count; i++)
            {
                int index = random.Next(0, randomTickBuffer.Count);
                Vector3Int worldPos = randomTickBuffer[index];
                randomTickBuffer[index] = randomTickBuffer[randomTickBuffer.Count - 1];
                randomTickBuffer.RemoveAt(randomTickBuffer.Count - 1);
                
                if (!randomTickBlocks.Contains(worldPos))
                    continue;

                byte id = chunkManager.GetBlockAtWorldPos(worldPos);
                if (id == 0)
                    continue;

                Block.Block block = BlockRegistry.GetBlock(id);
                if (block == null)
                    continue;

                TileEntity tile = chunkManager.GetTileEntity(worldPos);
                if (block.HasRandomTick)
                    block.OnRandomTick(worldPos, chunkManager);
                if (IsCurrentTile(tile) && tile.HasRandomTick)
                    tile.OnRandomTick();
            }
        }
        
        private bool IsCurrentTile(TileEntity tile)
        {
            return tile != null && !tile.IsRemoved &&
                   ReferenceEquals(chunkManager.GetTileEntity(tile.Position), tile);
        }

        private bool IsCurrentSchedule(ScheduledTick entry)
        {
            return scheduledTickBlocks.TryGetValue(entry.Position, out ScheduledTick current) &&
                   ReferenceEquals(current, entry);
        }
    }
}
