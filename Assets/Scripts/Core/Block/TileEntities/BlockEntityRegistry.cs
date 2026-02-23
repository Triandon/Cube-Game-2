using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Block.TileEntities
{
    public static class BlockEntityRegistry
    {
        private static readonly Dictionary<byte, Func<Transform, Vector3Int,
            InventoryHolder>> BlockEntityRegistrations = new Dictionary<byte, Func<Transform, Vector3Int, InventoryHolder>>();

        public static void Register(byte blockId, Func<Transform, Vector3Int, InventoryHolder> factory)
        {
            if (factory == null)
            {
                Debug.LogWarning($"BlockEntityRegistry: Tried to register null factory for block id {blockId}.");
                return;
            }

            BlockEntityRegistrations[blockId] = factory;
        }

        public static bool TryCreate(byte blockId, Transform parent,
            Vector3Int worldPos, out InventoryHolder holder)
        {
            holder = null;

            if (!BlockEntityRegistrations.TryGetValue(blockId, out var factory))
            {
                return false;
            }

            holder = factory(parent, worldPos);
            return holder != null;
        }
    }
}
