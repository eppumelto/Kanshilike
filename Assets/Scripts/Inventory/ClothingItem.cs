using UnityEngine;

[CreateAssetMenu(fileName = "NewClothing", menuName = "Inventory/Clothing")]
public class ClothingItem : EquippableItem
{
	[Header("Clothing")]
	[Tooltip("Flat armor value provided by this piece of clothing.")]
	public float armorValue = 0f;
}
