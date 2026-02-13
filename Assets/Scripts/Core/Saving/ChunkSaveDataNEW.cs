using System.Collections.Generic;

namespace Core
{
    [System.Serializable]
    public class RLEBlockRun
    {
        public byte id;
        public int count;
    }
    
    [System.Serializable]
    public class SerializableBlockStateEntry
    {
        public int index;
        public List<SerializableBlockState> states;
    }

    [System.Serializable]
    public class ChunkSaveDataNEW
    {
        // NEW: full base chunk
        public List<RLEBlockRun> baseBlocks;

        // EXISTING: player changes
        public List<SerializableBlockStateEntry> blockStates = new();
    }
}