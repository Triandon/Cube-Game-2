using System;
using Core.Item;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventorySlotUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI countText;
    [SerializeField] private Image background;

    private Inventory inventory;
    private int slotIndex;
    private InventoryViewManager owner;
        
    public void Init(Inventory inventory, int slotIndex, InventoryViewManager owner)
    {
        this.inventory = inventory;
        this.slotIndex = slotIndex;
        this.owner = owner;

        gameObject.name = $"Slot_{slotIndex}_{owner}";

        inventory.OnInventoryChanged += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.OnInventoryChanged -= Refresh;
    }

    public void SetSelected(bool selected)
    {
        background.color = selected ? Color.black : Color.gray4;
    }

    private void Refresh()
    {
        if (inventory == null)
            return;
        if (slotIndex < 0 || slotIndex >= inventory.Size)
            return;
        
        ItemStack stack = inventory.slots[slotIndex];
        if(icon.color != Color.white)
            icon.color = Color.white;

        if (!stack.IsEmpty && stack.Item.textureIndex != -1)
        {
            icon.sprite = ItemRegistry.GetItemSprite(stack.itemId);
            icon.enabled = true;
        }
        else
        {
            icon.sprite = null;
            icon.enabled = false;
        }

        countText.text = stack.count > 1 ? stack.count.ToString() : "";
    }
    
    public void OnUISlotClicked()
    {
        owner?.SlotClicked(this);
    }

    public int SlotIndex => slotIndex;

    public ItemStack GetItemStackFromUISlot()
    {
        return inventory.slots[slotIndex];
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            owner?.SlotRightClicked(this);
        }
    }
}
