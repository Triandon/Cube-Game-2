using System;
using System.Globalization;
using Core.Block;
using UnityEngine;

namespace Core.Blocks
{
    public class PoleBlock : Block.Block
    {
        public override bool HasScheduledTick => true;

        private const string GrowTime = "grow_time";

        public PoleBlock(byte id, string name, int top, int side, int bottom, int front = -1) : base(id, name, top, side, bottom, front)
        {
            isTransparent = true;
            isStrechy = true;
            AddState(BlockStateKeys.DirectionalFacing, DirectionalFacing.Up);
            AddState(BlockStateKeys.WidthState, 0.5f.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        }

        public override void OnPlaced(Vector3Int position, BlockStateContainer state, Transform player, Vector3Int? placementFace)
        {
            base.OnPlaced(position, state, player, placementFace);
            
            if (state == null)
                return;
            
            state.SetState(BlockStateKeys.DirectionalFacing, DirectionalFacing.Up);
            state.SetState(BlockStateKeys.WidthState, 0.5f.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        }

        public override bool OnActivated(Vector3Int position, BlockStateContainer state, Block.Block block, Transform player)
        {
            Debug.Log("Width: " + state.GetState(BlockStateKeys.WidthState));
            Debug.Log("Width: " + state.GetState(GrowTime));
            
            return false;
        }

        public override void OnScheduledTick(Vector3Int position, float deltaTime, ChunkManager chunkManager)
        {
            if (chunkManager == null)
                return;

            float currentGrowTime = GetGrowTime(position, chunkManager);
            currentGrowTime += Mathf.Max(0f, deltaTime);

            Debug.Log("Growing the POLE");
            
            if (currentGrowTime >= 5f)
            {
                BlockStateContainer blockStateContainer = GetOrCreateStateContainer(position, chunkManager);
                string value = blockStateContainer.GetState(BlockStateKeys.WidthState);
                float width = float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ? Mathf.Max(0f, parsed)
                    : 0f;

                width += 0.1f;
                
                Debug.Log("The Pole grown to: " + width + " and had before: " + (width - 0.1f));
                
                blockStateContainer.SetState(BlockStateKeys.WidthState,width.ToString(CultureInfo.InvariantCulture));
                return;
            }
            
            SetGrowTime(position, chunkManager, currentGrowTime);
        }
        
        private static float GetGrowTime(Vector3Int position, ChunkManager chunkManager)
        {
            BlockStateContainer state = GetOrCreateStateContainer(position, chunkManager);
            if (state == null)
                return 0f;

            string value = state.GetState(GrowTime);
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

            state.SetState(GrowTime, Mathf.Max(0f, value).ToString(CultureInfo.InvariantCulture));
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