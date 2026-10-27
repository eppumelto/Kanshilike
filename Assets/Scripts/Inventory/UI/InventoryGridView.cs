using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Draws an Inventory's grid and its stacks, and shows where a dragged item would land.</summary>
public class InventoryGridView : MonoBehaviour
{
	public const float CellSize = 48f;

	private InventoryWindow window;
	private Inventory inventory;
	private RectTransform itemLayer;
	private Image preview;
	private readonly List<InventoryItemView> items = new List<InventoryItemView>();

	public Inventory Inventory => inventory;
	public InventoryWindow Window => window;
	public RectTransform ItemLayer => itemLayer;
	public RectTransform Rect => (RectTransform)transform;

	public static Vector2 PixelSize(Inventory inventory) => new Vector2(inventory.Width, inventory.Height) * CellSize;

	public static InventoryGridView Create(InventoryWindow window, Transform parent, Inventory inventory, Vector2 topLeft)
	{
		Vector2 size = PixelSize(inventory);
		var background = UIFactory.Panel("Grid", parent, topLeft, size, UIFactory.CellLineColor);

		// Cells: one image each, inset so the background shows through as grid lines.
		for (int y = 0; y < inventory.Height; y++)
			for (int x = 0; x < inventory.Width; x++)
				UIFactory.Panel("Cell", background.transform, new Vector2(x * CellSize + 1f, y * CellSize + 1f),
					new Vector2(CellSize - 2f, CellSize - 2f), UIFactory.CellColor, raycast: false);

		var view = background.gameObject.AddComponent<InventoryGridView>();
		view.window = window;
		view.inventory = inventory;
		view.itemLayer = UIFactory.Rect("Items", background.transform, Vector2.zero, size);
		view.preview = UIFactory.Panel("DropPreview", background.transform, Vector2.zero, new Vector2(CellSize, CellSize), UIFactory.ValidColor, raycast: false);
		view.preview.gameObject.SetActive(false);

		inventory.OnInventoryChanged += view.Rebuild;
		view.Rebuild();
		return view;
	}

	private void OnDestroy()
	{
		if (inventory != null)
			inventory.OnInventoryChanged -= Rebuild;
	}

	public void Rebuild()
	{
		foreach (var item in items)
			if (item != null)
				Destroy(item.gameObject);
		items.Clear();

		foreach (var entry in inventory.Entries)
			items.Add(InventoryItemView.Create(this, entry, CellSize));
	}

	/// <summary>
	/// Top-left cell an item of <paramref name="size"/> would occupy if dropped with its centre
	/// at <paramref name="screenPos"/>.
	/// </summary>
	public Vector2Int CellFor(Vector2 screenPos, Vector2Int size)
	{
		RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, screenPos, null, out Vector2 local);
		// Pivot is the top-left corner: x grows right, y grows down (negative local y).
		float cx = local.x / CellSize;
		float cy = -local.y / CellSize;
		return new Vector2Int(Mathf.RoundToInt(cx - size.x * 0.5f), Mathf.RoundToInt(cy - size.y * 0.5f));
	}

	/// <summary>The cell directly under the pointer (for dropping onto a stack).</summary>
	public Vector2Int CellUnder(Vector2 screenPos)
	{
		RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, screenPos, null, out Vector2 local);
		return new Vector2Int(Mathf.FloorToInt(local.x / CellSize), Mathf.FloorToInt(-local.y / CellSize));
	}

	public void ShowPreview(Vector2Int cell, Vector2Int size, bool valid)
	{
		var rect = preview.rectTransform;
		rect.anchoredPosition = new Vector2(cell.x * CellSize, -cell.y * CellSize);
		rect.sizeDelta = (Vector2)size * CellSize;
		preview.color = valid ? UIFactory.ValidColor : UIFactory.InvalidColor;
		preview.gameObject.SetActive(true);
		preview.transform.SetAsLastSibling();
	}

	public void HidePreview() => preview.gameObject.SetActive(false);
}
