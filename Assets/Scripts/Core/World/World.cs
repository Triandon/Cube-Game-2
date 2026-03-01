using UnityEngine;

namespace Core
{
    public class World : MonoBehaviour
    {
        public static World Instance;
    
        [Header("World Size in chunks!")] 
        public int worldSize = 50;
        public int worldSizeY = 3;

        private TickCaller tickCaller;
        
        private void Awake()
        {
            Instance = this;

            tickCaller = GetComponent<TickCaller>();
            if (tickCaller == null)
            {
                tickCaller = gameObject.AddComponent<TickCaller>();
            }
        }
        
        void Start()
        {
            ChunkManager cm = FindAnyObjectByType<ChunkManager>();
            tickCaller?.Init(cm);
        }
        
        void Update()
        {
            tickCaller?.Tick(Time.deltaTime);
        }

        public TickCaller GetTickCaller()
        {
            return tickCaller;
        }

        //Check if the chunk coordinate is inside the world
        public bool IsChunkInsideOfWorld(Vector3Int coord)
        {
            int halfXZ = worldSize / 2;

            return
                coord.x >= -halfXZ && coord.x < halfXZ &&
                coord.z >= -halfXZ && coord.z < halfXZ &&
                coord.y >= 0 && coord.y < worldSizeY;
        }


        //Check if neighbor chunk exist inside world
        public bool HasChunk(Vector3Int coord)
        {
            return IsChunkInsideOfWorld(coord);
        }

        public bool IsBlockInsideOfWorld(Vector3Int worldPos)
        {
            return
                worldPos.x >= 0 && worldPos.x < worldSize * Chunk.CHUNK_SIZE &&
                worldPos.z >= 0 && worldPos.z < worldSize * Chunk.CHUNK_SIZE &&
                worldPos.y >= 0 && worldPos.y < worldSizeY * Chunk.CHUNK_SIZE;
        }
    }
}
