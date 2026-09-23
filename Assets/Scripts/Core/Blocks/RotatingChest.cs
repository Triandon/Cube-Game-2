using System.Globalization;
using Core.Block;
using UnityEngine;

namespace Core.Blocks
{
    public class RotatingChest : Block.Block
    {
        public RotatingChest(byte id, string name, int top, int side, int bottom, int front = -1) : base(id, name, top, side, bottom, front)
        {
            SetDefaultState(new BlockStateContainer().With(BlockStateKeys.DirectionalFacing,
                DirectionalFacing.North).With("rotation_timer", "0"));
        }
        
        public override BlockStateContainer GetStateForPlacement(BlockPlacementContext context)
        {
            return CreateDefaultState().With(BlockStateKeys.DirectionalFacing,
                GetHorizontalFacingTowardPlayer(context.Player));
        }

        public override bool HasInstantTick => true;

        public override void OnInstantTick(Vector3Int position, ChunkManager chunkManager)
        {
            BlockStateContainer state = chunkManager.GetBlockStateAtWorldPos(position);
            
            if (state == null)
                return;

            string timerValue = state.GetState("rotation_timer");
            float timer = float.TryParse(timerValue,
                NumberStyles.Float, CultureInfo.InvariantCulture,
                out float parsedTimer) ? parsedTimer : 0f;

            timer += Time.deltaTime;

            if (timer < 0.25f)
            {
                state.SetState("rotation_timer", timer.ToString(CultureInfo.InvariantCulture));
                
                return;
            }

            timer = 0;
            state.SetState("rotation_timer", timer.ToString(CultureInfo.InvariantCulture));

            string currentFacing = state.GetState(BlockStateKeys.DirectionalFacing);
            
            string nextFacing = currentFacing switch
            {
                "north" => "east",
                "east"  => "south",
                "south" => "west",
                "west"  => "north",
                _       => "north"
            };

            chunkManager.SetBlockState(position, BlockStateKeys.DirectionalFacing, nextFacing);
        }
        
    }
}
