using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Generic equipment slot UI (Hand, Shield, Helmet, etc.).
/// One instance per equipment slot panel — set Slot Type in the Inspector.
/// </summary>
public class EquipSlotUI : MonoBehaviour, IPointerClickHandler
{
	[SerializeField] private EquipSlot slotType;
	[SerializeField] private EquipmentManager equipment;
	[SerializeField] private InventoryUI inventoryUI;
	[SerializeField] private Image iconImage;
	[SerializeField] private Image slotBackground;

	[Header("Colors")]
	[SerializeField] private Color normalColor = Color.white;
	[SerializeField] private Color selectedColor = new Color(1f, 0.85f, 0.3f);

	private void Start()
	{
		SetEquipmentManager(equipment);
		Refresh();
	}

	private void OnDestroy()
	{
		SetEquipmentManager(null);
	}

	public void SetEquipmentManager(EquipmentManager manager)
	{
		if (equipment == manager)
			return;

		if (equipment != null)
			equipment.OnEquipChanged -= Refresh;

		equipment = manager;

		if (equipment != null)
			equipment.OnEquipChanged += Refresh;

		Refresh();
	}

	public void SetSelected(bool selected)
	{
		if (slotBackground != null)
			slotBackground.color = selected ? selectedColor : normalColor;
	}

	public void OnPointerClick(PointerEventData eventData)
	{
		if (equipment == null || inventoryUI == null) return;
		if (equipment.GetEquipped(slotType) == null) return;
		inventoryUI.SelectEquipSlot(slotType);
		SetSelected(true);
	}

	private void Refresh()
	{
		SetSelected(false);

		if (equipment == null)
		{
			iconImage.sprite = null;
			iconImage.color = Color.clear;
			return;
		}

		Item equipped = equipment.GetEquipped(slotType);
		if (equipped != null && equipped.icon != null)
		{
			iconImage.sprite = equipped.icon;
			iconImage.color = Color.white;
		}
		else
		{
			iconImage.sprite = null;
			iconImage.color = Color.clear;
		}
	}
}
