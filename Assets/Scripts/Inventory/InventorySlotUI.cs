using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class InventorySlotUI : MonoBehaviour, IPointerClickHandler
{
	[SerializeField] private Image iconImage;
	[SerializeField] private Image slotBackground;
	[Tooltip("Shows the stack size. Created automatically in the bottom-right corner if left empty.")]
	[SerializeField] private TMP_Text countText;

	[Header("Colors")]
	[SerializeField] private Color normalColor = Color.white;
	[SerializeField] private Color selectedColor = new Color(1f, 0.85f, 0.3f);

	private Item currentItem;
	private int slotIndex;
	private EquipmentManager equipment;

	public System.Action<InventorySlotUI> OnClicked;
	public Item CurrentItem => currentItem;

	public void Refresh(Item item, int count, int index, EquipmentManager equipmentManager)
	{
		if (countText == null)
			countText = CreateCountText();

		currentItem = item;
		slotIndex = index;
		equipment = equipmentManager;

		// Equipped items live in their equip slot — hide them here
		bool isEquipped = equipmentManager != null && equipmentManager.IsInventorySlotEquipped(index);
		Item displayItem = isEquipped ? null : item;

		if (displayItem != null && displayItem.icon != null)
		{
			iconImage.sprite = displayItem.icon;
			iconImage.color = Color.white;
		}
		else
		{
			iconImage.sprite = null;
			iconImage.color = Color.clear;
		}

		if (countText != null)
			countText.text = displayItem != null && count > 1 ? count.ToString() : string.Empty;

		if (slotBackground != null)
			slotBackground.color = normalColor;
	}

	public void SetSelected(bool selected)
	{
		if (slotBackground != null)
			slotBackground.color = selected ? selectedColor : normalColor;
	}

	public void OnPointerClick(PointerEventData eventData)
	{
		// Ignore click on empty slots or currently equipped slots
		if (currentItem == null) return;
		if (equipment != null && equipment.IsInventorySlotEquipped(slotIndex)) return;
		OnClicked?.Invoke(this);
	}

	private TMP_Text CreateCountText()
	{
		var go = new GameObject("Count", typeof(RectTransform));
		go.transform.SetParent(transform, false);

		var rect = (RectTransform)go.transform;
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = new Vector2(4f, 2f);
		rect.offsetMax = new Vector2(-4f, -2f);

		var text = go.AddComponent<TextMeshProUGUI>();
		text.alignment = TextAlignmentOptions.BottomRight;
		text.fontSize = 18f;
		text.fontStyle = FontStyles.Bold;
		text.color = Color.white;
		text.outlineWidth = 0.2f;
		text.outlineColor = Color.black;
		text.raycastTarget = false; // clicks go to the slot
		return text;
	}
}
