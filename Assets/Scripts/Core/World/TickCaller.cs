using System;
using System.Collections.Generic;
using Core.Block;
using UnityEngine;

namespace Core
{
    public class TickCaller : MonoBehaviour
    {
        [Header("Scheduled Tick Tuning")]
        [SerializeField] private int minimumScheduledCallsPerFrame = 24;
        [SerializeField] private int callsPerChunkBuildBudget = 4;
        // Calls in the world = chunkmanager.chunksPrFrame * callsPrChunkBuild
        // Additionaly a min value at minScheduledCallsPrFrame.
        // ChunkPrFrame = 3, callsPrBuild = 8  => budget=3*8 = 24
        // min = 32. The budget is 32.
        
        [Header("Random Tick Tuning")]
        [SerializeField] private float randomTickIntervalSeconds = 0.2f;
        [SerializeField] private int randomTicksPerInterval = 3;

        private readonly HashSet<Vector3Int> instantTickBlocks = new HashSet<Vector3Int>();
        private readonly HashSet<Vector3Int> scheduledTickBlocks = new HashSet<Vector3Int>();
        private readonly HashSet<Vector3Int> randomTickBlocks = new HashSet<Vector3Int>();

        private readonly Queue<Vector3Int> scheduledQueue = new Queue<Vector3Int>();
        private readonly Dictionary<Vector3Int, float> lastScheduledTickTime = new Dictionary<Vector3Int, float>();

        private readonly Dictionary<Vector3Int, HashSet<Vector3Int>> chunkTickPositions =
            new Dictionary<Vector3Int, HashSet<Vector3Int>>();
        
        private readonly List<Vector3Int> randomTickBuffer = new List<Vector3Int>();
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

            if (scheduledLocals == null || scheduledLocals.Count == 0)
                return;

            for (int i = 0; i < scheduledLocals.Count; i++)
            {
                Vector3Int worldPos = origin + scheduledLocals[i];
                if (!scheduledTickBlocks.Add(worldPos))
                    continue;

                chunkPositions.Add(worldPos);
                scheduledQueue.Enqueue(worldPos);
                lastScheduledTickTime[worldPos] = Time.time;
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
            if (block.HasInstantTick)
            {
                instantTickBlocks.Add(worldPos);
                TrackChunkPosition(worldPos);
            }

            if (block.HasScheduledTick && scheduledTickBlocks.Add(worldPos))
            {
                TrackChunkPosition(worldPos);
                scheduledQueue.Enqueue(worldPos);
                lastScheduledTickTime[worldPos] = Time.time;
            }

            if (block.HasRandomTick)
            {
                randomTickBlocks.Add(worldPos);
                TrackChunkPosition(worldPos);
            }
        }

        private void UnregisterPosition(Vector3Int worldPos)
        {
            instantTickBlocks.Remove(worldPos);
            scheduledTickBlocks.Remove(worldPos);
            randomTickBlocks.Remove(worldPos);
            lastScheduledTickTime.Remove(worldPos);

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
                lastScheduledTickTime.Remove(worldPos);
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

            foreach (Vector3Int worldPos in instantTickBlocks)
            {
                byte id = chunkManager.GetBlockAtWorldPos(worldPos);
                if (id == 0)
                    continue;

                Block.Block block = BlockRegistry.GetBlock(id);
                if (block == null || !block.HasInstantTick)
                    continue;

                block.OnInstantTick(worldPos, chunkManager);
            }
        }

        private void RunScheduledTicks()
        {
            if (scheduledQueue.Count == 0)
                return;

            int dynamicBudget = minimumScheduledCallsPerFrame;
            if (chunkManager != null)
            {
                dynamicBudget = Math.Max(minimumScheduledCallsPerFrame,
                    chunkManager.chunksPerFrame * callsPerChunkBuildBudget);
            }

            int calls = Math.Min(dynamicBudget, scheduledQueue.Count);

            for (int i = 0; i < calls; i++)
            {
                Vector3Int worldPos = scheduledQueue.Dequeue();

                if (!scheduledTickBlocks.Contains(worldPos))
                    continue;

                byte id = chunkManager.GetBlockAtWorldPos(worldPos);
                Block.Block block = BlockRegistry.GetBlock(id);

                if (id == 0 || block == null || !block.HasScheduledTick)
                {
                    scheduledTickBlocks.Remove(worldPos);
                    lastScheduledTickTime.Remove(worldPos);
                    continue;
                }

                float now = Time.time;
                if (!lastScheduledTickTime.TryGetValue(worldPos, out float lastTime))
                    lastTime = now;

                float delta = Mathf.Max(0f, now - lastTime);
                block.OnScheduledTick(worldPos, delta, chunkManager);

                lastScheduledTickTime[worldPos] = now;
                scheduledQueue.Enqueue(worldPos);
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

                byte id = chunkManager.GetBlockAtWorldPos(worldPos);
                if (id == 0)
                    continue;

                Block.Block block = BlockRegistry.GetBlock(id);
                if (block == null || !block.HasRandomTick)
                    continue;

                block.OnRandomTick(worldPos, chunkManager);
            }
        }
    }
}
