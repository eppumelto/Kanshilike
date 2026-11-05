using UnityEngine;

/// <summary>Ammunition for bows. Shot from the archer's inventory, one per shot.</summary>
[CreateAssetMenu(fileName = "NewArrow", menuName = "Inventory/Arrow")]
public class ArrowItem : Item
{
	[Tooltip("What flies when this arrow is shot. Needs an ArrowProjectile component.")]
	public ArrowProjectile projectilePrefab;

	private void Reset() => maxStack = 20;
}
