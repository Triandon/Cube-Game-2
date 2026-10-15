using System.Collections.Generic;
using System.Globalization;
using Core.Block;
using UnityEngine;

namespace Core.Blocks
{
    public class TreeLog : Block.Block
    {
        private const int GROWTIME = 3;
        private readonly Dictionary<Vector3Int, float> growList = new();
        
        public TreeLog(byte id, string name, int top, int side, int bottom, int front = -1) : base(id, name, top, side, bottom, front)
        {
            SetDefaultState(new BlockStateContainer().With(BlockStateKeys.WidthState, "0.1"));
            isTransparent = true;
            isStrechy = true;
            isCentered = true;
        }

        public override BlockStateContainer GetStateForPlacement(BlockPlacementContext context)
        {
            growList[context.Pos] = 0;
            return CreateDefaultState().With(BlockStateKeys.WidthState, "0.1");
        }

        public override bool HasInstantTick => true;

        public override void OnInstantTick(Vector3Int position, ChunkManager chunkManager)
        {
            if(!growList.ContainsKey(position))
                return;
            
            BlockStateContainer state = chunkManager.GetBlockStateAtWorldPos(position);
            
            string sWidth = state.GetState(BlockStateKeys.WidthState);
            float width = float.Parse(sWidth, CultureInfo.InvariantCulture);
            
            if (width >= 0.9999f)
                width = 1;

            if (width >= 1f)
            {
                chunkManager.SetBlockState(position, BlockStateKeys.WidthState, "1");
                if (chunkManager.GetBlockAtWorldPos(position + Vector3Int.up) == 0)
                {
                    chunkManager.SetBlockAtWorldPos(position + Vector3Int.up, 23);
                }
                growList.Remove(position);
                return;
            }
            
            growList.TryGetValue(position, out float growTime);
            growTime += Time.deltaTime;
            growList[position] = growTime;

            if (growTime >= GROWTIME)
            {
                growList[position] = 0;
                width = width + 0.1f;
            }
            
            chunkManager.SetBlockState(position,BlockStateKeys.WidthState, width.ToString(CultureInfo.InvariantCulture));
        }
    }
}
