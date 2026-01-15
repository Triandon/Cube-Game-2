using System;
using Core.Item;
using Microsoft.Unity.VisualStudio.Editor;
using TMPro;
using UnityEngine;
using Image = UnityEngine.UI.Image;

public class UIItemCursor : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private Image backGround;
    [SerializeField] private TextMeshProUGUI countText;
    [SerializeField] private Canvas canvas;
    [SerializeField] private InventoryCursor cursor;

    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        SetVisible(false);
    }

    // Update is called once per frame
    void Update()
    {
        ItemStack cursorStack = cursor.CursorStack;

        if (cursorStack.IsEmpty)
        {
            SetVisible(false);
            return;
        }
        
        SetVisible(true);
        FollowMouse();
        UpdateVisual(cursorStack);
    }

    private void FollowMouse()
    {
        Vector2 pos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            Input.mousePosition,
            canvas.worldCamera,
            out pos
        );

        rectTransform.localPosition = pos;
    }
    
    private void UpdateVisual(ItemStack stack)
    {
        icon.sprite = ItemRegistry.GetItemSprite(stack.itemId);
        icon.enabled = true;
        backGround.enabled = true;

        countText.text = stack.count > 1 ? stack.count.ToString() : "";
    }

    private void SetVisible(bool visible)
    {
        if (icon.enabled != visible)
        {
            icon.enabled = visible;
            backGround.enabled = visible;
        }
            

        if (!visible)
            countText.text = "";
    }
}
