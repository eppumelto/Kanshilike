using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Moving items between inventory grids and equipment slots. Every action either completes
/// or leaves everything as it was, and returns a short reason when it fails (shown in the UI).
/// </summary>
public static class InventoryActions
{
	/// <summary>Inventories farther apart than this can't swap items (e.g. a corpse you walked away from).</summary>
	public const float MaxTransferDistance = 5f;

	public static bool InReach(Component a, Component b)
	{
		if (a == null || b == null)
			return false;
		if (a.gameObject == b.gameObject)
			return true;
		return Vector3.Distance(a.transform.position, b.transform.position) <= MaxTransferDistance;
	}

	/// <summary>
	/// Drops a stack from <paramref name="from"/> onto <paramref name="cell"/> of <paramref name="to"/>.
	/// Lands on a matching stack → merges; on free space → moves (or transfers) the whole stack.
	/// </summary>
	public static bool TryMoveEntry(Inventory from, Inventory.Entry entry, Inventory to, Vector2Int cell, bool rotated, out string reason)
	{
		reason = null;
		if (from == null || to == null || !from.Contains(entry))
			return false;

		if (from != to && !InReach(from, to))
		{
			reason = "Too far away";
			return false;
		}

		// Dropped onto a stack of the same item: top it up.
		var under = to.GetEntryAt(cell);
		if (under != null && under != entry && under.item == entry.item)
		{
			if (Inventory.Merge(from, entry, to, under) > 0)
				return true;
			reason = "Stack is full";
			return false;
		}

		if (from == to)
		{
			if (to.Move(entry, cell, rotated))
				return true;
			reason = "Doesn't fit there";
			return false;
		}

		if (!to.CanPlace(entry.item, cell, rotated))
		{
			reason = "Doesn't fit there";
			return false;
		}

		to.Place(entry.item, entry.count, cell, rotated);
		from.RemoveEntry(entry);
		return true;
	}

	/// <summary>
	/// Moves a stack into <paramref name="to"/> wherever it fits (merging onto stacks first).
	/// Used for quick-transfer (right-click in a loot window). Partial moves count as success.
	/// </summary>
	public static bool TryQuickMove(Inventory from, Inventory.Entry entry, Inventory to, out string reason)
	{
		reason = null;
		if (from == null || to == null || from == to || !from.Contains(entry))
			return false;

		if (!InReach(from, to))
		{
			reason = "Too far away";
			return false;
		}

		int count = entry.count;
		int left = to.Add(entry.item, count);
		int moved = count - left;
		if (moved == 0)
		{
			reason = "No room";
			return false;
		}

		from.SetCount(entry, left);
		return true;
	}

	/// <summary>
	/// Equips an item taken from an inventory stack. Whatever was in the slot goes into
	/// <paramref name="ownerInventory"/>; if it has no room, nothing changes.
	/// </summary>
	public static bool TryEquip(Inventory from, Inventory.Entry entry, EquipmentManager equipment, Inventory ownerInventory, out string reason)
	{
		reason = null;
		if (from == null || equipment == null || !from.Contains(entry))
			return false;

		if (entry.item is not EquippableItem item)
		{
			reason = "Can't be equipped";
			return false;
		}

		if (!InReach(from, equipment))
		{
			reason = "Too far away";
			return false;
		}

		// Take one item off the stack (equippables normally don't stack).
		Vector2Int oldPos = entry.position;
		bool oldRot = entry.rotated;
		int oldCount = entry.count;
		from.RemoveOne(entry);

		// Whatever is in the way goes to the bag: the slot's item, plus a shield or two-handed
		// weapon that can't be used together with this item.
		var displaced = new List<EquippableItem>();
		EquippableItem conflict = equipment.GetConflict(item);
		if (conflict != null)
			displaced.Add(equipment.Unequip(conflict.equipSlot));

		EquippableItem previous = equipment.Equip(item);
		if (previous != null)
			displaced.Add(previous);

		var stored = new List<EquippableItem>();
		foreach (var d in displaced)
		{
			if (ownerInventory != null && ownerInventory.AddItem(d))
			{
				stored.Add(d);
				continue;
			}

			// Roll back: everything back where it was.
			foreach (var s in stored)
				ownerInventory.RemoveOneOf(s);
			equipment.Unequip(item.equipSlot);
			foreach (var back in displaced)
				equipment.Equip(back);
			if (oldCount == 1)
				from.Place(item, 1, oldPos, oldRot);
			else
				from.Add(item, 1);
			reason = $"No room for {d.itemName}";
			return false;
		}

		return true;
	}

	/// <summary>Takes the item out of the slot and puts it at <paramref name="cell"/> (or anywhere if null).</summary>
	public static bool TryUnequip(EquipmentManager equipment, EquipSlot slot, Inventory to, Vector2Int? cell, bool rotated, out string reason)
	{
		reason = null;
		EquippableItem item = equipment != null ? equipment.GetEquipped(slot) : null;
		if (item == null || to == null)
			return false;

		if (!InReach(equipment, to))
		{
			reason = "Too far away";
			return false;
		}

		bool fits = cell.HasValue
			? to.CanPlace(item, cell.Value, rotated) || StacksOnto(to, cell.Value, item)
			: to.CanAdd(item);
		if (!fits)
		{
			reason = cell.HasValue ? "Doesn't fit there" : "No room";
			return false;
		}

		equipment.Unequip(slot);
		if (cell.HasValue && to.CanPlace(item, cell.Value, rotated))
			to.Place(item, 1, cell.Value, rotated);
		else
			to.AddItem(item);
		return true;
	}

	/// <summary>Moves everything equipped into the inventory where there is room (used on death so it can be looted).</summary>
	public static void UnequipAllInto(EquipmentManager equipment, Inventory to)
	{
		if (equipment == null || to == null)
			return;

		foreach (var slot in EquipmentManager.AllSlots)
		{
			var item = equipment.GetEquipped(slot);
			if (item != null && to.CanAdd(item))
			{
				equipment.Unequip(slot);
				to.AddItem(item);
			}
		}
	}

	private static bool StacksOnto(Inventory inventory, Vector2Int cell, Item item)
	{
		var under = inventory.GetEntryAt(cell);
		return under != null && under.item == item && under.SpaceLeft > 0;
	}
}
