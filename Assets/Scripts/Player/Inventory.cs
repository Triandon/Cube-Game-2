using System;
using Core.Item;
using UnityEngine;

[System.Serializable]
public class Inventory
{
    public ItemStack[] slots;
    public int Size => slots.Length;

    public event Action OnInventoryChanged;

    public Inventory(int size)
    {
        slots = new ItemStack[size];
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = new ItemStack(0,0,"");
        }
    }

    public bool AddItem(int itemId, int amount, string displayName)
    {
        //Fllis existing stacks
        for (int i = 0; i < slots.Length && amount > 0; i++)
        {
            if (slots[i].itemId == itemId)
            {
                amount = slots[i].AddItemToStack(amount);
                slots[i].displayName = displayName;
            }
        }
        
        //Fill empty slots
        for (int i = 0; i < slots.Length && amount > 0; i++)
        {
            if (slots[i].IsEmpty)
            {
                slots[i].itemId = itemId;
                amount = slots[i].AddItemToStack(amount);
                slots[i].displayName = displayName;
            }
        }

        OnInventoryChanged?.Invoke();
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

        OnInventoryChanged?.Invoke();
        return amount == 0;
    }
    
    public bool RemoveItemFromSlot(int slotIndex, int amount)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return false;

        amount = slots[slotIndex].RemoveItemToStack(amount);

        OnInventoryChanged?.Invoke();
        return amount == 0;
    }

    public void InventoryChanged()
    {
        OnInventoryChanged?.Invoke();
    }

}
