using Core;
using Core.Block;
using Core.Item;
using Player;
using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction")] 
    public Camera playerCamera;
    public float pickupDistance = 3f;

    private Inventory inventory;
    private HotBarUI hotBarUI;
    private PlayerEntity playerEntity;

    private ChunkManager chunkManager;
    private Transform player;

    private float scaffoldingPlacmentHeight = 1f;
    private bool blockPlacementMode;
    
    //Hits rays
    public float maxDistance = 6.0f;
    private bool hasHit;
    private Vector3Int lastHitBlockPos;
    private Vector3Int lastHitNormal;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        chunkManager = FindAnyObjectByType<ChunkManager>();
        inventory = GetComponent<InventoryHolder>().Inventory;
        hotBarUI = FindAnyObjectByType<HotBarUI>();
        playerEntity = GetComponent<PlayerEntity>();
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
            Vector3Int target = lastHitBlockPos;
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
            Vector3Int target = lastHitBlockPos + lastHitNormal;
            ItemStack stack = playerEntity.GetHeldItemStack();

            Vector3Int hitTarget = lastHitBlockPos;
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
                    ModifyBlock(target,stack.Item.blockId, lastHitNormal);

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
        hasHit = TryGetBlockHit(ray.origin, ray.direction, maxDistance, out lastHitBlockPos, out lastHitNormal);
    }
    
    private bool TryGetBlockHit(Vector3 origin, Vector3 direction, float distance, out Vector3Int hitBlockPos, out Vector3Int hitNormal)
    {
        hitBlockPos = default;
        hitNormal = default;
        
        if (chunkManager == null || direction.sqrMagnitude < 0.000001f)
            return false;

        Vector3 dir = direction.normalized;
        Vector3Int current = Vector3Int.FloorToInt(origin);

        int stepX = dir.x >= 0f ? 1 : -1;
        int stepY = dir.y >= 0f ? 1 : -1;
        int stepZ = dir.z >= 0f ? 1 : -1;

        float tDeltaX = dir.x == 0f ? float.PositiveInfinity : Mathf.Abs(1f / dir.x);
        float tDeltaY = dir.y == 0f ? float.PositiveInfinity : Mathf.Abs(1f / dir.y);
        float tDeltaZ = dir.z == 0f ? float.PositiveInfinity : Mathf.Abs(1f / dir.z);

        float nextBoundaryX = current.x + (stepX > 0 ? 1f : 0f);
        float nextBoundaryY = current.y + (stepY > 0 ? 1f : 0f);
        float nextBoundaryZ = current.z + (stepZ > 0 ? 1f : 0f);

        float tMaxX = dir.x == 0f ? float.PositiveInfinity : Mathf.Abs((nextBoundaryX - origin.x) / dir.x);
        float tMaxY = dir.y == 0f ? float.PositiveInfinity : Mathf.Abs((nextBoundaryY - origin.y) / dir.y);
        float tMaxZ = dir.z == 0f ? float.PositiveInfinity : Mathf.Abs((nextBoundaryZ - origin.z) / dir.z);

        if (chunkManager.GetBlockAtWorldPos(current) != 0)
        {
            hitBlockPos = current;
            hitNormal = -Vector3Int.RoundToInt(dir);
            return true;
        }

        float traveled = 0f;
        while (traveled <= distance)
        {
            if (tMaxX < tMaxY && tMaxX < tMaxZ)
            {
                current.x += stepX;
                traveled = tMaxX;
                tMaxX += tDeltaX;
                hitNormal = new Vector3Int(-stepX, 0, 0);
            }
            else if (tMaxY < tMaxZ)
            {
                current.y += stepY;
                traveled = tMaxY;
                tMaxY += tDeltaY;
                hitNormal = new Vector3Int(0, -stepY, 0);
            }
            else
            {
                current.z += stepZ;
                traveled = tMaxZ;
                tMaxZ += tDeltaZ;
                hitNormal = new Vector3Int(0, 0, -stepZ);
            }

            if (traveled > distance)
                break;

            if (chunkManager.GetBlockAtWorldPos(current) != 0)
            {
                hitBlockPos = current;
                return true;
            }
        }

        return false;
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
