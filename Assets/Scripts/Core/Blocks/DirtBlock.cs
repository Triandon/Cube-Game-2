using System.Globalization;
using Core.Block;
using UnityEngine;

namespace Core.Blocks
{
    public class DirtBlock : Block.Block
    {
        public override bool HasScheduledTick => true;

        private const string GrowTimeState = "grow_time";
        private const float TotalGrowTime = 32f;

        private static readonly Vector3Int[] HorizontalDirections =
        {
            Vector3Int.right,
            Vector3Int.left,
            Vector3Int.forward,
            Vector3Int.back
        };

        public DirtBlock(byte id, string name, int top, int side, int bottom, int front = -1) : base(id, name, top, side, bottom, front)
        {
        }

        public override void OnScheduledTick(Vector3Int position, float deltaTime, ChunkManager chunkManager)
        {
            if (chunkManager == null)
                return;

            if (!HasHorizontalGrassNeighbors(position, chunkManager))
            {
                SetGrowTime(position, chunkManager, 0);
                return;
            }

            float currentGrowTime = GetGrowTime(position, chunkManager);
            currentGrowTime += Mathf.Max(0f, deltaTime);

            if (currentGrowTime >= TotalGrowTime)
            {
                chunkManager.SetBlockAtWorldPos(position, BlockDataBase.GrassBlock.id);
                return;
            }
            
            SetGrowTime(position, chunkManager, currentGrowTime);
        }

        private static bool HasHorizontalGrassNeighbors(Vector3Int pos, ChunkManager cm)
        {
            for (int i = 0; i < HorizontalDirections.Length; i++)
            {
                Vector3Int checkPos = pos + HorizontalDirections[i];
                byte neighborId = cm.GetBlockAtWorldPos(checkPos);
                if (neighborId == BlockDataBase.GrassBlock.id)
                    return true;
            }

            return false;
        }
        
        private static float GetGrowTime(Vector3Int position, ChunkManager chunkManager)
        {
            BlockStateContainer state = GetOrCreateStateContainer(position, chunkManager);
            if (state == null)
                return 0f;

            string value = state.GetState(GrowTimeState);
            if (string.IsNullOrEmpty(value))
                return 0f;

            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
                ? Mathf.Max(0f, parsed)
                : 0f;
        }
        
        private static void SetGrowTime(Vector3Int position, ChunkManager chunkManager, float value)
        {
            BlockStateContainer state = GetOrCreateStateContainer(position, chunkManager);
            if (state == null)
                return;

            state.SetState(GrowTimeState, Mathf.Max(0f, value).ToString(CultureInfo.InvariantCulture));
        }

        
        private static BlockStateContainer GetOrCreateStateContainer(Vector3Int position, ChunkManager chunkManager)
        {
            Chunk chunk = chunkManager.GetChunkFromWorldPos(position);
            if (chunk == null)
                return null;

            Vector3Int local = chunk.WorldToLocal(position);
            if (local.x < 0 || local.x >= Chunk.CHUNK_SIZE ||
                local.y < 0 || local.y >= Chunk.CHUNK_SIZE ||
                local.z < 0 || local.z >= Chunk.CHUNK_SIZE)
            {
                return null;
            }

            BlockStateContainer state = chunk.states[local.x, local.y, local.z];
            if (state != null)
                return state;

            state = new BlockStateContainer();
            chunk.states[local.x, local.y, local.z] = state;
            return state;
        }


    }
}