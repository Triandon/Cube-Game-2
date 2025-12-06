using Core;
using TMPro;
using UnityEngine;

public class MiscVariablesDisplay : MonoBehaviour
{
    public TextMeshProUGUI ChunkCountText;
    public TextMeshProUGUI RenderDistanceText;
    public TextMeshProUGUI chunkBuilding;
    public TextMeshProUGUI playerCordsText;
    private ChunkManager chunkManager;
    private int chunkCount;
    private int renderDistance;
    private int chunksCurrentlyBuilding;
    private Vector3Int playerPos;

    public Transform player;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        chunkManager = FindAnyObjectByType<ChunkManager>();
    }

    // Update is called once per frame
    void Update()
    {
        chunkCount = chunkManager.chunkCount;
        renderDistance = chunkManager.viewDistance;
        chunksCurrentlyBuilding = chunkManager.chunksPerFrame;
        playerPos = Vector3Int.FloorToInt(player.position);
        
        ChunkCountText.text = $"Chunk Count: {chunkCount}";
        RenderDistanceText.text = $"Render Distance: {renderDistance}";
        chunkBuilding.text = $"Chunks currently building: {chunksCurrentlyBuilding}";
        playerCordsText.text = "Coords: " + playerPos;

        if (Input.GetKeyDown(KeyCode.I))
        {
            chunkManager.viewDistance++;
            chunkManager.UpdateChunks();
        }

        if (Input.GetKeyDown(KeyCode.K))
        {
            chunkManager.viewDistance--;
            chunkManager.TrimUnusedChunks();
            chunkManager.UpdateChunks();
        }

        if (Input.GetKeyDown(KeyCode.U))
        {
            chunkManager.TrimUnusedChunks();
            chunkManager.UpdateChunks();
        }

        if (Input.GetKeyDown(KeyCode.Z))
        {
            chunkManager.SaveWorld();
        }
        
        if (Input.GetKeyDown(KeyCode.R))
        {
            player.position = new Vector3(2.5f, 108f, 2.6f);
        }
    }
}
