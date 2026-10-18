using System.Collections.Generic;
using Core.Block;
using Core.TileEntities;
using UnityEngine;

namespace Core.Items
{
    public class DebugLog : Item.Item
    {
        public DebugLog(int id, string itemName, bool isBlock, byte blockId, int textureIndex, int maxStackSize) : base(id, itemName, isBlock, blockId, textureIndex, maxStackSize)
        {
            
        }

        public override void OnBlockActivated(Vector3Int pos, BlockStateContainer state, Block.Block block, Transform player)
        {
            Debug.Log($"Block: {block}, Block id: {block.id}, Block Pos: {pos}");
            Debug.Log($"Player pos: {player.position}");
            Debug.Log($"Player transform: {player}");

            if (state != null)
            {
                Dictionary<string, BlockState> allStates = state.GetAllStates;

                foreach (KeyValuePair<string, BlockState> entry in allStates)
                {
                    Debug.Log($"State: {entry.Key}, Value: {entry.Value.value}");
                }
            }

            ChunkManager cm = Object.FindAnyObjectByType<ChunkManager>();
            TileEntity tile = cm.GetTileEntity(pos);

            if (tile != null)
            {
                Debug.Log($"Tile entity: {tile.GetType().Name}");
                Debug.Log($"Tile position: {tile.Position}");
                Debug.Log($"Tile block ID: {tile.BlockId}");
            }
        }
    }
}
