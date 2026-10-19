using System.Collections.Generic;
using System.Globalization;
using Core.Block;
using UnityEngine;

namespace Core.Blocks
{
    public class TreeLog : Block.Block
    {
        private const int GROWTIME = 3;
        private const string GrowTimeState = "GrowTime";
        
        public TreeLog(byte id, string name, int top, int side, int bottom, int front = -1) : base(id, name, top, side, bottom, front)
        {
            SetDefaultState(new BlockStateContainer().With(BlockStateKeys.WidthState, "0.1").
                With(GrowTimeState, "0"));
            isTransparent = true;
            isStrechy = true;
            isCentered = true;
        }

        public override BlockStateContainer GetStateForPlacement(BlockPlacementContext context)
        {
            return CreateDefaultState().With(BlockStateKeys.WidthState, "0.1").
                    With(GrowTimeState, "0");
        }

        public override bool HasInstantTick => true;

        public override void OnInstantTick(Vector3Int position, ChunkManager chunkManager)
        {
            BlockStateContainer state = chunkManager.GetBlockStateAtWorldPos(position);
            
            string sWidth = state.GetState(BlockStateKeys.WidthState);
            float width = float.Parse(sWidth, CultureInfo.InvariantCulture);
            
            if (width >= 1)
                return;

            string sGrowTime = state.GetState(GrowTimeState);
            float growTime = float.Parse(sGrowTime, CultureInfo.InvariantCulture);
            
            if (width >= 0.9999f)
                width = 1;
            
            growTime += Time.deltaTime;
            
            if (growTime >= GROWTIME)
            {
                growTime = 0;
                width = Mathf.Min(1f, width + 0.1f);
                chunkManager.SetBlockState(position,BlockStateKeys.WidthState, width.ToString(CultureInfo.InvariantCulture));
            }

            if (width >= 0.9999f)
            {
                chunkManager.SetBlockState(position, BlockStateKeys.WidthState, "1");
                if (chunkManager.GetBlockAtWorldPos(position + Vector3Int.up) == 0)
                {
                    chunkManager.SetBlockAtWorldPos(position + Vector3Int.up, 23);
                }
            }
            
            state.SetState(GrowTimeState, growTime.ToString(CultureInfo.InvariantCulture));
        }
    }
}
