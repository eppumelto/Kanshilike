using UnityEngine;

[CreateAssetMenu(fileName = "NewFood", menuName = "Inventory/Food")]
public class FoodItem : Item
{
	[Header("Consumable")]
	[Tooltip("How much hunger this food restores.")]
	public float nutritionValue = 20f;
}
