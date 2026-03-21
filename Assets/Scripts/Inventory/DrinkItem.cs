using UnityEngine;

[CreateAssetMenu(fileName = "NewDrink", menuName = "Inventory/Drink")]
public class DrinkItem : Item
{
	[Header("Consumable")]
	[Tooltip("How much thirst this drink restores.")]
	public float hydrationValue = 20f;
}
