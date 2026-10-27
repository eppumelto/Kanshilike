using UnityEngine;

public class Inventory : MonoBehaviour
{
	public const int SlotCount = 12;

	[System.Serializable]
	public struct Stack
	{
		public Item item;
		[Min(0)] public int count;

		public bool IsEmpty => item == null || count <= 0;
	}

	[SerializeField] private Stack[] slots = new Stack[SlotCount];

	public event System.Action OnInventoryChanged;

	/// <summary>
	/// Adds one item, topping up an existing stack first, then the first empty slot.
	/// Returns false (and changes nothing) if there is no room.
	/// </summary>
	public bool AddItem(Item item)
	{
		if (item == null)
			return false;

		int index = FindSlotFor(item);
		if (index < 0)
		{
			Debug.Log($"Inventory is full, no room for '{item.itemName}'.");
			return false;
		}

		slots[index].item = item;
		slots[index].count++;
		Debug.Log($"Added '{item.itemName}' to inventory slot {index} ({slots[index].count}).");
		OnInventoryChanged?.Invoke();
		return true;
	}

	/// <summary>True if one more of this item would fit.</summary>
	public bool CanAdd(Item item) => item != null && FindSlotFor(item) >= 0;

	public Item GetItemAt(int index) => slots[index].IsEmpty ? null : slots[index].item;

	public int GetCountAt(int index) => slots[index].IsEmpty ? 0 : slots[index].count;

	public bool IsFull()
	{
		for (int i = 0; i < SlotCount; i++)
			if (slots[i].IsEmpty) return false;
		return true;
	}

	/// <summary>Removes one item from the slot, clearing it when the stack runs out.</summary>
	public void RemoveOneAt(int index)
	{
		if (slots[index].IsEmpty)
			return;

		slots[index].count--;
		if (slots[index].count <= 0)
			slots[index] = default;

		OnInventoryChanged?.Invoke();
	}

	/// <summary>
	/// Turns every stack of <paramref name="from"/> into <paramref name="to"/>, keeping the counts
	/// (e.g. empty waterskins into full ones). Returns how many items changed.
	/// </summary>
	public int ReplaceAll(Item from, Item to)
	{
		if (from == null || to == null)
			return 0;

		int changed = 0;
		for (int i = 0; i < SlotCount; i++)
		{
			if (slots[i].IsEmpty || slots[i].item != from)
				continue;

			slots[i].item = to;
			changed += slots[i].count;
		}

		if (changed > 0)
			OnInventoryChanged?.Invoke();
		return changed;
	}

	/// <summary>Index of the first slot holding an item of type T, or -1.</summary>
	public int FindFirst<T>() where T : Item
	{
		for (int i = 0; i < SlotCount; i++)
			if (!slots[i].IsEmpty && slots[i].item is T)
				return i;
		return -1;
	}

	private int FindSlotFor(Item item)
	{
		for (int i = 0; i < SlotCount; i++)
			if (!slots[i].IsEmpty && slots[i].item == item && slots[i].count < item.maxStack)
				return i;

		for (int i = 0; i < SlotCount; i++)
			if (slots[i].IsEmpty)
				return i;

		return -1;
	}

	private void OnValidate()
	{
		if (slots == null || slots.Length != SlotCount)
			System.Array.Resize(ref slots, SlotCount);
	}
}
