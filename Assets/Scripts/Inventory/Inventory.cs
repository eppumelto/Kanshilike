using UnityEngine;

public class Inventory : MonoBehaviour
{
	public const int SlotCount = 12;

	public Item[] slots = new Item[SlotCount];

	public event System.Action OnInventoryChanged;

	/// <summary>
	/// Adds an item to the first empty slot. Returns true on success, false if inventory is full.
	/// </summary>
	public bool AddItem(Item item)
	{
		for (int i = 0; i < SlotCount; i++)
		{
			if (slots[i] == null)
			{
				slots[i] = item;
				Debug.Log($"Added '{item.itemName}' to inventory slot {i}.");
				OnInventoryChanged?.Invoke();
				return true;
			}
		}
		Debug.Log("Inventory is full!");
		return false;
	}

	public Item GetItemAt(int index) => slots[index];

	public bool IsFull()
	{
		for (int i = 0; i < SlotCount; i++)
			if (slots[i] == null) return false;
		return true;
	}

	public void RemoveItemAt(int index)
	{
		slots[index] = null;
		OnInventoryChanged?.Invoke();
	}
}
