using UnityEngine;

[CreateAssetMenu(fileName = "NewWeapon", menuName = "Inventory/Weapon")]
public class WeaponItem : EquippableItem
{
	[Header("Weapon")]
	public GameObject weaponPrefab;

	[Tooltip("Which skill is trained when this weapon is used to attack.")]
	public SkillType weaponSkill = SkillType.Unarmed;

	[Tooltip("Base damage dealt per attack.")]
	public float damage = 10f;

	[Tooltip("Max melee reach in world units. Spears should typically be higher than axes.")]
	[Min(0.2f)] public float attackRange = 1.5f;

	[Tooltip("Attack speed multiplier. >1 is faster, <1 is slower.")]
	[Min(0.1f)] public float attackSpeedMultiplier = 1f;
}
