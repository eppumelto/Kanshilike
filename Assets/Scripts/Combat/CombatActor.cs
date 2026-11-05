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

	private readonly CombatSlotReservation attackSlot = new CombatSlotReservation();

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

	/// <summary>
	/// Takes one of the target's attacker places (see AttackerSlotController). False while the
	/// target already has its maximum number of attackers; this actor is then queued.
	/// </summary>
	public bool TryTakeAttackSlot(CombatActor target)
	{
		return attackSlot.TryReserve(gameObject, target);
	}

	/// <summary>Gives up the attacker place, letting the next queued attacker in.</summary>
	public void ReleaseAttackSlot()
	{
		attackSlot.Release(gameObject);
	}

	/// <summary>
	/// One melee attack against the target: the target defends, damage and XP are applied. Used by
	/// every actor that fights in melee, so people and animals follow the same rules.
	/// </summary>
	public CombatExchangeResult ResolveStrike(CombatActor target, AttackProfile attack, CombatFormulaConfig config, System.Action<CombatReactionType> onReactionDecided = null)
	{
		CombatExchangeResult result = CombatExchangeResolver.ResolveAndApply(attack, this, target, target.Stats, config, gameObject, onReactionDecided);
		if (!target.IsAlive)
			ReleaseAttackSlot();
		return result;
	}

	protected virtual void Awake()
	{
		stats = GetComponent<CharacterStats>();
		skills = GetComponent<SkillSet>();
		equipment = GetComponent<EquipmentManager>();
	}
}
