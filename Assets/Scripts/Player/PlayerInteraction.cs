using Core;
using Core.Block;
using Core.Item;
using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction")] 
    public Camera playerCamera;
    public float pickupDistance = 3f;

    private Inventory inventory;
    private HotBarUI hotBarUI;

    private ChunkManager chunkManager;
    private Transform player;

    private float scaffoldingPlacmentHeight = 1f;
    private bool blockPlacementMode;
    
    //Hits rays
    public float maxDistance = 6.0f;
    private bool hasHit;
    private RaycastHit lastHit;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        chunkManager = FindAnyObjectByType<ChunkManager>();
        inventory = GetComponent<InventoryHolder>().Inventory;
        hotBarUI = FindAnyObjectByType<HotBarUI>();
        player = transform;
        
        GetComponent<PlayerInventoryHolder>().OpenInventory();
    }

    // Update is called once per frame
    void Update()
    {
        CheckForHit();
        HandleInput();
        
        if(!IsTryingToInteract())
            return;
        
        PickupItem();
    }

    private void HandleInput()
    {
        HandleScaffoldingHeightHotKeysAndBlockPlacementMode();
        
        if(Input.GetKey(KeyCode.LeftAlt))
            return;
        
        if(!hasHit) return;

        // Break block
        if (Input.GetMouseButtonDown(0))
        {
            Vector3Int target = GetTargetBlockPos(lastHit, false);
            byte blockId = chunkManager.GetBlockAtWorldPos(target);
            Block block = BlockRegistry.GetBlock(blockId);
            BlockStateContainer state = chunkManager.GetBlockStateAtWorldPos(target);

            if (block != null && block.id != 0)
            {
                block?.OnClicked(target, state, block, player);
                
                Item item = ItemRegistry.GetItem(block.id);

                if (item != null)
                {
                    CompositionLogic composition = new CompositionLogic();

                    if (block.id == BlockDataBase.SnowBlock.id)
                    {
                        composition = CompositionLogic.Add(
                            (MaterialDatabase.Granite.materialId,
                                Random.Range(0.1f, 0.8f)));
                    }
                    else
                    {
                        composition = CompositionGenerator.GenerateRandom();
                    }
                    
                    inventory.AddItem(item.id, 1,item.itemName, composition);
                }
            }
            
            ModifyBlock(target,0);
        }

        //Place block
        if (Input.GetMouseButtonDown(1))
        {
            Vector3Int target = GetTargetBlockPos(lastHit, true);
            ItemStack stack = hotBarUI.GetSelectedStack();

            Vector3Int hitTarget = GetTargetBlockPos(lastHit, false);
            byte hitBlockId = chunkManager.GetBlockAtWorldPos(hitTarget);
            Block hitBlock = BlockRegistry.GetBlock(hitBlockId);
            BlockStateContainer state = chunkManager.GetBlockStateAtWorldPos(hitTarget);

            bool interactionConsumed = false;

            if (hitBlock != null && hitBlock.id != 0)
            {
                interactionConsumed = hitBlock.OnActivated(hitTarget,state,hitBlock,player);
            }
            
            if (interactionConsumed)
                return;

            if (stack != null && !stack.IsEmpty && stack.Item.isBlock)
            {
                if (!IsInsideOfPlayer(target))
                {
                    ModifyBlock(target,stack.Item.blockId, Vector3Int.RoundToInt(lastHit.normal));

                    inventory.RemoveItemFromSlot(hotBarUI.GetSelectedSlot(), 1);
                }
            }
        }
    }
    
    private void ModifyBlock(Vector3Int worldPos, byte id, Vector3Int? placementFace = null)
    {
        if (id == BlockDataBase.ScaffoldingBlock.id)
        {
            ScaffoldingBlock.SetPlacementHeight(scaffoldingPlacmentHeight);
            ScaffoldingBlock.SetPlacementMode(blockPlacementMode);
        }
        
        chunkManager.SetBlockAtWorldPos(worldPos, id, placementFace);
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
    
    private Vector3Int GetTargetBlockPos(RaycastHit hit, bool place)
    {
        const float mineOffset = 0.01f;
        Vector3 pos = hit.point - hit.normal * mineOffset;
        Vector3Int hitBlockPos = Vector3Int.FloorToInt(pos);
        
        if (!place)
            return hitBlockPos;

        Vector3Int placeOffset = new Vector3Int(
            Mathf.RoundToInt(hit.normal.x),
            Mathf.RoundToInt(hit.normal.y),
            Mathf.RoundToInt(hit.normal.z));

        
        return hitBlockPos + placeOffset;
    }

    private bool IsInsideOfPlayer(Vector3Int blockWorldPos)
    {
        Vector3 blockCenter = blockWorldPos + Vector3.one * 0.5f;
        Vector3 blockExtents = Vector3.one * 0.45f;

        int playerLayer = LayerMask.GetMask("Player");

        return Physics.CheckBox(blockCenter, blockExtents, Quaternion.identity, playerLayer);
    }

    private void PickupItem()
    {
        if(!Physics.Raycast(playerCamera.transform.position,
               playerCamera.transform.forward,
               out RaycastHit hit, pickupDistance))
            return;

        ItemEntity itemEntity = hit.collider.GetComponent<ItemEntity>();
        if(itemEntity == null) return;
        
        //Case one: Holds shift
        if (Input.GetKey(KeyCode.LeftControl))
        {
            TryPickup(itemEntity);
        }
        
        //Case two: Right clicke with empty hand
        if (Input.GetMouseButton(1) && IsHandEmpty())
        {
            TryPickup(itemEntity);
        }
        
    }

    private void TryPickup(ItemEntity itemEntity)
    {
        ItemStack stack = itemEntity.stack;
        
        if(stack == null || stack.IsEmpty) return;

        bool success = inventory.AddItem(stack.itemId, stack.count, stack.displayName, stack.composition);
        if (success)
        {
            Destroy(itemEntity.gameObject);
        }
    }

    private bool IsHandEmpty()
    {
        if (hotBarUI == null) return true;

        ItemStack stack = hotBarUI.GetSelectedStack();
        return stack == null || stack.IsEmpty;
    }

    private bool IsTryingToInteract()
    {
        return Input.GetKey(KeyCode.LeftControl)
               || (Input.GetMouseButtonDown(1) && IsHandEmpty());
    }

    private void HandleScaffoldingHeightHotKeysAndBlockPlacementMode()
    {
        ItemStack stack = hotBarUI != null ? hotBarUI.GetSelectedStack() : null;
        if (stack == null || stack.IsEmpty || !stack.Item.isBlock)
            return;
        
        if (!(stack.Item.blockId == BlockDataBase.ScaffoldingBlock.id || stack.Item.blockId == BlockDataBase.SlabBlock.id))
            return;

        bool changed = false;

        if (Input.GetKeyDown(KeyCode.N))
        {
            scaffoldingPlacmentHeight = Mathf.Min(1f, scaffoldingPlacmentHeight + 0.1f);
            changed = true;
        }
        
        if (Input.GetKeyDown(KeyCode.M))
        {
            scaffoldingPlacmentHeight = Mathf.Max(0.1f, scaffoldingPlacmentHeight - 0.1f);
            changed = true;
        }
        
        if (Input.GetKeyDown(KeyCode.V))
        {
            blockPlacementMode = !blockPlacementMode;
            ScaffoldingBlock.SetPlacementMode(blockPlacementMode);
            SlabBlock.SetPlacementMode(blockPlacementMode);
            Debug.Log($"Scaffolding mode: {(blockPlacementMode ? "vertical" : "flat")}");
        }

        if (changed)
        {
            ScaffoldingBlock.SetPlacementHeight(scaffoldingPlacmentHeight);
            Debug.Log($"Scaffolding height: {scaffoldingPlacmentHeight:0.##}");
        }
    }
}
