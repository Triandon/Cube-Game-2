using Core.Block;
using Core.Item;
using UnityEngine;

namespace Core
{
    public class Test : MonoBehaviour
    {
        private void Awake()
        {
            var dummy = typeof(BlockDataBase);
            BlockDataBase.Init();
            ItemDatabase.Init();
            BlockId.Init();
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
