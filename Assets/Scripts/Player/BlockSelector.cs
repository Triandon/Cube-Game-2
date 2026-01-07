using Core;
using Core.Block;
using Core.Item;
using UnityEngine;

public class BlockSelector : MonoBehaviour
{

    public Camera playerCamera;
    public float maxDistance = 6.0f;

    private ChunkManager chunkManager;
    private Inventory inventory;

    public Vector3Int highlightedBlock;
    public bool hasHit;

    private RaycastHit lastHit;

    public Transform highlightCube;
    public Transform cubeParent;

    private HotBarUI hotBar;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        chunkManager = FindAnyObjectByType<ChunkManager>();
        hotBar = FindAnyObjectByType<HotBarUI>();
        InventoryHolder holder = GetComponent<InventoryHolder>();
        inventory = holder.Inventory;
        GenerateSelectorCube();
    }

    // Update is called once per frame
    void Update()
    {
        CheckForHit();
        UpdateHighLight();
        HandleInput();
    }

    private void CheckForHit()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
        {
            lastHit = hit;
            hasHit = true;
        }
        else
        {
            hasHit = false;
        }
    }

    private void UpdateHighLight()
    {
        if (hasHit)
        {

            highlightedBlock = GetTargetBlockPos(lastHit, false);
            highlightCube.gameObject.SetActive(true);
            highlightCube.position = highlightedBlock + Vector3.one * 0.5f;
        }
        else
        {
            highlightCube.gameObject.SetActive(false);
        }
    }

    private void GenerateSelectorCube()
    {
        highlightCube = Instantiate(highlightCube);
        highlightCube.gameObject.SetActive(false);
    }

    private void HandleInput()
    {
        if(!hasHit) return;

        // Break block
        if (Input.GetMouseButtonDown(0))
        {
            Vector3Int target = GetTargetBlockPos(lastHit, false);
            byte block = chunkManager.GetBlockAtWorldPos(target);

            if (block != 0)
            {
                Item item = ItemRegistry.GetItem(block);

                if (item != null)
                {
                    inventory.AddItem(item.id, 1,item.itemName);

                }
            }
            
            ModifyBlock(target,0);
        }

        //Place block
        if (Input.GetMouseButtonDown(1))
        {
            Vector3Int target = GetTargetBlockPos(lastHit, true);
            ItemStack stack = hotBar.GetSelectedStack();

            if (stack != null && !stack.IsEmpty && stack.Item.isBlock)
            {
                if (!IsInsideOfPlayer(target))
                {
                    ModifyBlock(target,stack.Item.blockId);

                    inventory.RemoveItemFromSlot(hotBar.GetSelectedSlot(), 1);
                }
            }
        }
    }

    private void ModifyBlock(Vector3Int worldPos, byte id)
    {
        chunkManager.SetBlockAtWorldPos(worldPos, id);
    }
    
    private Vector3Int GetTargetBlockPos(RaycastHit hit, bool place)
    {
        Vector3 pos = hit.point;

        if (place)
        {
            // Move one block in the direction of the hit normal
            pos += hit.normal * 0.5f;
        }
        else
        {
            // Move slightly inside the block to avoid rounding issues
            pos -= hit.normal * 0.5f;
        }

        return Vector3Int.FloorToInt(pos);
    }

    private bool IsInsideOfPlayer(Vector3Int blockWorldPos)
    {
        Vector3 blockCenter = blockWorldPos + Vector3.one * 0.5f;
        Vector3 blockExtents = Vector3.one * 0.45f;

        int playerLayer = LayerMask.GetMask("Player");

        return Physics.CheckBox(blockCenter, blockExtents, Quaternion.identity, playerLayer);
    }
    

}
