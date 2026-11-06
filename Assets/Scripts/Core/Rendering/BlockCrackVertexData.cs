using System;
using System.Collections.Generic;

namespace Core.Rendering
{
    public sealed class BlockCrackVertexData
    {
        public readonly struct VertexRange
        {
            public readonly int Start;
            public readonly int Count;
            public readonly int Next;

            public VertexRange(int start, int count, int next)
            {
                Start = start;
                Count = count;
                Next = next;
            }
        }
        
        public readonly MeshUtilityCustom.ChunkVertex[] Vertices;
        public readonly VertexRange[] Ranges;
        private readonly Dictionary<int, int> heads;

        public BlockCrackVertexData(MeshUtilityCustom.ChunkVertex[] vertices,
            Dictionary<int, int> heads, VertexRange[] ranges)
        {
            Vertices = vertices;
            this.heads = heads;
            Ranges = ranges;
        }

        // ceil(3 * damage / maxHp): any positive damage is visible; exact thirds
        // belong to the preceding stage. Use long arithmetic to avoid overflow.
        public static byte CalculateStage(int damage, int maxHp)
        {
            if (damage <= 0 || maxHp <= 0)
                return 0;
            return (byte)Math.Min(3L, (3L * damage + maxHp - 1L) / maxHp);
        }

        // Returns the first changed range, or -1 for hidden blocks/unchanged stages.
        public int SetStage(int blockIndex, byte stage)
        {
            if (!heads.TryGetValue(blockIndex, out int head) ||
                Vertices[Ranges[head].Start].CrackStage == stage)
                return -1;

            for (int r = head; r >= 0; r = Ranges[r].Next)
            {
                VertexRange range = Ranges[r];
                for (int i = range.Start; i < range.Start + range.Count; i++)
                    Vertices[i].CrackStage = stage;
            }
            return head;
        }
    }
}
