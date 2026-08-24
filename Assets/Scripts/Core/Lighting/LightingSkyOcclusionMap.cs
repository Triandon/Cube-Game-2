using System.Collections.Generic;
using Core;
using UnityEngine;

public sealed class LightingSkyOcclusionMap
    {
        private readonly Dictionary<Vector3Int, byte[]> chunkColumns =
            new Dictionary<Vector3Int, byte[]>();
        private readonly Dictionary<Vector2Int, int> highestOccluders =
            new Dictionary<Vector2Int, int>();

        public IReadOnlyDictionary<Vector3Int, byte[]> ChunkColumns => chunkColumns;

        public bool UpdateChunk(Vector3Int coord, byte[,,] blocks, int worldHeightInChunks,
            ICollection<Vector2Int> changedColumns = null)
        {
            int size = Chunk.CHUNK_SIZE;
            byte[] columns = new byte[size * size];
            bool hasOccluder = false;

            if (blocks != null)
            {
                for (int x = 0; x < size; x++)
                for (int z = 0; z < size; z++)
                {
                    byte height = FindHighestOccluder(blocks, x, z);
                    columns[ColumnIndex(x, z)] = height;
                    hasOccluder |= height != 0;
                }
            }

            chunkColumns.TryGetValue(coord, out byte[] previous);
            if ((!hasOccluder && previous == null) ||
                (hasOccluder && previous != null && ColumnsEqual(previous, columns)))
                return false;

            if (hasOccluder)
                chunkColumns[coord] = columns;
            else
                chunkColumns.Remove(coord);

            for (int x = 0; x < size; x++)
            for (int z = 0; z < size; z++)
            {
                int index = ColumnIndex(x, z);
                byte oldHeight = previous?[index] ?? 0;
                if (oldHeight == columns[index])
                    continue;

                RecalculateHighest(coord.x * size + x, coord.z * size + z, worldHeightInChunks);
                changedColumns?.Add(new Vector2Int(x, z));
            }

            return true;
        }

        public bool UpdateColumn(Vector3Int coord, byte[,,] blocks, int localX, int localZ,
            int worldHeightInChunks)
        {
            byte height = FindHighestOccluder(blocks, localX, localZ);
            int index = ColumnIndex(localX, localZ);

            if (!chunkColumns.TryGetValue(coord, out byte[] columns))
            {
                if (height == 0)
                    return false;

                columns = new byte[Chunk.CHUNK_SIZE * Chunk.CHUNK_SIZE];
                chunkColumns.Add(coord, columns);
            }
            else if (columns[index] == height)
            {
                return false;
            }

            columns[index] = height;
            if (height == 0 && IsEmpty(columns))
                chunkColumns.Remove(coord);

            RecalculateHighest(coord.x * Chunk.CHUNK_SIZE + localX,
                coord.z * Chunk.CHUNK_SIZE + localZ, worldHeightInChunks);

            return true;
        }

        public bool HasOccluderAbove(int worldX, int worldY, int worldZ)
        {
            return highestOccluders.TryGetValue(new Vector2Int(worldX, worldZ), out int height) &&
                   height > worldY;
        }

        public void ReplaceWith(IEnumerable<KeyValuePair<Vector3Int, byte[]>> entries)
        {
            chunkColumns.Clear();
            highestOccluders.Clear();
            if (entries == null)
                return;

            int expectedLength = Chunk.CHUNK_SIZE * Chunk.CHUNK_SIZE;
            foreach (KeyValuePair<Vector3Int, byte[]> entry in entries)
            {
                if (entry.Value == null || entry.Value.Length != expectedLength || IsEmpty(entry.Value))
                    continue;

                chunkColumns[entry.Key] = (byte[])entry.Value.Clone();
            }

            foreach (KeyValuePair<Vector3Int, byte[]> entry in chunkColumns)
            {
                int size = Chunk.CHUNK_SIZE;
                for (int x = 0; x < size; x++)
                for (int z = 0; z < size; z++)
                {
                    byte localHeight = entry.Value[ColumnIndex(x, z)];
                    if (localHeight == 0)
                        continue;

                    Vector2Int column = new Vector2Int(entry.Key.x * size + x, entry.Key.z * size + z);
                    int worldHeight = entry.Key.y * size + localHeight - 1;
                    if (!highestOccluders.TryGetValue(column, out int current) || worldHeight > current)
                        highestOccluders[column] = worldHeight;
                }
            }
        }

        private void RecalculateHighest(int worldX, int worldZ, int worldHeightInChunks)
        {
            int size = Chunk.CHUNK_SIZE;
            int chunkX = Mathf.FloorToInt((float)worldX / size);
            int chunkZ = Mathf.FloorToInt((float)worldZ / size);
            int localX = PositiveModulo(worldX, size);
            int localZ = PositiveModulo(worldZ, size);
            int highest = int.MinValue;

            for (int chunkY = 0; chunkY < worldHeightInChunks; chunkY++)
            {
                if (!chunkColumns.TryGetValue(new Vector3Int(chunkX, chunkY, chunkZ), out byte[] columns))
                    continue;

                byte localHeight = columns[ColumnIndex(localX, localZ)];
                if (localHeight != 0)
                    highest = Mathf.Max(highest, chunkY * size + localHeight - 1);
            }

            Vector2Int key = new Vector2Int(worldX, worldZ);
            if (highest == int.MinValue)
                highestOccluders.Remove(key);
            else
                highestOccluders[key] = highest;
        }

        private static byte FindHighestOccluder(byte[,,] blocks, int x, int z)
        {
            if (blocks == null)
                return 0;

            for (int y = Chunk.CHUNK_SIZE - 1; y >= 0; y--)
            {
                if (VoxelLight.BlocksSkyLight(blocks[x, y, z]))
                    return (byte)(y + 1);
            }

            return 0;
        }

        private static int ColumnIndex(int x, int z) => x + Chunk.CHUNK_SIZE * z;

        private static int PositiveModulo(int value, int divisor)
        {
            int remainder = value % divisor;
            return remainder < 0 ? remainder + divisor : remainder;
        }

        private static bool IsEmpty(byte[] columns)
        {
            for (int i = 0; i < columns.Length; i++)
            {
                if (columns[i] != 0)
                    return false;
            }

            return true;
        }

        private static bool ColumnsEqual(byte[] left, byte[] right)
        {
            if (left == null || left.Length != right.Length)
                return false;

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                    return false;
            }

            return true;
        }
    }

