using System;
using Core.Item;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    public ItemStack[] slots = new ItemStack[3];

    private void Awake()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = new ItemStack(0,0);
        }
    }

    public bool AddItem(int itemId, int amount)
    {
        //Fllis existing stacks
        for (int i = 0; i < slots.Length && amount > 0; i++)
        {
            if (slots[i].itemId == itemId)
            {
                amount = slots[i].AddItemToStack(amount);
            }
        }
        
        //Fill empty slots
        for (int i = 0; i < slots.Length && amount > 0; i++)
        {
            if (slots[i].IsEmpty)
            {
                slots[i].itemId = itemId;
                amount = slots[i].AddItemToStack(amount);
            }
        }

        return amount == 0;
    }

    public bool RemoveItem(int itemId, int amount)
    {
        for (int i = 0; i < slots.Length && amount > 0; i++)
        {
            if (slots[i].itemId == itemId)
            {
                amount = slots[i].RemoveItemToStack(amount);
            }
        }

        return amount == 0;
    }
}
