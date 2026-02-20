using System;
using System.Collections.Generic;
using Core.Item;
using Unity.VisualScripting;
using UnityEngine;

public class InventoryViewManager : MonoBehaviour
{
    [SerializeField] private InventorySlotUI slotPrefab;
    [SerializeField] private Transform slotParent;
    [SerializeField] public InventoryCursor cursor;
    private TooltipUI tooltipUI;
    
    [Serializable]
    public class  SlotLayoutBinding
    {
        [Min(0)] public int inventoryIndex;
        public Transform parent;
    }

    [SerializeField] private List<SlotLayoutBinding> fixedSlotLayout = new();

    protected Inventory inventory;
    protected readonly List<InventorySlotUI> slots = new();
    protected readonly Dictionary<int, InventorySlotUI> slotByIndex = new Dictionary<int, InventorySlotUI>();
    
    protected ItemStack cursorStack = ItemStack.Empty;

    protected void OnEnable()
    {
        InventoryHolder.OnInventoryDisplayRequested += OnInventoryRequested;
    }

    protected void OnDisable()
    {
        InventoryHolder.OnInventoryDisplayRequested -= OnInventoryRequested;

        if (inventory != null)
            inventory.OnInventoryResized -= RebuildSlots;
        
        cursor.ClearCursor();
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.OnInventoryResized -= RebuildSlots;
    }

    protected virtual void OnInventoryRequested(Inventory inv, InventoryHolder holder)
    {
        if(this is HotBarUI && holder is not IPlayerInventory)
            return;

        if (inventory != null)
            inventory.OnInventoryResized -= RebuildSlots;
        
        inventory = inv;

        inventory.OnInventoryResized += RebuildSlots;
        BuildSlots();
    }

    protected void BuildSlots()
    {
        foreach (Transform child in slotParent)
        {
            Destroy(child.gameObject);
        }

        foreach (SlotLayoutBinding binding in fixedSlotLayout)
        {
            if (binding?.parent == null || binding.parent == slotParent)
            {
                continue;
            }

            foreach (Transform child in binding.parent)
            {
                Destroy(child.gameObject);
            }
        }

        slots.Clear();
        slotByIndex.Clear();
        
        bool usesFixedLayout = TryBuildFixedLayout();
        if (usesFixedLayout)
        {
            return;
        }
            
        for (int i = 0; i < inventory.Size; i++)
        {
            CreateSlot(i, slotParent);
        }
    }

    private bool TryBuildFixedLayout()
    {
        if (fixedSlotLayout == null || fixedSlotLayout.Count == 0)
        {
            return false;
        }

        HashSet<int> usedIndexes = new HashSet<int>();
        bool hasAtLeastOneValidBinding = false;

        foreach (SlotLayoutBinding binding in fixedSlotLayout)
        {
            if (binding == null)
            {
                continue;
            }

            if (binding.inventoryIndex < 0 || binding.inventoryIndex >= inventory.Size)
            {
                Debug.LogWarning($"Ignored slot layout binding with out-of-range index {binding.inventoryIndex} on {name}");
                continue;
            }

            if (!usedIndexes.Add(binding.inventoryIndex))
            {
                Debug.LogWarning($"Duplicate slot layout binding for index {binding.inventoryIndex} on {name}");
                continue;
            }

            Transform targetParent = binding.parent != null ? binding.parent : slotParent;
            CreateSlot(binding.inventoryIndex, targetParent);
            hasAtLeastOneValidBinding = true;
        }

        if (!hasAtLeastOneValidBinding)
        {
            return false;
        }

        for (int i = 0; i < inventory.Size; i++)
        {
            if (usedIndexes.Contains(i))
            {
                continue;
            }

            CreateSlot(i, slotParent);
        }

        return true;
    }

    private void CreateSlot(int slotIndex, Transform targetParent)
    {
        InventorySlotUI slot = Instantiate(slotPrefab, targetParent);
        slot.Init(inventory, slotIndex, this);
        
        slots.Add(slot);
        slotByIndex[slotIndex] = slot;
    }

    public virtual string GetSlotCountText(int slotIndex, ItemStack stack)
    {
        return stack.count > 1 ? stack.count.ToString() : "";
    }

    protected void RebuildSlots()
    {
        BuildSlots();
    }

    public virtual void SlotClicked(InventorySlotUI clickedSlot)
    {
        cursor.HandleSlotClick(inventory, clickedSlot.SlotIndex);
        tooltipUI?.Hide();
    }
    
    public virtual void SlotRightClicked(InventorySlotUI clickedSlot)
    {
        cursor.HandleSlotRightClick(inventory, clickedSlot.SlotIndex);
        tooltipUI?.Hide();
    }

    public void ShowTooltip(ItemStack stack)
    {
        if(!cursor.CursorStack.IsEmpty)
            return;
        
        tooltipUI?.Show(stack);
    }

    public void HideToolTip()
    {
        tooltipUI?.Hide();
    }

    private void Awake()
    {
        if (tooltipUI == null)
            tooltipUI = FindObjectOfType<TooltipUI>(true);
    }
}
