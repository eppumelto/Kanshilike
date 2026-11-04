using UnityEngine;

/// <summary>
/// A shield for the Shield slot. It only helps against attacks from inside its arc, so the
/// carrier has to face the enemy: flanking attackers and fleeing past enemies get no benefit.
/// </summary>
[CreateAssetMenu(fileName = "NewShield", menuName = "Inventory/Shield")]
public class ShieldItem : EquippableItem
{
	[Header("Shield")]
	[Tooltip("Width of the protected arc in degrees, centred on where the carrier faces.")]
	[Range(0f, 360f)] public float coverageAngle = 140f;

	[Tooltip("Multiplies block rating against attacks inside the arc.")]
	[Min(1f)] public float blockRatingMultiplier = 1.6f;

	[Tooltip("Multiplies how often the carrier chooses to block against attacks inside the arc.")]
	[Min(1f)] public float blockWeightMultiplier = 2f;

	[Tooltip("Damage removed from a hit when a block inside the arc fails (the shield still takes some of it).")]
	[Range(0f, 1f)] public float failedBlockDamageReduction = 0.3f;

	[Tooltip("Multiplies attack speed while carried. Below 1 is slower.")]
	[Range(0.1f, 1f)] public float attackSpeedMultiplier = 0.9f;

	// Called when the asset is created or reset in the editor.
	private void Reset() => equipSlot = EquipSlot.Shield;
}
