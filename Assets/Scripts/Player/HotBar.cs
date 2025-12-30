using System;
using Core.Item;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;

public class HotBar : MonoBehaviour
{
    [SerializeField] private InventoryHolder playerInventoryHolder;
    
    private Inventory inventory;
    public int selectedSlot = 0;
    public Image[] slots;

    public TextMeshProUGUI[] slotsCount;

    private void Start()
    {
        inventory = playerInventoryHolder.Inventory;
        inventory.OnInventoryChanged += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (inventory != null)
        {
            inventory.OnInventoryChanged -= Refresh;
        }
    }
    private void Refresh()
    {
        UpdateIcons();
        UpdateItemStackCount();
    }
    
    // Update is called once per frame
    void Update()
    {
        // scroll wheel selection
        float scroll = Input.mouseScrollDelta.y;
        if (scroll != 0)
        {
            selectedSlot -= (int)Mathf.Sign(scroll);
            selectedSlot = Mathf.Clamp(selectedSlot, 0, 2);
        }
        
        // number keys (1,2,3)
        if (Input.GetKeyDown(KeyCode.Alpha1)) selectedSlot = 0;
        if (Input.GetKeyDown(KeyCode.Alpha2)) selectedSlot = 1;
        if (Input.GetKeyDown(KeyCode.Alpha3)) selectedSlot = 2;
        
        UpdateSelectedSlot();
    }

    public ItemStack GetSelectedStack()
    {
        return inventory.slots[selectedSlot];
    }

    private void UpdateSelectedSlot()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].color =
                (i == selectedSlot) ? Color.gray3 : Color.white;
        }
    }


    private void UpdateIcons()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            ItemStack stack = inventory.slots[i];

            if (!stack.IsEmpty && stack.Item.textureIndex != -1)
            {
                slots[i].sprite = ItemRegistry.GetItemSprite(stack.itemId);
            }
            else
            {
                slots[i].sprite = null;
            }
        }
    }

    private void UpdateItemStackCount()
    {
        for (int i = 0; i < slotsCount.Length; i++)
        {
            int count = inventory.slots[i].count;

            if (count > 0)
            {
                slotsCount[i].text = $"{count}";
            }
            else
            {
                slotsCount[i].text = "";
            }

            
        }
    }

}
