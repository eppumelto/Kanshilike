using UnityEngine;

[CreateAssetMenu(fileName = "NewDrink", menuName = "Inventory/Drink")]
public class DrinkItem : Item
{
	[Header("Consumable")]
	[Tooltip("How much thirst this drink restores.")]
	public float hydrationValue = 20f;

	[Tooltip("Item left in the inventory after drinking, e.g. an empty waterskin. Wells refill it back into this drink. Leave empty for drinks that are used up.")]
	public Item emptyContainer;

	// Called when the asset is created or reset in the editor.
	private void Reset() => maxStack = 20;
}
