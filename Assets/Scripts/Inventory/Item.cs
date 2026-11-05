using UnityEngine;

/// <summary>
/// Base class for all inventory items. Add shared fields here only.
/// Concrete types: WeaponItem, ClothingItem, FoodItem, DrinkItem, MiscItem.
/// </summary>
public abstract class Item : ScriptableObject
{
	public string itemName;
	public Sprite icon;

	[TextArea]
	public string description;

	[Tooltip("How many of this item fit in one inventory stack. 1 = does not stack.")]
	[Min(1)] public int maxStack = 1;

	[Header("Inventory Grid")]
	[Tooltip("Cells the item covers in an inventory grid, unrotated (width x height). A spear might be 1x5, berries 1x1.")]
	public Vector2Int size = Vector2Int.one;

	[Tooltip("Weight of one item in kg. Counts toward the carrier's encumbrance.")]
	[Min(0f)] public float weight = 1f;

	/// <summary>
	/// What happens when a character uses one of this item from their inventory (right-click, or
	/// automatic eating and drinking). Returns false with a short reason when it can't be used.
	/// </summary>
	public virtual bool TryUse(Character user, Inventory.Entry entry, out string reason)
	{
		reason = $"Can't use {itemName}";
		return false;
	}

	protected virtual void OnValidate()
	{
		size = Vector2Int.Max(size, Vector2Int.one);
	}
}
