using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>An equipment slot in a character's window. Drop items on it to equip, drag out or right-click to unequip.</summary>
public class EquipmentSlotView : MonoBehaviour,
	IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
	private InventoryWindow window;
	private EquipSlot slot;
	private Image box;
	private Image icon;
	private Vector2 boxSize;

	public EquipSlot Slot => slot;
	public InventoryWindow Window => window;

	public static EquipmentSlotView Create(InventoryWindow window, Transform parent, EquipSlot slot, Vector2 topLeft, Vector2 size)
	{
		var box = UIFactory.Panel(slot + " Slot", parent, topLeft, size, UIFactory.SlotColor);
		var label = UIFactory.Text("Label", parent, topLeft + new Vector2(0f, size.y), new Vector2(size.x, 18f),
			Label(slot), 14f, TextAlignmentOptions.Center);
		label.color = UIFactory.MutedTextColor;

		var view = box.gameObject.AddComponent<EquipmentSlotView>();
		view.window = window;
		view.slot = slot;
		view.box = box;
		view.boxSize = size;
		view.Refresh();
		return view;
	}

	public void Refresh()
	{
		if (icon != null)
			Destroy(icon.gameObject);

		var item = window.Equipment != null ? window.Equipment.GetEquipped(slot) : null;
		icon = UIFactory.Icon(box.transform, item, boxSize - new Vector2(12f, 12f), false);
	}

	public void SetHighlight(Color? color) => box.color = color ?? UIFactory.SlotColor;

	private static string Label(EquipSlot slot) => slot switch
	{
		EquipSlot.Hand => "Weapon",
		EquipSlot.Chest => "Body",
		_ => slot.ToString()
	};

	public void OnBeginDrag(PointerEventData eventData)
	{
		if (eventData.button == PointerEventData.InputButton.Left && window.Equipment.GetEquipped(slot) != null)
			InventoryScreen.Instance.BeginDragFromSlot(window.Equipment, slot, window.Inventory, eventData.position);
	}

	public void OnDrag(PointerEventData eventData) => InventoryScreen.Instance.UpdateDrag(eventData.position);

	public void OnEndDrag(PointerEventData eventData) => InventoryScreen.Instance.EndDrag(eventData.position);

	public void OnPointerClick(PointerEventData eventData)
	{
		if (eventData.button == PointerEventData.InputButton.Right && !eventData.dragging)
			window.Unequip(slot);
	}

	public void OnPointerEnter(PointerEventData eventData) => window.ShowInfo(window.Equipment.GetEquipped(slot), 1);

	public void OnPointerExit(PointerEventData eventData) => window.ShowInfo(null, 0);
}
