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
            BlockStateContainer state = chunkManager.GetBlockStateAtWorldPos(target);

            if (hitBlock != null && hitBlock.id != 0)
            {
                hitBlock?.OnActivated(hitTarget,state,hitBlock,player);
            }

            if (stack != null && !stack.IsEmpty && stack.Item.isBlock)
            {
                if (!IsInsideOfPlayer(target))
                {
                    ModifyBlock(target,stack.Item.blockId);

                    inventory.RemoveItemFromSlot(hotBarUI.GetSelectedSlot(), 1);
                }
            }
        }
    }
    
    private void ModifyBlock(Vector3Int worldPos, byte id)
    {
        chunkManager.SetBlockAtWorldPos(worldPos, id);
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

        bool success = inventory.AddItem(stack.itemId, stack.count, stack.displayName, null);
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
}
