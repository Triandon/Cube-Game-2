using System.Collections.Generic;
using UnityEngine;

namespace Core.Block
{
    public static class BlockRegistry
    {
        // Dictionary to store blocks by ID
        private static Dictionary<int, Block> blocksById = new Dictionary<int, Block>();
    
        // Optional: store by name as well
        private static Dictionary<string, Block> blocksByName = new Dictionary<string, Block>();

        public static Block[] ThreadBlockInfo
        {
            get;
            private set;
        } = new Block[256];
        

        // Register a block
        public static void RegisterBlock(Block block)
        {
            if (!blocksById.ContainsKey(block.id))
            {
                blocksById.Add(block.id, block);
                blocksByName.Add(block.blockName, block);
                Debug.Log($"Registered block: {block.blockName} (ID: {block.id})");
            }
            else
            {
                Debug.LogWarning($"Block ID {block.id} already exists: {block.blockName}");
            }
        }

        // Get a block by ID
        public static Block GetBlock(int id)
        {
            if (blocksById.TryGetValue(id, out Block block))
                return block;
            return null;
        }

        // Get a block by Name
        public static Block GetBlock(string name)
        {
            if (blocksByName.TryGetValue(name, out Block block))
                return block;
            return null;
        }
        
        // Call this once on main thread after all blocks are registered (e.g. at game start)
        public static void BuildThreadLookup()
        {
            var arr = new Block[256];
            foreach (var kv in blocksById)
            {
                int key = kv.Key;
                Block b = kv.Value;
                if (key >= 0 && key < 256)
                {
                    arr[key] = new Block(b.id, b.blockName, b.topIndex, b.sideIndex, b.bottomIndex, b.frontIndex);
                }

                ThreadBlockInfo = arr;
                Debug.Log($"BlockRegistry: built thread lookup for {blocksById.Count} blocks.");
            }
        }
    }
}