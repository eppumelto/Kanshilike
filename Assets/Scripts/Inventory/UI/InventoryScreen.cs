using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Kenshi-style inventory windows. Creates its own canvas on first use, so a scene only needs
/// an EventSystem (one is created if missing).
///
///   I            open/close inventories of the selected characters (side by side)
///   drag         move items within or between windows, onto equipment slots
///   R            rotate the item being dragged
///   right-click  use / equip (character) or take (loot window); unequip on a slot
///   Esc          close all
/// </summary>
public class InventoryScreen : MonoBehaviour
{
	private const int MaxCharacterWindows = 3;
	private const float WindowGap = 16f;
	private const float TopMargin = 60f;

	private static InventoryScreen instance;

	private RectTransform windowLayer;
	private RectTransform dragLayer;
	private readonly List<InventoryWindow> windows = new List<InventoryWindow>();

	// Current drag: from a grid stack, or from an equipment slot.
	private class Drag
	{
		public Inventory fromInventory;
		public Inventory.Entry entry;
		public EquipmentManager fromEquipment;
		public EquipSlot fromSlot;
		public Inventory slotOwnerInventory;
		public Item item;
		public bool rotated;
		public RectTransform ghost;
		public Vector2 pointer;
		public InventoryGridView previewGrid;
		public EquipmentSlotView previewSlot;
	}

	private Drag drag;

	public static InventoryScreen Instance
	{
		get
		{
			if (instance == null)
				instance = new GameObject("InventoryScreen").AddComponent<InventoryScreen>();
			return instance;
		}
	}

	/// <summary>True if any inventory window is open (without creating the screen).</summary>
	public static bool IsOpen => instance != null && instance.windows.Count > 0;

	public RectTransform WindowLayer => windowLayer;

	private void Awake()
	{
		instance = this;

		var canvas = gameObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 100;
		var scaler = gameObject.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920f, 1080f);
		scaler.matchWidthOrHeight = 0.5f;
		gameObject.AddComponent<GraphicRaycaster>();

		windowLayer = Stretch("Windows");
		dragLayer = Stretch("Drag");

		if (FindFirstObjectByType<EventSystem>() == null)
			new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
	}

	private RectTransform Stretch(string name)
	{
		var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
		rect.SetParent(transform, false);
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = rect.offsetMax = Vector2.zero;
		return rect;
	}

	private void Update()
	{
		if (drag != null)
		{
			if (Input.GetKeyDown(KeyCode.R))
			{
				drag.rotated = !drag.rotated;
				BuildGhost();
				UpdateDrag(drag.pointer);
			}

			// The dragged view can vanish mid-drag (e.g. auto-eat used the stack) and then never
			// gets its end-drag event; don't stay stuck. On the release frame itself, leave it to EndDrag.
			if (!Input.GetMouseButton(0) && !Input.GetMouseButtonUp(0))
				CancelDrag();
		}

		if (windows.Count > 0 && Input.GetKeyDown(KeyCode.Escape))
			CloseAll();
	}

	// ── Windows ─────────────────────────────────────────────────────────────

	/// <summary>I key: closes everything if anything is open, otherwise opens the selected characters' inventories.</summary>
	public void Toggle(IEnumerable<Character> characters)
	{
		if (windows.Count > 0)
		{
			CloseAll();
			return;
		}

		int opened = 0;
		foreach (var character in characters)
		{
			if (opened >= MaxCharacterWindows)
				break;
			if (OpenCharacter(character) != null)
				opened++;
		}
	}

	public InventoryWindow OpenCharacter(Character character)
	{
		if (character == null || !character.IsAlive || character.GetComponent<Inventory>() == null)
			return null;

		var existing = windows.Find(w => w.Character == character);
		if (existing != null)
			return existing;

		var window = InventoryWindow.ForCharacter(this, character);
		windows.Add(window);
		Layout();
		return window;
	}

	/// <summary>Opens a container (corpse) next to the looter's own inventory.</summary>
	public void OpenLoot(Character looter, Inventory container, string title)
	{
		if (looter == null || container == null)
			return;

		OpenCharacter(looter);
		if (windows.Exists(w => w.Inventory == container))
			return;

		windows.Add(InventoryWindow.ForContainer(this, container, title, looter));
		Layout();
	}

	public void Close(InventoryWindow window)
	{
		if (drag != null)
			CancelDrag();
		if (windows.Remove(window) && window != null)
			Destroy(window.gameObject);
		Layout();
	}

	public void CloseAll()
	{
		if (drag != null)
			CancelDrag();
		foreach (var w in windows)
			if (w != null)
				Destroy(w.gameObject);
		windows.Clear();
	}

	/// <summary>Lines the windows up side by side, centred at the top of the screen.</summary>
	private void Layout()
	{
		windows.RemoveAll(w => w == null);

		float total = -WindowGap;
		foreach (var w in windows)
			total += w.Rect.sizeDelta.x + WindowGap;

		float x = (windowLayer.rect.width - total) * 0.5f;
		foreach (var w in windows)
		{
			w.Rect.anchoredPosition = new Vector2(Mathf.Max(0f, x), -TopMargin);
			x += w.Rect.sizeDelta.x + WindowGap;
		}
	}

	// ── Dragging ────────────────────────────────────────────────────────────

	public void BeginDrag(Inventory from, Inventory.Entry entry, Vector2 pointer)
	{
		CancelDrag();
		drag = new Drag { fromInventory = from, entry = entry, item = entry.item, rotated = entry.rotated, pointer = pointer };
		BuildGhost();
		UpdateDrag(pointer);
	}

	public void BeginDragFromSlot(EquipmentManager equipment, EquipSlot slot, Inventory ownerInventory, Vector2 pointer)
	{
		CancelDrag();
		var item = equipment.GetEquipped(slot);
		if (item == null)
			return;

		drag = new Drag { fromEquipment = equipment, fromSlot = slot, slotOwnerInventory = ownerInventory, item = item, pointer = pointer };
		BuildGhost();
		UpdateDrag(pointer);
	}

	public void UpdateDrag(Vector2 pointer)
	{
		if (drag == null)
			return;

		drag.pointer = pointer;
		RectTransformUtility.ScreenPointToLocalPointInRectangle(dragLayer, pointer, null, out Vector2 local);
		drag.ghost.localPosition = local;

		ClearPreview();
		var (grid, slot) = TargetUnder(pointer);
		Vector2Int size = Inventory.SizeOf(drag.item, drag.rotated);

		if (grid != null)
		{
			Vector2Int cell = grid.CellFor(pointer, size);
			var stackUnder = grid.Inventory.GetEntryAt(grid.CellUnder(pointer));
			bool ontoStack = stackUnder != null && stackUnder != drag.entry && stackUnder.item == drag.item && stackUnder.SpaceLeft > 0;
			bool valid = ontoStack || grid.Inventory.CanPlace(drag.item, cell, drag.rotated, drag.entry);
			if (ontoStack)
				grid.ShowPreview(stackUnder.position, stackUnder.Size, true);
			else
				grid.ShowPreview(cell, size, valid);
			drag.previewGrid = grid;
		}
		else if (slot != null)
		{
			bool valid = drag.item is EquippableItem eq && eq.equipSlot == slot.Slot;
			slot.SetHighlight(valid ? UIFactory.ValidColor : UIFactory.InvalidColor);
			drag.previewSlot = slot;
		}
	}

	public void EndDrag(Vector2 pointer)
	{
		if (drag == null)
			return;

		var d = drag;
		var (grid, slot) = TargetUnder(pointer);
		string reason = null;
		InventoryWindow feedback = grid != null ? grid.Window : slot != null ? slot.Window : null;

		if (grid != null)
		{
			Vector2Int size = Inventory.SizeOf(d.item, d.rotated);
			Vector2Int cell = grid.CellFor(pointer, size);

			// Dropping onto a matching stack merges: aim at the stack under the pointer.
			var stackUnder = grid.Inventory.GetEntryAt(grid.CellUnder(pointer));
			if (stackUnder != null && stackUnder != d.entry && stackUnder.item == d.item)
				cell = grid.CellUnder(pointer);

			if (d.entry != null)
				InventoryActions.TryMoveEntry(d.fromInventory, d.entry, grid.Inventory, cell, d.rotated, out reason);
			else
				InventoryActions.TryUnequip(d.fromEquipment, d.fromSlot, grid.Inventory, cell, d.rotated, out reason);
		}
		else if (slot != null && d.entry != null)
		{
			if (d.item is EquippableItem eq && eq.equipSlot != slot.Slot)
				reason = $"{d.item.itemName} doesn't go in {slot.Slot}";
			else
				InventoryActions.TryEquip(d.fromInventory, d.entry, slot.Window.Equipment, slot.Window.Inventory, out reason);
		}

		if (reason != null && feedback != null)
			feedback.ShowMessage(reason);

		CancelDrag();
	}

	private void CancelDrag()
	{
		if (drag == null)
			return;

		ClearPreview();
		if (drag.ghost != null)
			Destroy(drag.ghost.gameObject);
		drag = null;
	}

	private void ClearPreview()
	{
		if (drag == null)
			return;
		if (drag.previewGrid != null)
			drag.previewGrid.HidePreview();
		if (drag.previewSlot != null)
			drag.previewSlot.SetHighlight(null);
		drag.previewGrid = null;
		drag.previewSlot = null;
	}

	/// <summary>A see-through copy of the item that follows the pointer, centred on it.</summary>
	private void BuildGhost()
	{
		if (drag.ghost != null)
			Destroy(drag.ghost.gameObject);

		Vector2 size = (Vector2)Inventory.SizeOf(drag.item, drag.rotated) * InventoryGridView.CellSize;
		var ghost = UIFactory.Panel("DragGhost", dragLayer, Vector2.zero, size, new Color(1f, 1f, 1f, 0.12f), raycast: false);
		var rect = ghost.rectTransform;
		rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
		UIFactory.Icon(rect, drag.item, size - new Vector2(10f, 10f), drag.rotated).color = new Color(1f, 1f, 1f, 0.85f);
		drag.ghost = rect;
	}

	private static (InventoryGridView grid, EquipmentSlotView slot) TargetUnder(Vector2 pointer)
	{
		if (EventSystem.current == null)
			return (null, null);

		var results = new List<RaycastResult>();
		EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = pointer }, results);
		foreach (var r in results)
		{
			var slot = r.gameObject.GetComponentInParent<EquipmentSlotView>();
			if (slot != null)
				return (null, slot);
			var grid = r.gameObject.GetComponentInParent<InventoryGridView>();
			if (grid != null)
				return (grid, null);
		}
		return (null, null);
	}
}
