using System.Collections.Generic;
using UnityEngine;

public enum EquipSlot { Hand, Shield, Helmet, Chest, Legs, Shoes, Gloves }

/// <summary>
/// What a character is wearing and wielding. Equipped items are held here, not in the
/// inventory grid (Kenshi-style): moving them between the two is InventoryActions' job.
/// </summary>
public class EquipmentManager : MonoBehaviour
{
	[System.Serializable]
	private class SlotSocket
	{
		public EquipSlot slot;
		public Transform socket;
	}

	[SerializeField] private List<SlotSocket> sockets = new List<SlotSocket>();

	private class EquipEntry
	{
		public EquippableItem item;
		public GameObject instance;
	}

	private readonly Dictionary<EquipSlot, EquipEntry> equipped = new Dictionary<EquipSlot, EquipEntry>();

	public event System.Action OnEquipChanged;

	public static readonly EquipSlot[] AllSlots = (EquipSlot[])System.Enum.GetValues(typeof(EquipSlot));

	public float TotalWeight
	{
		get
		{
			float total = 0f;
			foreach (var e in equipped.Values)
				total += e.item.weight;
			return total;
		}
	}

	/// <summary>
	/// Puts the item in its slot and returns whatever was there before (or null).
	/// The caller is responsible for where the returned item goes.
	/// </summary>
	public EquippableItem Equip(EquippableItem item)
	{
		if (item == null)
			return null;

		EquipSlot slot = item.equipSlot;
		EquippableItem previous = Unequip(slot);

		Transform socket = GetSocket(slot);
		GameObject instance = null;
		GameObject equippedPrefab = ResolveEquippedPrefab(item);
		if (equippedPrefab != null && socket != null)
		{
			instance = Instantiate(equippedPrefab, socket);
			instance.transform.localPosition = Vector3.zero;
			instance.transform.localRotation = Quaternion.identity;
			instance.transform.localScale = Vector3.one;
		}

		equipped[slot] = new EquipEntry { item = item, instance = instance };
		OnEquipChanged?.Invoke();
		return previous;
	}

	/// <summary>Empties the slot and returns the item that was in it (or null).</summary>
	public EquippableItem Unequip(EquipSlot slot)
	{
		if (!equipped.TryGetValue(slot, out var entry))
			return null;

		if (entry.instance != null)
			Destroy(entry.instance);

		equipped.Remove(slot);
		OnEquipChanged?.Invoke();
		return entry.item;
	}

	public EquippableItem GetEquipped(EquipSlot slot)
	{
		return equipped.TryGetValue(slot, out var entry) ? entry.item : null;
	}

	/// <summary>The item in another slot that can't be used together with this one (two-handed weapon vs shield), or null.</summary>
	public EquippableItem GetConflict(EquippableItem item)
	{
		if (item is WeaponItem weapon && weapon.twoHanded)
			return GetEquipped(EquipSlot.Shield);

		if (item.equipSlot == EquipSlot.Shield && GetEquipped(EquipSlot.Hand) is WeaponItem held && held.twoHanded)
			return held;

		return null;
	}

	private Transform GetSocket(EquipSlot slot)
	{
		foreach (var s in sockets)
			if (s.slot == slot) return s.socket;
		return null;
	}

	private GameObject ResolveEquippedPrefab(EquippableItem item)
	{
		if (item is WeaponItem weapon && weapon.weaponPrefab != null)
			return weapon.weaponPrefab;

		return item.equippedPrefab;
	}
}
