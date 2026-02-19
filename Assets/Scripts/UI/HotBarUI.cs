using System;
using Core.Item;
using TMPro;
using UnityEngine;

public class HotBarUI : InventoryViewManager
{
    [SerializeField] private int selectedSlot;
    public TMP_InputField chatBox;

    [SerializeField] private GameObject itemEntityPref;
    [SerializeField] private Transform playerPos;
    [SerializeField] private GameObject emptyGoItemListThing;

    private void Update()
    {
        if (!chatBox.isFocused)
        {
            float scroll = Input.mouseScrollDelta.y;
            if (scroll != 0)
            {
                selectedSlot -= (int)Mathf.Sign(scroll);
                selectedSlot = Mathf.Clamp(selectedSlot, 0, slots.Count - 1);
                UpdateSelection();
            }
        
            if(Input.GetKeyDown(KeyCode.Alpha1)) Select(0);
            if(Input.GetKeyDown(KeyCode.Alpha2)) Select(1);
            if(Input.GetKeyDown(KeyCode.Alpha3)) Select(2);
            if(Input.GetKeyDown(KeyCode.Alpha4)) Select(3);
            if(Input.GetKeyDown(KeyCode.Alpha5)) Select(4);

            if (Input.GetKeyDown(KeyCode.Q))
            {
                TryDropItem();
            }
        }
        
    }

    private void Select(int index)
    {
        if (index < slots.Count)
        {
            selectedSlot = index;
            UpdateSelection();
        }
    }

    private void UpdateSelection()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            slots[i].SetSelected(i == selectedSlot);
        }
    }

    public ItemStack GetSelectedStack()
    {
        if (inventory != null && selectedSlot >= 0 && selectedSlot < inventory.Size)
        {
            return inventory.slots[selectedSlot];
        }

        return ItemStack.Empty;
    }

    public int GetSelectedSlot()
    {
        return selectedSlot;
    }

    private void TryDropItem()
    {
        //Cursor has item -> drop cursor stack
        if (cursor.HasItem)
        {
            DropItem(cursor.CursorStack);
            cursor.CursorStack.count--;
            
            if(cursor.CursorStack.count <= 0)
                cursor.ClearCursor();
            
            return;
        }
        
        //Cursor empty -> drop selected stack in hotbar
        ItemStack stack = GetSelectedStack();
        if(stack.IsEmpty)
            return;
        
        DropItem(stack);
        stack.count--;

        if (stack.count <= 0)
            inventory.slots[selectedSlot] = ItemStack.Empty;
        
        inventory.InventoryChanged();
    }

    private void DropItem(ItemStack stack)
    {
        Vector3 spawnPos =
            playerPos.position +
            playerPos.forward * 1.2f +
            Vector3.up * 0.5f;

        GameObject go = Instantiate(itemEntityPref, spawnPos, Quaternion.identity,emptyGoItemListThing.transform);
        go.GetComponent<ItemEntity>().Init(
            new ItemStack(stack.itemId, 1, stack.displayName, stack.composition));
    }
     
}
