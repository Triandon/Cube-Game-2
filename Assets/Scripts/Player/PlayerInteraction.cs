using Core.Item;
using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction")] 
    public Camera playerCamera;
    public float pickupDistance = 3f;

    private Inventory inventory;
    private HotBarUI hotBarUI;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        inventory = GetComponent<InventoryHolder>().Inventory;
        hotBarUI = FindAnyObjectByType<HotBarUI>();
    }

    // Update is called once per frame
    void Update()
    {
        if(!IsTryingToInteract())
            return;
        
        PickupItem();
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

        bool success = inventory.AddItem(stack.itemId, stack.count, stack.displayName);
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
