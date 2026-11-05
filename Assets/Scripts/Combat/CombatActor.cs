using UnityEngine;

/// <summary>
/// Anything that fights and can be fought: people (Character) and animals (AnimalAI).
/// AI should target this type rather than a specific kind of creature.
/// </summary>
public abstract class CombatActor : MonoBehaviour, ICombatActor
{
	protected CharacterStats stats;
	protected SkillSet skills;
	protected EquipmentManager equipment;

	public Transform ActorTransform => transform;
	public CharacterStats Stats => stats;
	public SkillSet Skills => skills;
	public EquipmentManager Equipment => equipment;

	public abstract Faction Faction { get; }
	public abstract bool CanUseEquipment { get; }
	public abstract bool CanBlock { get; }
	public abstract bool CanDodge { get; }
	public abstract bool CanCounter { get; }

	public bool IsAlive => stats == null || stats.IsAlive;

	protected virtual void Awake()
	{
		stats = GetComponent<CharacterStats>();
		skills = GetComponent<SkillSet>();
		equipment = GetComponent<EquipmentManager>();
	}
}
