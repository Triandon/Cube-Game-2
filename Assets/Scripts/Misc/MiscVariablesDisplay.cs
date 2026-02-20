using Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MiscVariablesDisplay : MonoBehaviour
{
    public TextMeshProUGUI ChunkCountText;
    public TextMeshProUGUI RenderDistanceText;
    public TextMeshProUGUI chunkBuilding;
    public TextMeshProUGUI playerCordsText, usernameDisplayText, currentLodDistanceText;
    public TextMeshProUGUI chunkClimateText;

    [SerializeField] private GameObject keyInfo, debugPanel, chatBoxGO, cursorGO;
    
    private ChunkManager chunkManager;
    private int chunkCount;
    private int renderDistance;
    private int chunksCurrentlyBuilding;
    private Vector3Int playerPos;

    public Transform player;

    private Settings settings;

    [SerializeField] private PlayerInventoryHolder playerHolder;
    public TMP_InputField chatBox;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        chunkManager = FindAnyObjectByType<ChunkManager>();
        settings = Settings.Instance;
        if (settings != null)
        {
            usernameDisplayText.text = settings.userName;
            currentLodDistanceText.text = "Lod Distance: " + settings.lodDistance;
        }
        else
        {
            usernameDisplayText.text = playerHolder.GetInventoryName();
            currentLodDistanceText.text = "Lod Distance: " + 32;
        }
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
        chunkBuilding.text = $"CCB: {chunksCurrentlyBuilding}";
        playerCordsText.text = "Coords: " + playerPos;

        Vector3Int playerChunk = GetPlayerChunkCoord();
        ChunkClimate climate = BiomeManager.GetChunkClimate(playerChunk);
        
        chunkClimateText.text =
            $"Chunk Climate | T: {climate.temperature:F2} H: {climate.humidity:F2}";

        if (!chatBox.isFocused)
        {
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
                InventoryHolder[] allHolders = FindObjectsOfType<InventoryHolder>();
                foreach (var holder in allHolders)
                {
                    holder.SaveInventory();
                }
            }
        
            if (Input.GetKeyDown(KeyCode.R))
            {
                player.position = new Vector3(2.5f, 108f, 2.6f);
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                chunkManager.SaveWorld();
                InventoryHolder[] allHolders = FindObjectsOfType<InventoryHolder>();
                foreach (var holder in allHolders)
                {
                    holder.SaveInventory();
                }
                
                Cursor.lockState = CursorLockMode.None;
                SceneManager.LoadScene("Scenes/Menu");
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                int newSize = playerHolder.Inventory.Size == 5 ? 3 : 5;
                playerHolder.Inventory.Resize(newSize);
            }

            if (Input.GetKeyDown(KeyCode.F1))
            {
                HideAllTrash();
            }

            if (Input.GetKeyDown(KeyCode.F2))
            {
                HideCursor();
            }
        }
    }

    private void HideAllTrash()
    {
        bool a = ChunkCountText.gameObject.activeInHierarchy;

        if (a)
        {
            a = false;
        }
        else
        {
            a = true;
        }
        
        ChunkCountText.gameObject.SetActive(a);
        RenderDistanceText.gameObject.SetActive(a);
        chunkBuilding.gameObject.SetActive(a);
        playerCordsText.gameObject.SetActive(a);
        usernameDisplayText.gameObject.SetActive(a);
        keyInfo.SetActive(a);
        debugPanel.SetActive(a);
        chatBoxGO.SetActive(a);
    }

    private void HideCursor()
    {
        bool b = cursorGO.activeInHierarchy;

        if (b) b = false;
        else b = true;
        
        cursorGO.SetActive(b);
    }
    
    private Vector3Int GetPlayerChunkCoord()
    {
        return new Vector3Int(
            Mathf.FloorToInt(player.position.x / Chunk.CHUNK_SIZE),
            Mathf.FloorToInt(player.position.y / Chunk.CHUNK_SIZE),
            Mathf.FloorToInt(player.position.z / Chunk.CHUNK_SIZE)
        );
    }
}
