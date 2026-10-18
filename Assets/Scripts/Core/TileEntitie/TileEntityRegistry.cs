using System;
using System.Collections.Generic;

namespace Core.TileEntities
{
    public static class TileEntityRegistry
    {
        private static readonly Dictionary<string, TileEntityType> types =
            new Dictionary<string, TileEntityType>(StringComparer.Ordinal);
        private static readonly Dictionary<byte, TileEntityType> typesByBlock =
            new Dictionary<byte, TileEntityType>();

        // Register during startup, before the chunk worker threads begin reading blocks.
        public static TileEntityType<T> Register<T>(string id, Func<T> factory, params Block.Block[] blocks)
            where T : TileEntity
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A tile entity type needs an ID.", nameof(id));
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));
            if (blocks == null || blocks.Length == 0)
                throw new ArgumentException("A tile entity type needs at least one block.", nameof(blocks));
            if (types.ContainsKey(id))
                throw new ArgumentException($"Tile entity type '{id}' is already registered.", nameof(id));

            var blockIds = new HashSet<byte>();
            foreach (Block.Block block in blocks)
            {
                if (block == null || block.id == 0)
                    throw new ArgumentException("Tile entities require a non-air block.", nameof(blocks));
                if (typesByBlock.ContainsKey(block.id) || !blockIds.Add(block.id))
                    throw new ArgumentException($"Block {block.id} already has a tile entity type.", nameof(blocks));
            }

            var type = new TileEntityType<T>(id, factory);
            types.Add(id, type);
            foreach (byte blockId in blockIds)
                typesByBlock.Add(blockId, type);
            return type;
        }

        public static bool HasTileEntity(byte blockId) => typesByBlock.ContainsKey(blockId);

        public static TileEntityType Get(string id)
        {
            return types.TryGetValue(id, out TileEntityType type) ? type : null;
        }

        public static TileEntity Create(byte blockId)
        {
            return typesByBlock.TryGetValue(blockId, out TileEntityType type) ? type.Create() : null;
        }
    }
}
