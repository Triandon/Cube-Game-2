using System.Collections.Generic;
using UnityEngine;

namespace Core.Block
{
    public class Block
    {

        public byte id;
        public string blockName;

        public int topIndex;
        public int sideIndex;
        public int bottomIndex;
        public int frontIndex;
    
        public List<BlockState> states = new List<BlockState>();

        public float hardness = 1f;
        public bool isTransparent = false;
        public virtual bool HasBlockEntity => false;

        public Block(byte id, string name, int top, int side, int bottom, int front = -1)
        {
            this.id = id;
            this.blockName = name;
            this.topIndex = top;
            this.sideIndex = side;
            this.bottomIndex = bottom;
            this.frontIndex = front;
        }

        public void AddState(string stateName, string value)
        {
            var existing = states.Find(s => s.stateName == stateName);
            if (existing != null)
                existing.value = value;
            else
                states.Add(new BlockState(stateName, value));
        }
    
        public string GetState(string stateName)
        {
            var existing = states.Find(s => s.stateName == stateName);
            return existing != null ? existing.value : null;
        }

        public bool HasStates => states.Count > 0;
        
        //Events
        
        // Called once a block is placed!
        public virtual void OnPlaced(
            Vector3Int position, BlockStateContainer state, Transform player)
        {
            // default: does nothing
        }
        
        // Called when a block is activated (right clicked)
        public virtual bool OnActivated(
            Vector3Int position, BlockStateContainer state, Block block, Transform player)
        {
            // true => interaction  false => default behavior
            return false;
        }

        // Called when a block is LEFT-Clicked
        public virtual void OnClicked(
            Vector3Int position, BlockStateContainer state, Block block,Transform player)
        {
            //Debug.Log("Block clicked with block: " + block + " and with id: " + block.id);
        }

        // When a block is mined, aka replaced with air blockId: 0
        public virtual void OnMined(
            Vector3Int position, BlockStateContainer state, Transform player
        )
        {
            
        }
    
    }
}
