using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// One inventory window: a character's (equipment + grid + weight) or a loot container's
/// (grid only; right-click moves items to the looter).
/// </summary>
public class InventoryWindow : MonoBehaviour
{
	private const float Pad = 14f;
	private const float HeaderHeight = 40f;
	private const float FooterHeight = 34f;
	private const float SlotSize = 80f;
	private const float SlotGap = 10f;
	private const float SlotRowHeight = SlotSize + 24f;
	private const float MinWidth = 480f;

	// Paper-doll layout of the equipment column: (slot, column, row).
	private static readonly (EquipSlot slot, int col, int row)[] SlotLayout =
	{
		(EquipSlot.Helmet, 0, 0),
		(EquipSlot.Hand, 0, 1), (EquipSlot.Shield, 1, 1),
		(EquipSlot.Chest, 0, 2), (EquipSlot.Gloves, 1, 2),
		(EquipSlot.Legs, 0, 3), (EquipSlot.Shoes, 1, 3),
	};

	private InventoryScreen screen;
	private Inventory inventory;
	private Character character;   // owner, for character windows
	private Character looter;      // who is looting, for container windows
	private EquipmentManager equipment;
	private InventoryGridView grid;
	private readonly List<EquipmentSlotView> slots = new List<EquipmentSlotView>();
	private TextMeshProUGUI weightText;
	private TextMeshProUGUI infoText;
	private float messageUntil;
	private float nextWeightRefresh;

	public Inventory Inventory => inventory;
	public Character Character => character;
	public Character Looter => looter;
	public EquipmentManager Equipment => equipment;
	public InventoryGridView Grid => grid;
	public IReadOnlyList<EquipmentSlotView> Slots => slots;
	public bool IsLootWindow => character == null;
	public RectTransform Rect => (RectTransform)transform;

	public static InventoryWindow ForCharacter(InventoryScreen screen, Character character)
	{
		return Build(screen, character.GetComponent<Inventory>(), character.characterName,
			character.GetComponent<EquipmentManager>(), character, null);
	}

	public static InventoryWindow ForContainer(InventoryScreen screen, Inventory inventory, string title, Character looter)
	{
		return Build(screen, inventory, title, null, null, looter);
	}

	private static InventoryWindow Build(InventoryScreen screen, Inventory inventory, string title, EquipmentManager equipment,
		Character character, Character looter)
	{
		Vector2 gridSize = InventoryGridView.PixelSize(inventory);
		bool hasEquipment = equipment != null;
		float equipWidth = hasEquipment ? SlotSize * 2f + SlotGap + Pad : 0f;
		float bodyHeight = Mathf.Max(gridSize.y, hasEquipment ? SlotRowHeight * 4f : 0f);
		// Small containers (a wolf's 4x3) still need room for the title, weight and hint.
		Vector2 size = new Vector2(Mathf.Max(MinWidth, Pad + equipWidth + gridSize.x + Pad), HeaderHeight + Pad + bodyHeight + FooterHeight);

		var root = UIFactory.Panel(title + " Inventory", screen.WindowLayer, Vector2.zero, size, UIFactory.WindowColor);
		var window = root.gameObject.AddComponent<InventoryWindow>();
		window.screen = screen;
		window.inventory = inventory;
		window.equipment = equipment;
		window.character = character;
		window.looter = looter;

		// Header: title, weight, close button.
		UIFactory.Panel("Header", root.transform, Vector2.zero, new Vector2(size.x, HeaderHeight), UIFactory.HeaderColor, raycast: false);
		var titleText = UIFactory.Text("Title", root.transform, new Vector2(Pad, 0f), new Vector2(size.x * 0.5f, HeaderHeight), title, 22f);
		titleText.fontStyle = FontStyles.Bold;
		window.weightText = UIFactory.Text("Weight", root.transform, new Vector2(size.x * 0.4f, 0f), new Vector2(size.x * 0.6f - 50f, HeaderHeight),
			"", 18f, TextAlignmentOptions.MidlineRight);
		UIFactory.Button("Close", root.transform, new Vector2(size.x - 40f, 6f), new Vector2(30f, 28f), "X", () => screen.Close(window));

		// Equipment column.
		if (hasEquipment)
		{
			foreach (var (slot, col, row) in SlotLayout)
			{
				Vector2 pos = new Vector2(Pad + col * (SlotSize + SlotGap), HeaderHeight + Pad + row * SlotRowHeight);
				window.slots.Add(EquipmentSlotView.Create(window, root.transform, slot, pos, new Vector2(SlotSize, SlotSize)));
			}
			equipment.OnEquipChanged += window.HandleEquipChanged;
		}

		window.grid = InventoryGridView.Create(window, root.transform, inventory, new Vector2(Pad + equipWidth, HeaderHeight + Pad));
		inventory.OnInventoryChanged += window.RefreshWeight;

		window.infoText = UIFactory.Text("Info", root.transform, new Vector2(Pad, size.y - FooterHeight), new Vector2(size.x - Pad * 2f, FooterHeight), "", 16f);
		window.ShowInfo(null, 0);
		window.RefreshWeight();
		return window;
	}

	private void OnDestroy()
	{
		if (inventory != null)
			inventory.OnInventoryChanged -= RefreshWeight;
		if (equipment != null)
			equipment.OnEquipChanged -= HandleEquipChanged;
	}

	private void Update()
	{
		// Close when what we show is gone, the owner died, or the looter walked away.
		if (inventory == null
			|| (character != null && !character.IsAlive)
			|| (IsLootWindow && (looter == null || !looter.IsAlive || !InventoryActions.InReach(looter, inventory))))
		{
			screen.Close(this);
			return;
		}

		if (messageUntil > 0f && Time.unscaledTime > messageUntil)
		{
			messageUntil = 0f;
			ShowInfo(null, 0);
		}

		// Capacity changes without any item moving (Strength levels up), so refresh now and then.
		if (Time.unscaledTime >= nextWeightRefresh)
		{
			nextWeightRefresh = Time.unscaledTime + 0.5f;
			RefreshWeight();
		}
	}

	private void HandleEquipChanged()
	{
		foreach (var slot in slots)
			slot.Refresh();
		RefreshWeight();
	}

	public void RefreshWeight()
	{
		if (weightText == null)
			return;

		if (character != null)
		{
			float carried = character.CarryWeight;
			float capacity = character.CarryCapacity;
			weightText.text = $"{carried:0.0} / {capacity:0} kg  ·  {character.EncumbranceLabel}";
			weightText.color = carried > capacity ? UIFactory.WarningColor : UIFactory.TextColor;
		}
		else
		{
			weightText.text = $"{inventory.TotalWeight:0.0} kg";
			weightText.color = UIFactory.TextColor;
		}
	}

	public void ShowInfo(Item item, int count)
	{
		if (infoText == null || (item == null && messageUntil > 0f))
			return;

		if (item == null)
		{
			infoText.color = UIFactory.MutedTextColor;
			infoText.text = IsLootWindow
				? "Drag items to your inventory, or right-click to take them."
				: "Drag to move · R rotates while dragging · Right-click to use or equip";
			return;
		}

		infoText.color = UIFactory.TextColor;
		string amount = count > 1 ? $" x{count}" : "";
		string desc = string.IsNullOrEmpty(item.description) ? "" : $"  —  {item.description}";
		infoText.text = $"{item.itemName}{amount}  ·  {item.weight * Mathf.Max(1, count):0.0} kg{desc}";
	}

	public void ShowMessage(string message)
	{
		if (infoText == null || string.IsNullOrEmpty(message))
			return;

		infoText.color = UIFactory.WarningColor;
		infoText.text = message;
		messageUntil = Time.unscaledTime + 2f;
	}

	/// <summary>Right-click on a stack: eat/drink/equip it, or (in a loot window) take it.</summary>
	public void UseItem(Inventory.Entry entry)
	{
		string reason = null;
		if (IsLootWindow)
		{
			var looterInventory = looter != null ? looter.GetComponent<Inventory>() : null;
			InventoryActions.TryQuickMove(inventory, entry, looterInventory, out reason);
		}
		else if (entry.item is EquippableItem)
			InventoryActions.TryEquip(inventory, entry, equipment, inventory, out reason);
		else if (entry.item is HealingItem)
			character.TryTreatWounds(entry, out reason);
		else if (!character.TryConsume(entry))
			reason = $"Can't use {entry.item.itemName}";

		ShowMessage(reason);
	}

	public void Unequip(EquipSlot slot)
	{
		InventoryActions.TryUnequip(equipment, slot, inventory, null, false, out string reason);
		ShowMessage(reason);
	}
}
