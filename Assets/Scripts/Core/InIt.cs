using Core.Block;
using Core.Item;
using UnityEngine;

namespace Core
{
    public class Init : MonoBehaviour
    {
        private void Awake()
        {
            BlockDataBase.Init();
            ItemDatabase.Init();
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
