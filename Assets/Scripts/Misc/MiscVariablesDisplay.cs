using Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MiscVariablesDisplay : MonoBehaviour
{
    public TextMeshProUGUI ChunkCountText;
    public TextMeshProUGUI RenderDistanceText;
    public TextMeshProUGUI chunkBuilding;
    public TextMeshProUGUI playerCordsText, usernameDisplayText;
    private ChunkManager chunkManager;
    private int chunkCount;
    private int renderDistance;
    private int chunksCurrentlyBuilding;
    private Vector3Int playerPos;

    public Transform player;

    private Settings settings;

    [SerializeField] private InventoryHolder playerHolder;
    public TMP_InputField chatBox;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        chunkManager = FindAnyObjectByType<ChunkManager>();
        settings = Settings.Instance;
        if (settings != null)
        {
            usernameDisplayText.text = settings.userName;
        }
        else
        {
            usernameDisplayText.text = playerHolder.GetInventoryName();
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
        chunkBuilding.text = $"Chunks currently building: {chunksCurrentlyBuilding}";
        playerCordsText.text = "Coords: " + playerPos;

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
                Cursor.lockState = CursorLockMode.None;
                SceneManager.LoadScene("Scenes/Menu");
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                int newSize = playerHolder.Inventory.Size == 5 ? 3 : 5;
                playerHolder.Inventory.Resize(newSize);
            }
        }
    }
}
