using UnityEngine;

/// <summary>
/// Base class for items that occupy an EquipSlot (weapons, clothing, shields, etc.).
/// Subclasses: WeaponItem, ClothingItem.
/// </summary>
public abstract class EquippableItem : Item
{
	[Header("Equipment")]
	public EquipSlot equipSlot = EquipSlot.Hand;

	[Header("Equipment Visual")]
	[Tooltip("Generic equipped visual used by non-weapon equippables. Weapons still use WeaponItem.weaponPrefab.")]
	public GameObject equippedPrefab;

	/// <summary>Using equipment means putting it on; whatever it replaces goes back into the bag.</summary>
	public override bool TryUse(Character user, Inventory.Entry entry, out string reason)
	{
		Inventory inventory = user.CarriedInventory;
		return InventoryActions.TryEquip(inventory, entry, user.Equipment, inventory, out reason);
	}
}
