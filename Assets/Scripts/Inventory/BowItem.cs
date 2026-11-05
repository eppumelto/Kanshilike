using UnityEngine;

/// <summary>
/// A weapon that shoots arrows. attackRange is how far the archer will shoot from: keep it under
/// the animals' chaseGiveUpDistance (30), or they stop chasing and can be shot for free.
/// </summary>
[CreateAssetMenu(fileName = "NewBow", menuName = "Inventory/Bow")]
public class BowItem : WeaponItem
{
	[Header("Bow")]
	[Tooltip("Arrow speed in meters per second.")]
	[Min(1f)] public float arrowSpeed = 30f;

	[Tooltip("Widest a shot can stray from the aim point, in degrees, for an untrained archer.")]
	[Min(0f)] public float untrainedSpread = 3f;

	[Tooltip("Widest a shot can stray from the aim point, in degrees, for a master archer.")]
	[Min(0f)] public float masterSpread = 0.75f;

	[Tooltip("Extra spread in degrees against a target running at movingTargetSpeed or faster.")]
	[Min(0f)] public float movingTargetSpread = 1f;

	[Tooltip("Target speed in meters per second at which movingTargetSpread applies in full.")]
	[Min(0.1f)] public float movingTargetSpeed = 5f;

	[Tooltip("The archer holds fire and moves closer until a shot has at least this chance of landing, so skilled archers open fire from further away.")]
	[Range(0f, 1f)] public float minHitChance = 0.15f;

	[Tooltip("The archer follows a fleeing target until it is this far away, then gives up.")]
	[Min(0f)] public float maxPursuitDistance = 45f;

	private void Reset()
	{
		weaponSkill = SkillType.Bows;
		twoHanded = true;
		attackRange = 25f;
	}
}
