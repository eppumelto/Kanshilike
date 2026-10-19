using UnityEngine;

public class InventoryUI : MonoBehaviour
{
	[SerializeField] private Inventory inventory;
	[SerializeField] private EquipmentManager equipment;
	[SerializeField] private SelectableCharacter character;
	[SerializeField] private InventorySlotUI[] slots;
	[SerializeField] private EquipSlotUI[] equipSlots;
	[SerializeField] private SelectionManager selectionManager;

	private InventorySlotUI selectedSlot;
	private bool equipSlotSelected;
	private EquipSlot selectedEquipSlot;

	private void Start()
	{
		foreach (var slot in slots)
			slot.OnClicked += OnSlotClicked;

		if (equipSlots == null || equipSlots.Length == 0)
			equipSlots = GetComponentsInChildren<EquipSlotUI>(true);

		if (selectionManager == null)
			selectionManager = FindFirstObjectByType<SelectionManager>();

		BindContext(inventory, equipment, character);

		if (selectionManager != null)
		{
			selectionManager.OnSelectionChanged += OnSelectionChanged;
			BindToCharacter(selectionManager.PrimarySelected);
		}

		RefreshAll();
		gameObject.SetActive(false);
	}

	private void OnDestroy()
	{
		UnbindEvents();

		if (selectionManager != null)
			selectionManager.OnSelectionChanged -= OnSelectionChanged;
	}

	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.E))
			UseSelected();
	}

	public void SelectEquipSlot(EquipSlot slot)
	{
		ClearSelection();
		equipSlotSelected = true;
		selectedEquipSlot = slot;
	}

	private void OnSlotClicked(InventorySlotUI slot)
	{
		// Toggle deselect
		if (selectedSlot == slot)
		{
			ClearSelection();
			return;
		}
		ClearSelection();
		selectedSlot = slot;
		selectedSlot.SetSelected(true);
	}

	private void ClearSelection()
	{
		if (selectedSlot != null)
		{
			selectedSlot.SetSelected(false);
			selectedSlot = null;
		}
		equipSlotSelected = false;
	}

	private void UseSelected()
	{
		if (equipment == null || inventory == null)
			return;

		if (equipSlotSelected)
		{
			equipment.Unequip(selectedEquipSlot);
			ClearSelection();
			return;
		}

		if (selectedSlot == null) return;

		Item item = selectedSlot.CurrentItem;
		if (item == null) return;

		int index = System.Array.IndexOf(slots, selectedSlot);
		if (index < 0) return;

		if (item is EquippableItem equippable)
		{
			equipment.Equip(equippable, index);
		}
		else if (item is FoodItem food)
		{
			character?.Eat(food.nutritionValue);
			inventory.RemoveItemAt(index);
		}
		else if (item is DrinkItem drink)
		{
			character?.Drink(drink.hydrationValue);
			inventory.RemoveItemAt(index);
		}
		else
		{
			Debug.Log($"No use action defined for {item.itemName}.");
		}

		ClearSelection();
	}

	private void RefreshAll()
	{
		ClearSelection();

		if (inventory == null)
		{
			for (int i = 0; i < slots.Length; i++)
				slots[i].Refresh(null, i, null);
			return;
		}

		for (int i = 0; i < slots.Length; i++)
			slots[i].Refresh(inventory.GetItemAt(i), i, equipment);
	}

	public void ToggleForCharacter(SelectableCharacter targetCharacter)
	{
		if (gameObject.activeSelf)
		{
			Close();
			return;
		}

		if (targetCharacter == null)
			return;

		OpenForCharacter(targetCharacter);
	}

	public void OpenForCharacter(SelectableCharacter targetCharacter)
	{
		if (targetCharacter == null)
			return;

		BindToCharacter(targetCharacter);
		gameObject.SetActive(true);
	}

	public void Close()
	{
		ClearSelection();
		gameObject.SetActive(false);
	}

	private void OnSelectionChanged(System.Collections.Generic.IReadOnlyList<SelectableCharacter> selected)
	{
		BindToCharacter(selectionManager != null ? selectionManager.PrimarySelected : null);
	}

	private void BindToCharacter(SelectableCharacter targetCharacter)
	{
		if (targetCharacter == null)
			return;

		BindContext(
			targetCharacter.GetComponent<Inventory>(),
			targetCharacter.GetComponent<EquipmentManager>(),
			targetCharacter);
	}

	private void BindContext(Inventory nextInventory, EquipmentManager nextEquipment, SelectableCharacter nextCharacter)
	{
		if (inventory == nextInventory && equipment == nextEquipment && character == nextCharacter)
			return;

		UnbindEvents();

		inventory = nextInventory;
		equipment = nextEquipment;
		character = nextCharacter;

		if (inventory != null)
			inventory.OnInventoryChanged += RefreshAll;
		if (equipment != null)
			equipment.OnEquipChanged += RefreshAll;

		if (equipSlots != null)
		{
			foreach (var equipSlot in equipSlots)
				equipSlot?.SetEquipmentManager(equipment);
		}

		RefreshAll();
	}

	private void UnbindEvents()
	{
		if (inventory != null)
			inventory.OnInventoryChanged -= RefreshAll;
		if (equipment != null)
			equipment.OnEquipChanged -= RefreshAll;
	}
}
