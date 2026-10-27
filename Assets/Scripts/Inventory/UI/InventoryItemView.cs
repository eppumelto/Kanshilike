using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>One stack drawn on an inventory grid. Drag to move, right-click to use.</summary>
public class InventoryItemView : MonoBehaviour,
	IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
	private InventoryGridView grid;
	private Inventory.Entry entry;
	private Image background;

	public Inventory.Entry Entry => entry;

	public static InventoryItemView Create(InventoryGridView grid, Inventory.Entry entry, float cell)
	{
		Vector2 size = (Vector2)entry.Size * cell;
		var background = UIFactory.Panel(entry.item != null ? entry.item.itemName : "Item", grid.ItemLayer,
			new Vector2(entry.position.x * cell + 2f, entry.position.y * cell + 2f), size - new Vector2(4f, 4f), UIFactory.ItemColor);

		UIFactory.Icon(background.transform, entry.item, size - new Vector2(10f, 10f), entry.rotated);

		if (entry.count > 1)
		{
			var count = UIFactory.Text("Count", background.transform, Vector2.zero, size - new Vector2(8f, 6f),
				entry.count.ToString(), 18f, TextAlignmentOptions.BottomRight);
			count.fontStyle = FontStyles.Bold;
			count.outlineWidth = 0.25f;
			count.outlineColor = Color.black;
		}

		var view = background.gameObject.AddComponent<InventoryItemView>();
		view.grid = grid;
		view.entry = entry;
		view.background = background;
		return view;
	}

	public void OnBeginDrag(PointerEventData eventData)
	{
		if (eventData.button != PointerEventData.InputButton.Left)
			return;

		InventoryScreen.Instance.BeginDrag(grid.Inventory, entry, eventData.position);
		background.color = new Color(1f, 1f, 1f, 0.15f);
	}

	public void OnDrag(PointerEventData eventData) => InventoryScreen.Instance.UpdateDrag(eventData.position);

	public void OnEndDrag(PointerEventData eventData)
	{
		if (background != null)
			background.color = UIFactory.ItemColor;
		InventoryScreen.Instance.EndDrag(eventData.position);
	}

	public void OnPointerClick(PointerEventData eventData)
	{
		if (eventData.button == PointerEventData.InputButton.Right && !eventData.dragging)
			grid.Window.UseItem(entry);
	}

	public void OnPointerEnter(PointerEventData eventData) => grid.Window.ShowInfo(entry.item, entry.count);

	public void OnPointerExit(PointerEventData eventData) => grid.Window.ShowInfo(null, 0);
}
