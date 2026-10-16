using System.Collections.Generic;
using Core.Block;
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
            
            Debug.Log($"Block: {block}, Block id: {blockId}");
            Debug.Log($"Player pos: {player.position}");
            Debug.Log($"Player transform: {player}");
            
            Dictionary<string, BlockState> allStates = state.GetAllStates;

            foreach (KeyValuePair<string, BlockState> entry in allStates)
            {
                Debug.Log($"State: {entry.Key}, Value: {entry.Value.value}");
            }
            
        }
    }
}
