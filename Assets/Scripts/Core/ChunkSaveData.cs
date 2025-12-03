using System.Collections.Generic;

namespace Core
{
    [System.Serializable]
    public class SerializableBlockChange
    {
        public int index;
        public byte id;
    }

    [System.Serializable]
    public class ChunkSaveData
    {
        public List<SerializableBlockChange> changedBlocks = new List<SerializableBlockChange>();
    }
}