using Misc.InventoryHolders;
using UnityEngine;

namespace Core.Block.TileEntities
{
    public class BlockEntityDataBase
    {
        static BlockEntityDataBase()
        {
            RegisterBlockEntities();
        }

        private static void RegisterBlockEntities()
        {
            //Chest entity   
            BlockEntityRegistry.Register(BlockDataBase.ChestBlock.id,
                (parent, worldPos) =>
                {
                    var go = new GameObject("ChestEntity");
                    go.transform.SetParent(parent, false);
                    go.transform.position = worldPos + Vector3.one * 0.5f;

                    var holder = go.AddComponent<ChestInventoryHolder>();
                    holder.Init(worldPos);
                    return holder;
                });
            
            //Crafting table entity
            BlockEntityRegistry.Register(BlockDataBase.CraftingTableBlock.id,
                (parent, worldPos) =>
                {
                    var go = new GameObject("CraftingTableEntity");
                    go.transform.SetParent(parent, false);
                    go.transform.position = worldPos + Vector3.one * 0.5f;
            
                    var holder = go.AddComponent<CraftingTableInventoryHolder>();
                    holder.Init(worldPos);
                    return holder;
                });
            
            //Crusher entity
            BlockEntityRegistry.Register(BlockDataBase.CrusherBlock.id,
                (parent, worldPos) =>
                {
                    var go = new GameObject("CrusherEntity");
                    go.transform.SetParent(parent, false);
                    go.transform.position = worldPos + Vector3.one * 0.5f;

                    var holder = go.AddComponent<CrusherInventoryHolder>();
                    holder.Init(worldPos);
                    return holder;
                });
            
        }

        public static void Init()
        {
            
        }
    }
}
