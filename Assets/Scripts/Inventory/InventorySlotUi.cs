using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class InventorySlotUI : MonoBehaviour, IPointerClickHandler
{
	[SerializeField] private Image iconImage;
	[SerializeField] private Image slotBackground;

	[Header("Colors")]
	[SerializeField] private Color normalColor = Color.white;
	[SerializeField] private Color selectedColor = new Color(1f, 0.85f, 0.3f);

	private Item currentItem;
	private int slotIndex;
	private EquipmentManager equipment;

	public System.Action<InventorySlotUI> OnClicked;
	public Item CurrentItem => currentItem;

	public void Refresh(Item item, int index, EquipmentManager equipmentManager)
	{
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
}
