using System;
using System.Collections.Generic;

namespace Core.Rendering
{
    /// <summary>
    /// Allocates contiguous integer ranges and coalesces adjacent ranges when they
    /// are returned. Terrain pages use separate instances for vertices and indices.
    /// </summary>
    public sealed class ContiguousRangeAllocator
    {
        private readonly List<Range> freeRanges = new List<Range>();

        public int Capacity { get; }
        public int TotalFree { get; private set; }
        public int LargestFreeRange { get; private set; }
        public int FreeRangeCount => freeRanges.Count;

        public readonly struct Allocation
        {
            public readonly int Offset;
            public readonly int Length;

            public Allocation(int offset, int length)
            {
                Offset = offset;
                Length = length;
            }
        }

        private struct Range
        {
            public int Offset;
            public int Length;

            public Range(int offset, int length)
            {
                Offset = offset;
                Length = length;
            }
        }

        public ContiguousRangeAllocator(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            Capacity = capacity;
            TotalFree = capacity;
            LargestFreeRange = capacity;
            freeRanges.Add(new Range(0, capacity));
        }

        public bool TryAllocate(int length, out Allocation allocation)
        {
            allocation = default;
            if (length <= 0 || length > LargestFreeRange)
                return false;

            // Best fit keeps large ranges available for unusually complex chunks.
            int bestIndex = -1;
            int bestLength = int.MaxValue;
            for (int i = 0; i < freeRanges.Count; i++)
            {
                int candidateLength = freeRanges[i].Length;
                if (candidateLength >= length && candidateLength < bestLength)
                {
                    bestIndex = i;
                    bestLength = candidateLength;
                    if (candidateLength == length)
                        break;
                }
            }

            if (bestIndex < 0)
                return false;

            Range range = freeRanges[bestIndex];
            allocation = new Allocation(range.Offset, length);
            if (range.Length == length)
                freeRanges.RemoveAt(bestIndex);
            else
                freeRanges[bestIndex] = new Range(range.Offset + length, range.Length - length);

            TotalFree -= length;
            RecalculateLargestRange();
            return true;
        }

        public void Free(Allocation allocation)
        {
            if (allocation.Length <= 0 || allocation.Offset < 0 ||
                allocation.Offset > Capacity - allocation.Length)
                throw new ArgumentOutOfRangeException(nameof(allocation));

            int insertIndex = 0;
            while (insertIndex < freeRanges.Count && freeRanges[insertIndex].Offset < allocation.Offset)
                insertIndex++;

            int allocationEnd = allocation.Offset + allocation.Length;
            if (insertIndex > 0)
            {
                Range previous = freeRanges[insertIndex - 1];
                if (previous.Offset + previous.Length > allocation.Offset)
                    throw new InvalidOperationException("The allocation overlaps an already free range.");
            }

            if (insertIndex < freeRanges.Count && allocationEnd > freeRanges[insertIndex].Offset)
                throw new InvalidOperationException("The allocation overlaps an already free range.");

            freeRanges.Insert(insertIndex, new Range(allocation.Offset, allocation.Length));
            TotalFree += allocation.Length;

            if (insertIndex > 0)
            {
                Range previous = freeRanges[insertIndex - 1];
                Range current = freeRanges[insertIndex];
                if (previous.Offset + previous.Length == current.Offset)
                {
                    freeRanges[insertIndex - 1] = new Range(previous.Offset, previous.Length + current.Length);
                    freeRanges.RemoveAt(insertIndex);
                    insertIndex--;
                }
            }

            if (insertIndex + 1 < freeRanges.Count)
            {
                Range current = freeRanges[insertIndex];
                Range next = freeRanges[insertIndex + 1];
                if (current.Offset + current.Length == next.Offset)
                {
                    freeRanges[insertIndex] = new Range(current.Offset, current.Length + next.Length);
                    freeRanges.RemoveAt(insertIndex + 1);
                }
            }

            RecalculateLargestRange();
        }

        private void RecalculateLargestRange()
        {
            int largest = 0;
            for (int i = 0; i < freeRanges.Count; i++)
                largest = Math.Max(largest, freeRanges[i].Length);
            LargestFreeRange = largest;
        }
    }
}
