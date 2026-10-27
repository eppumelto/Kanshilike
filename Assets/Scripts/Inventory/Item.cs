using UnityEngine;

/// <summary>
/// Base class for all inventory items. Add shared fields here only.
/// Concrete types: WeaponItem, ClothingItem, FoodItem, DrinkItem.
/// </summary>
public abstract class Item : ScriptableObject
{
	public string itemName;
	public Sprite icon;

	[TextArea]
	public string description;

	[Tooltip("How many of this item fit in one inventory slot. 1 = does not stack.")]
	[Min(1)] public int maxStack = 1;
}
