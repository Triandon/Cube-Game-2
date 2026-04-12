using System;
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
        public bool isStrechy = false;
        public bool isCentered = false;
        public int shapeIndex = (int)BlockShapes.Cube;
        
        public virtual bool HasBlockEntity => false;
        public virtual bool HasInstantTick => false;
        public virtual bool HasScheduledTick => false;
        public virtual bool HasRandomTick => false;

        public Block(byte id, string name, int top, int side, int bottom, int front = -1)
        {
            this.id = id;
            this.blockName = name;
            this.topIndex = top;
            this.sideIndex = side;
            this.bottomIndex = bottom;
            this.frontIndex = front;
            
            AddState(BlockStateKeys.HeightState, "1");
            AddState(BlockStateKeys.WidthState, "1");
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

        public bool HasDefinedState(string stateName)
        {
            return states.Exists(s => s.stateName == stateName);
        }
        
        //Events
        
        // Called once a block is placed!
        public virtual void OnPlaced(
            Vector3Int position, BlockStateContainer state, Transform player)
        {
            OnPlaced(position, state, player, null);
        }

        public virtual void OnPlaced(
            Vector3Int position, BlockStateContainer state, Transform player, Vector3Int? placementFace)
        {
            if (state == null)
                return;
             
            if (frontIndex < 0)
                return;
            
            if (!HasDefinedState(BlockStateKeys.DirectionalFacing))
                return;
            
            state.SetState(BlockStateKeys.DirectionalFacing, GetHorizontalFacingTowardPlayer(player));
        }
        
        protected static string GetHorizontalFacingTowardPlayer(Transform player)
        {
            if (player == null)
                return DirectionalFacing.North;

            Vector3 forward = -player.transform.forward;

            if (Mathf.Abs(forward.x) > Mathf.Abs(forward.z))
                return forward.x > 0 ? DirectionalFacing.East : DirectionalFacing.West;

            return forward.z > 0 ? DirectionalFacing.North : DirectionalFacing.South;
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

        // Called every frame (REALLY! Not recommended!)
        public virtual void OnInstantTick(Vector3Int position, ChunkManager chunkManager)
        {
            
        }

        // Called from the queued tick scheduler. Delta is the time since this block's last scheduled call.
        public virtual void OnScheduledTick(Vector3Int position, float deltaTime, ChunkManager chunkManager)
        {
            
        }

        // Called on random intervals (Not recommended)
        public virtual void OnRandomTick(Vector3Int position, ChunkManager chunkManager)
        {
            
        }
    
    }
}
