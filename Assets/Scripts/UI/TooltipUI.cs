using System; using Core.Item; using TMPro; using UnityEngine;

public class TooltipUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI stackDisplayName;
    [SerializeField] private Canvas canvas;
    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        gameObject.SetActive(false);
    }

    public void Show(ItemStack stack)
    {
        stackDisplayName.text = stack.displayName;
        gameObject.SetActive(true);
        FollowMouse();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (gameObject.activeSelf) FollowMouse();
    }

    private void FollowMouse()
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas.transform as RectTransform, Input.mousePosition,
            canvas.worldCamera, out Vector2 pos);
        rectTransform.localPosition = pos + new Vector2(16, -16);
    }
}