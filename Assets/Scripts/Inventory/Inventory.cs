using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kenshi-style grid inventory. Each entry is a stack of one item type placed at a cell,
/// covering the item's size (swapped when rotated). Cell (0,0) is the top-left corner.
/// </summary>
public class Inventory : MonoBehaviour
{
	[System.Serializable]
	public class Entry
	{
		public Item item;
		[Min(1)] public int count = 1;
		public Vector2Int position;
		public bool rotated;

		public Vector2Int Size => SizeOf(item, rotated);
		public RectInt Rect => new RectInt(position, Size);
		public float Weight => item != null ? item.weight * count : 0f;
		public int SpaceLeft => item != null ? Mathf.Max(0, item.maxStack - count) : 0;
	}

	[SerializeField, Min(1)] private int width = 8;
	[SerializeField, Min(1)] private int height = 6;
	[SerializeField] private List<Entry> entries = new List<Entry>();

	public event System.Action OnInventoryChanged;

	public int Width => width;
	public int Height => height;
	public IReadOnlyList<Entry> Entries => entries;

	public float TotalWeight
	{
		get
		{
			float total = 0f;
			foreach (var e in entries)
				total += e.Weight;
			return total;
		}
	}

	public static Vector2Int SizeOf(Item item, bool rotated)
	{
		Vector2Int size = item != null ? Vector2Int.Max(item.size, Vector2Int.one) : Vector2Int.one;
		return rotated ? new Vector2Int(size.y, size.x) : size;
	}

	/// <summary>Changes the grid size. Only for empty inventories (e.g. a freshly made corpse inventory).</summary>
	public void Resize(int newWidth, int newHeight)
	{
		if (entries.Count > 0)
		{
			Debug.LogWarning($"{name}: can't resize a non-empty inventory.");
			return;
		}

		width = Mathf.Max(1, newWidth);
		height = Mathf.Max(1, newHeight);
		OnInventoryChanged?.Invoke();
	}

	// ── Queries ─────────────────────────────────────────────────────────────

	public Entry GetEntryAt(Vector2Int cell)
	{
		foreach (var e in entries)
			if (e.Rect.Contains(cell))
				return e;
		return null;
	}

	/// <summary>True if the item fits at <paramref name="position"/>, ignoring <paramref name="ignore"/> (the entry being moved).</summary>
	public bool CanPlace(Item item, Vector2Int position, bool rotated, Entry ignore = null)
	{
		if (item == null)
			return false;

		var rect = new RectInt(position, SizeOf(item, rotated));
		if (rect.xMin < 0 || rect.yMin < 0 || rect.xMax > width || rect.yMax > height)
			return false;

		foreach (var e in entries)
			if (e != ignore && e.Rect.Overlaps(rect))
				return false;
		return true;
	}

	/// <summary>First free spot for the item, scanning row by row, trying it unrotated and then rotated.</summary>
	public bool TryFindSpot(Item item, out Vector2Int position, out bool rotated, Entry ignore = null)
	{
		foreach (bool rot in new[] { false, true })
		{
			if (rot && item.size.x == item.size.y)
				continue;

			for (int y = 0; y < height; y++)
				for (int x = 0; x < width; x++)
				{
					var p = new Vector2Int(x, y);
					if (CanPlace(item, p, rot, ignore))
					{
						position = p;
						rotated = rot;
						return true;
					}
				}
		}

		position = default;
		rotated = false;
		return false;
	}

	/// <summary>True if one more of this item would fit (on a stack or in free space).</summary>
	public bool CanAdd(Item item)
	{
		if (item == null)
			return false;
		foreach (var e in entries)
			if (e.item == item && e.SpaceLeft > 0)
				return true;
		return TryFindSpot(item, out _, out _);
	}

	public Entry FindFirst<T>() where T : Item
	{
		foreach (var e in entries)
			if (e.item is T)
				return e;
		return null;
	}

	public bool Contains(Entry entry) => entry != null && entries.Contains(entry);

	// ── Changes ─────────────────────────────────────────────────────────────

	/// <summary>Adds one item, topping up a stack first, then the first free spot. False if there is no room.</summary>
	public bool AddItem(Item item) => Add(item, 1) == 0;

	/// <summary>Adds up to <paramref name="count"/> items. Returns how many did NOT fit.</summary>
	public int Add(Item item, int count)
	{
		if (item == null || count <= 0)
			return count;

		int left = count;
		foreach (var e in entries)
		{
			if (left == 0) break;
			if (e.item != item) continue;
			int put = Mathf.Min(left, e.SpaceLeft);
			e.count += put;
			left -= put;
		}

		while (left > 0 && TryFindSpot(item, out var pos, out var rot))
		{
			int put = Mathf.Min(left, item.maxStack);
			entries.Add(new Entry { item = item, count = put, position = pos, rotated = rot });
			left -= put;
		}

		if (left < count)
			OnInventoryChanged?.Invoke();
		return left;
	}

	/// <summary>Places a whole stack at an exact spot. False (and nothing changes) if it doesn't fit.</summary>
	public bool Place(Item item, int count, Vector2Int position, bool rotated)
	{
		if (count <= 0 || !CanPlace(item, position, rotated))
			return false;

		entries.Add(new Entry { item = item, count = count, position = position, rotated = rotated });
		OnInventoryChanged?.Invoke();
		return true;
	}

	/// <summary>Moves an entry within this inventory. False if it doesn't fit there.</summary>
	public bool Move(Entry entry, Vector2Int position, bool rotated)
	{
		if (!Contains(entry) || !CanPlace(entry.item, position, rotated, entry))
			return false;

		entry.position = position;
		entry.rotated = rotated;
		OnInventoryChanged?.Invoke();
		return true;
	}

	/// <summary>Moves as many items as fit from <paramref name="source"/> onto <paramref name="target"/> (same item). Returns how many moved.</summary>
	public static int Merge(Inventory sourceInventory, Entry source, Inventory targetInventory, Entry target)
	{
		if (source == null || target == null || source == target || source.item != target.item)
			return 0;

		int moved = Mathf.Min(source.count, target.SpaceLeft);
		if (moved <= 0)
			return 0;

		target.count += moved;
		sourceInventory.RemoveCount(source, moved, notify: false);
		sourceInventory.OnInventoryChanged?.Invoke();
		if (targetInventory != sourceInventory)
			targetInventory.OnInventoryChanged?.Invoke();
		return moved;
	}

	public void RemoveOne(Entry entry) => RemoveCount(entry, 1, notify: true);

	/// <summary>Removes one of this item from the first stack holding it.</summary>
	public void RemoveOneOf(Item item)
	{
		foreach (var e in entries)
		{
			if (e.item == item)
			{
				RemoveOne(e);
				return;
			}
		}
	}

	/// <summary>Sets a stack's size; 0 or less removes it.</summary>
	public void SetCount(Entry entry, int count)
	{
		if (!Contains(entry))
			return;

		if (count <= 0)
			entries.Remove(entry);
		else
			entry.count = Mathf.Min(count, entry.item.maxStack);
		OnInventoryChanged?.Invoke();
	}

	public void RemoveEntry(Entry entry)
	{
		if (entries.Remove(entry))
			OnInventoryChanged?.Invoke();
	}

	private void RemoveCount(Entry entry, int amount, bool notify)
	{
		if (!Contains(entry))
			return;

		entry.count -= amount;
		if (entry.count <= 0)
			entries.Remove(entry);

		if (notify)
			OnInventoryChanged?.Invoke();
	}

	/// <summary>
	/// Turns every stack of <paramref name="from"/> into <paramref name="to"/>, keeping counts and
	/// positions (e.g. empty waterskins into full ones). Stacks where the new item would not fit
	/// in the same spot are skipped. Returns how many items changed.
	/// </summary>
	public int ReplaceAll(Item from, Item to)
	{
		if (from == null || to == null)
			return 0;

		int changed = 0;
		foreach (var e in entries)
		{
			if (e.item != from)
				continue;

			Item old = e.item;
			e.item = to;
			if (!CanPlace(to, e.position, e.rotated, e))
			{
				e.item = old;
				continue;
			}
			changed += e.count;
		}

		if (changed > 0)
			OnInventoryChanged?.Invoke();
		return changed;
	}

	private void OnValidate()
	{
		width = Mathf.Max(1, width);
		height = Mathf.Max(1, height);
		entries.RemoveAll(e => e == null);
	}
}
