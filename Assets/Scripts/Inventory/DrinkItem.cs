using UnityEngine;

[CreateAssetMenu(fileName = "NewDrink", menuName = "Inventory/Drink")]
public class DrinkItem : Item
{
	[Header("Consumable")]
	[Tooltip("How much thirst this drink restores.")]
	public float hydrationValue = 20f;

	[Tooltip("Item left in the inventory after drinking, e.g. an empty waterskin. Wells refill it back into this drink. Leave empty for drinks that are used up.")]
	public Item emptyContainer;

	public override bool TryUse(Character user, Inventory.Entry entry, out string reason)
	{
		reason = null;
		Inventory inventory = user.CarriedInventory;
		user.Drink(hydrationValue);

		Vector2Int pos = entry.position;
		bool rotated = entry.rotated;
		bool lastOne = entry.count == 1;
		inventory.RemoveOne(entry);

		// Keep the empty container (waterskin) so it can be refilled at a well,
		// in the same spot when that was the last one of the stack.
		if (emptyContainer != null
			&& !(lastOne && inventory.Place(emptyContainer, 1, pos, rotated))
			&& !inventory.AddItem(emptyContainer))
			user.ShowFloatingText($"No room, dropped {emptyContainer.itemName}", 2f);

		return true;
	}

	// Called when the asset is created or reset in the editor.
	private void Reset() => maxStack = 20;
}
