using System;
using Core.Block;
using UnityEngine;

namespace Core.TileEntities
{
    public abstract class TileEntity
    {
        public ChunkManager World { get; private set; }
        public Vector3Int Position { get; private set; }
        public byte BlockId { get; private set; }
        public bool IsRemoved { get; private set; }
        public BlockStateContainer State => World?.GetBlockStateAtWorldPos(Position);

        // Opt in once, when the instance is created. Unused tick types cost no callbacks.
        public virtual bool HasInstantTick => false;
        public virtual bool HasScheduledTick => false;
        public virtual bool HasRandomTick => false;

        internal void Attach(ChunkManager world, Vector3Int position, byte blockId)
        {
            if (World != null || IsRemoved)
                throw new InvalidOperationException("A tile entity instance can only be attached once.");

            World = world ?? throw new ArgumentNullException(nameof(world));
            Position = position;
            BlockId = blockId;
        }

        internal void Remove(bool blockRemoved = false)
        {
            if (IsRemoved)
                return;

            IsRemoved = true;
            if (blockRemoved)
                OnBlockRemoved();
            OnRemoved();
        }

        // Called after both the block data and this instance are available in the world.
        public virtual void OnLoad() { }
        
        // Called only when the owning block is mined/replaced, before OnRemoved.
        // Chunk unload and shutdown must not drop a container's contents.
        public virtual void OnBlockRemoved() { }

        // Called on block replacement/mining, chunk unload, or world shutdown.
        public virtual void OnRemoved() { }

        public virtual void OnInstantTick() { }
        public virtual void OnScheduledTick(float deltaTime) { }
        public virtual void OnRandomTick() { }
    }
}
