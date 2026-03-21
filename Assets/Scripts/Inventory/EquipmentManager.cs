using System.Collections.Generic;
using UnityEngine;

public enum EquipSlot { Hand, Shield, Helmet, Chest, Legs, Shoes, Gloves }

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
		public int inventoryIndex;
		public GameObject instance;
	}

	private readonly Dictionary<EquipSlot, EquipEntry> equipped = new Dictionary<EquipSlot, EquipEntry>();

	public event System.Action OnEquipChanged;

	public void Equip(EquippableItem item, int inventoryIndex)
	{
		if (item == null) return;

		EquipSlot slot = item.equipSlot;
		Unequip(slot);

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

		equipped[slot] = new EquipEntry { item = item, inventoryIndex = inventoryIndex, instance = instance };
		Debug.Log($"Equipped {item.itemName} in {slot} slot.");
		OnEquipChanged?.Invoke();
	}

	public void Unequip(EquipSlot slot)
	{
		if (!equipped.TryGetValue(slot, out var entry)) return;

		if (entry.instance != null)
			Destroy(entry.instance);

		Debug.Log($"Unequipped {entry.item.itemName} from {slot} slot.");
		equipped.Remove(slot);
		OnEquipChanged?.Invoke();
	}

	public EquippableItem GetEquipped(EquipSlot slot)
	{
		return equipped.TryGetValue(slot, out var entry) ? entry.item : null;
	}

	public int GetEquippedInventoryIndex(EquipSlot slot)
	{
		return equipped.TryGetValue(slot, out var entry) ? entry.inventoryIndex : -1;
	}

	/// <summary>Returns true if the given inventory slot index is currently equipped in any slot.</summary>
	public bool IsInventorySlotEquipped(int inventoryIndex)
	{
		foreach (var entry in equipped.Values)
			if (entry.inventoryIndex == inventoryIndex) return true;
		return false;
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
