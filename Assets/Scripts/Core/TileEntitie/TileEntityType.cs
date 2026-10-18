using System;

namespace Core.TileEntities
{
    public abstract class TileEntityType
    {
        public string Id { get; }

        protected TileEntityType(string id)
        {
            Id = id;
        }

        public abstract TileEntity Create();
    }

    public sealed class TileEntityType<T> : TileEntityType where T : TileEntity
    {
        private readonly Func<T> factory;

        internal TileEntityType(string id, Func<T> factory) : base(id)
        {
            this.factory = factory;
        }

        public override TileEntity Create()
        {
            return factory() ?? throw new InvalidOperationException($"Tile entity factory '{Id}' returned null.");
        }
    }
}
