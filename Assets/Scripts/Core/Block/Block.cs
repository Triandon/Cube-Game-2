using System.Collections.Generic;

namespace Core.Block
{
    public class Block
    {

        public byte id;
        public string blockName;

        public int topIndex;
        public int sideIndex;
        public int bottomIndex;
    
        public List<BlockState> states = new List<BlockState>();

        public float hardness = 1f;
        public bool isTransparent = false;

        public Block(byte id, string name, int top, int side, int bottom)
        {
            this.id = id;
            this.blockName = name;
            this.topIndex = top;
            this.sideIndex = side;
            this.bottomIndex = bottom;
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
    
    }
}
