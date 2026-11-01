using System;
using System.Collections.Generic;
using System.Globalization;
using Core.Block;
using UnityEngine;

namespace Core.TileEntities
{
    public class TreeTrunkTile : TileEntity
    {
        // Growth tuning (width/height are fractions of one voxel).
        public const float InitialWidth = 0.1f;
        public const float InitialHeight = 0.1f;
        public const float FullHeightWidth = 0.2f;
        public const float SupportDifference = 0.2f;
        public const float MaximumWidth = 1f;
        public const float GrowthIntervalSeconds = 0.75f;
        public const float WidthPerGrowthStep = 0.025f;
        public const float LeafDelaySeconds = 3f;
        
        // Reserved inputs for a later environmental model. They do not gate growth.
        public const string SunlightState = "TreeSunlight";
        public const string HumidityState = "TreeHumidity";
        public const string TemperatureState = "TreeTemperature";

        private const string SegmentsState = "TreeSegments";
        private const string PartIdState = "TreePartId";
        private const string TipLeafState = "TreeTipLeaf";
        private const float Epsilon = 0.0001f;

        private float growthTimer;
        private float leafTimer;

        private static string Format(float value) => value.ToString("0.####", CultureInfo.InvariantCulture);
        
        public override bool HasScheduledTick => true;

        public static BlockStateContainer CreateSegmentState()
        {
            return new BlockStateContainer()
                .With(BlockStateKeys.WidthState, Format(InitialWidth))
                .With(BlockStateKeys.HeightState, Format(InitialHeight));
        }
        
        public static BlockStateContainer CreateTrunkState()
        {
            return CreateSegmentState()
                .With(SegmentsState, "")
                .With(TipLeafState, "")
                .With(SunlightState, "0")
                .With(HumidityState, "0")
                .With(TemperatureState, "0");
        }

        public override void OnScheduledTick(float deltaTime)
        {
            if (IsRemoved || State == null|| World.GetBlockAtWorldPos(Position) != BlockId)
                return;

            growthTimer += Mathf.Max(0f, deltaTime);
            if (growthTimer < GrowthIntervalSeconds)
                return;

            growthTimer = 0;
            Grow();
        }

        private void Grow()
        {
            string savedSegments = State.GetState(SegmentsState);
            var segments = string.IsNullOrEmpty(savedSegments)
                ? new List<string>()
                : new List<string>(savedSegments.Split(';'));
            
            // Never interpret an unloaded chunk as a broken log (GetBlock alone
            // returns air in that case). Saved IDs also detect same-type replacements.
            for (int i = 0; i < segments.Count; i++)
            {
                Vector3Int position = Position + Vector3Int.up * (i + 1);
                if (!World.TryGetKnownBlockAtWorldPos(position, out byte id))
                    return;

                if (id == BlockDataBase.TreeLog.id &&
                    World.GetBlockStateAtWorldPos(position)?.GetState(PartIdState) == segments[i])
                    continue;

                segments.RemoveRange(i, segments.Count - i);
                SaveSegments(segments);
                SetRootState(TipLeafState, "");
                leafTimer = 0f;
                break;
            }
            
            Vector3Int tipPosition = Position + Vector3Int.up * segments.Count;
            Vector3Int above = tipPosition + Vector3Int.up;
            if (!World.TryGetKnownBlockAtWorldPos(above, out byte aboveId))
                return;

            string leafId = State.GetState(TipLeafState);
            bool ownLeaf = aboveId == BlockDataBase.TreeLeaf.id &&
                           !string.IsNullOrEmpty(leafId) &&
                           World.GetBlockStateAtWorldPos(above)?.GetState(PartIdState) == leafId;

            // Any foreign block, including an old detached leaf/log, pauses ALL growth.
            if (aboveId != 0 && !ownLeaf)
                return;
            
            float widthBelow = MaximumWidth + SupportDifference;
            for (int i = 0; i <= segments.Count; i++)
            {
                Vector3Int position = Position + Vector3Int.up * i;
                BlockStateContainer state = World.GetBlockStateAtWorldPos(position);
                float width = ReadDimension(state, BlockStateKeys.WidthState, InitialWidth);
                float height = ReadDimension(state, BlockStateKeys.HeightState, InitialHeight);
                float limit = Mathf.Min(MaximumWidth, widthBelow - SupportDifference);

                // A too-wide existing segment stays, but cannot grow until supported.
                if (width + Epsilon < limit)
                {
                    width = Mathf.Min(limit, width + WidthPerGrowthStep);
                    World.SetBlockState(position, BlockStateKeys.WidthState, Format(width));
                }

                float targetHeight = Mathf.Lerp(InitialHeight, 1f,
                    Mathf.InverseLerp(InitialWidth, FullHeightWidth, width));
                if (height + Epsilon < targetHeight)
                    World.SetBlockState(position, BlockStateKeys.HeightState, Format(targetHeight));

                widthBelow = width;
            }
            BlockStateContainer tip = World.GetBlockStateAtWorldPos(tipPosition);
            if (ReadDimension(tip, BlockStateKeys.HeightState, InitialHeight) < 1f - Epsilon)
                return;

            if (!ownLeaf)
            {
                // Known virtual air chunks can be materialized by SetBlockAtWorldPos;
                // unknown chunks were rejected above and are left alone.
                World.SetBlockAtWorldPos(above, BlockDataBase.TreeLeaf.id);
                if (World.GetBlockAtWorldPos(above) != BlockDataBase.TreeLeaf.id)
                    return;

                leafId = Guid.NewGuid().ToString("N");
                World.SetBlockState(above, PartIdState, leafId);
                SetRootState(TipLeafState, leafId);
                leafTimer = 0f;
                return; // Keep a visible leaf stage even when support is already ample.
            }

            leafTimer += GrowthIntervalSeconds;
            float tipWidth = ReadDimension(tip, BlockStateKeys.WidthState, InitialWidth);
            // Require enough support for the child's FULL height, not just its seed.
            // Max width 1 and a 0.4 gap naturally stop at 1 -> 0.6 -> 0.2.
            if (leafTimer < LeafDelaySeconds ||
                tipWidth + Epsilon < FullHeightWidth + SupportDifference)
                return;

            World.SetBlockAtWorldPos(above, BlockDataBase.TreeLog.id);
            if (World.GetBlockAtWorldPos(above) != BlockDataBase.TreeLog.id)
                return;

            string segmentId = Guid.NewGuid().ToString("N");
            World.SetBlockState(above, PartIdState, segmentId);
            segments.Add(segmentId);
            SaveSegments(segments);
            SetRootState(TipLeafState, "");
            leafTimer = 0f;
        }
        
        private void SaveSegments(List<string> segments)
        {
            SetRootState(SegmentsState, string.Join(";", segments));
        }

        private void SetRootState(string key, string value)
        {
            World.SetBlockState(Position, key, value);
        }

        private static float ReadDimension(BlockStateContainer state, string key, float fallback)
        {
            if (!float.TryParse(state?.GetState(key), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float value) ||
                float.IsNaN(value) || float.IsInfinity(value))
                return fallback;

            return Mathf.Clamp01(value);
        }
    }
}
