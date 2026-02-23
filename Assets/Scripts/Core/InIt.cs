using Core.Block;
using Core.Block.TileEntities;
using Core.Item;
using UnityEngine;

namespace Core
{
    public class Init : MonoBehaviour
    {
        private void Awake()
        {
            BlockDataBase.Init();
            BlockEntityDataBase.Init();
            // Load atlas
            Material itemAtlas = Resources.Load<Material>("Materials/AtlasMaterial");
            ItemRegistry.InitAtlas(
                atlasMaterial: itemAtlas,
                atlasSize: 256,
                tileSize: 16
            );
            ItemDatabase.Init();
            MaterialDatabase.Init();
            RecipeDataBase.Init(); 
        }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
        
        }

        // Update is called once per frame
        void Update()
        {
        
        }
    }
}
