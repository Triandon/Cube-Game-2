using System;
using UnityEngine;

namespace Core
{
    public class World : MonoBehaviour
    {
        public static World Instance;
    
        [Header("World Size in chunks!")] 
        public int worldSize = 50;
        public int worldSizeY = 10;

        [Header("Lighting SunLight")] [Range(0f, 1f)]
        public float SunLight = 1f;
        public int worldTime = 1200;
        private float timer;
        public int dayCounter = 0;
        public AnimationCurve sunlightCurve;
        
        private static readonly int SunLightShaderId = Shader.PropertyToID("_SunLight");
        private static readonly int SkyboxExposureId = Shader.PropertyToID("_Exposure");

        private TickCaller tickCaller;
        
        private void Awake()
        {
            Instance = this;

            ApplySunLight();

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
            UpdateSunLight();
            ApplySunLight();
            DayCycle();
        }

        private void OnValidate()
        {
            SunLight = Mathf.Clamp01(SunLight);
            ApplySunLight();
        }

        private void ApplySunLight()
        {
            float lightLevel = Mathf.Clamp01(SunLight);
            float a = Mathf.Pow(lightLevel, 2);
            Shader.SetGlobalFloat(SunLightShaderId, lightLevel);

            Material skyboxMaterial = RenderSettings.skybox;
            if (skyboxMaterial != null && skyboxMaterial.HasProperty(SkyboxExposureId))
            {
                skyboxMaterial.SetFloat(SkyboxExposureId, a);
            }

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

        private void DayCycle()
        {
            timer += Time.deltaTime;

            // 1 sec = 1 in game minute
            if (timer >= 1f)
            {
                timer -= 1f;

                worldTime++;
                int minutes = worldTime % 100;

                if (minutes >= 60)
                {
                    worldTime += 40;
                }

                if (worldTime >= 2400)
                {
                    worldTime = 0;
                    dayCounter++;
                    Debug.Log("New Day, your at day " + dayCounter);
                }

                //Debug.Log("Time: " + worldTime);
            }
        }
        
        private void UpdateSunLight()
        {
            int hours = worldTime / 100;
            int minutes = worldTime % 100;

            // Convert the current clock into minutes since midnight.
            float totalMinutes = (hours * 60) + minutes;

            // Add the partial real second so the lighting moves smoothly.
            totalMinutes += timer;

            // Convert 0-1440 minutes into 0-1.
            float dayProgress = totalMinutes / 1440f;

            SunLight = sunlightCurve.Evaluate(dayProgress);
        }
    }
}
