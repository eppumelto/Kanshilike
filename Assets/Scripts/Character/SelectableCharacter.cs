using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using TMPro;

public class SelectableCharacter : MonoBehaviour, ICombatActor, ICombatAnimationEventSource
{
	public string characterName;
	public bool isSelected = false;

	private Renderer rend;
	private Color originalColor;
	private NavMeshAgent agent;
	private CharacterStats stats;
	private Inventory inventory;
	private SkillSet skillSet;
	private EquipmentManager equipmentManager;
	private Coroutine combatLoopRoutine;
	private GameObject activeActionLabel;
	private readonly CombatSlotReservation reservedTargetSlot = new CombatSlotReservation();

	// Athletics XP: award XP per unit of distance traveled
	private Vector3 lastPosition;
	private const float AthleticsXPPerUnit = 0.05f;

	public TMP_Text nameText;
	public Slider hungerSlider;
	public Slider thirstSlider;
	public Slider tirednessSlider;

	[SerializeField] private SkillsPanel skillsPanel;

	[Tooltip("World-space Canvas prefab shown above the character while harvesting. Assign the HarvestingLabel prefab here.")]
	[SerializeField] private GameObject harvestingLabelPrefab;

	[Header("Combat")]
	[SerializeField] private CombatFormulaConfig combatFormulaConfig;
	[SerializeField] private bool canUseEquipment = true;
	[SerializeField] private bool canBlock = true;
	[SerializeField] private bool canCounter = true;

	public Transform ActorTransform => transform;
	public CharacterStats Stats => stats;
	public SkillSet Skills => skillSet;
	public EquipmentManager Equipment => equipmentManager;
	public bool CanUseEquipment => canUseEquipment;
	public bool CanBlock => canBlock;
	public bool CanCounter => canCounter;

	public event System.Action<CombatAnimationEventArgs> OnAttackWindup;
	public event System.Action<CombatAnimationEventArgs> OnAttackImpact;
	public event System.Action<CombatAnimationEventArgs> OnReactionResolved;

	void Start()
	{
		rend = GetComponentInChildren<Renderer>();
		originalColor = rend.material.color;
		agent = GetComponent<NavMeshAgent>();
		stats = GetComponent<CharacterStats>();
		inventory = GetComponent<Inventory>();
		skillSet = GetComponent<SkillSet>();
		equipmentManager = GetComponent<EquipmentManager>();
		lastPosition = transform.position;

		// Initialize sliders
		hungerSlider.maxValue = 100f;
		thirstSlider.maxValue = 100f;
		tirednessSlider.maxValue = 100f;
	}

	private void Update()
	{
		// Athletics: grant XP proportional to distance walked
		float moved = Vector3.Distance(transform.position, lastPosition);
		if (moved > 0.01f)
		{
			skillSet?.GainXP(SkillType.Athletics, moved * AthleticsXPPerUnit);
			lastPosition = transform.position;
		}

		if (isSelected)
		{
			nameText.text = characterName;
			hungerSlider.value = stats.hunger;
			thirstSlider.value = stats.thirst;
			tirednessSlider.value = stats.tiredness;
		}
	}

	public void SetSelected(bool selected)
	{
		isSelected = selected;
		rend.material.color = selected ? Color.green : originalColor;

		if (selected)
		{
			if (skillSet == null)
				Debug.LogWarning($"[SkillsPanel] {characterName} has no SkillSet component — skills panel will stay blank.");
			skillsPanel?.Bind(skillSet);
		}
		else
			skillsPanel?.Unbind();
	}

	public void MoveTo(Vector3 position)
	{
		if (agent != null)
			agent.SetDestination(position);
	}

	/// <summary>Player move order: drops whatever the character was doing and walks there.</summary>
	public void CommandMove(Vector3 position)
	{
		if (!IsAlive)
			return;

		CancelCurrentAction();
		MoveTo(position);
	}

	public void InteractWith(IInteractable target, string actionId)
	{
		if (!IsAlive)
			return;

		CancelCurrentAction();

		if (IsAttackAction(actionId))
			combatLoopRoutine = StartCoroutine(CombatLoop(target, actionId));
		else
			StartCoroutine(MoveAndPerform(target, actionId));
	}

	private bool IsAlive => stats == null || stats.IsAlive;

	private void CancelCurrentAction()
	{
		StopAllCoroutines();
		combatLoopRoutine = null;
		ClearActionLabel();
		ReleaseReservedSlot();
	}

	// Walks into range, swings, waits out the attack interval, repeats until the target dies,
	// disappears, or the player gives another order.
	private IEnumerator CombatLoop(IInteractable target, string actionId)
	{
		if (combatFormulaConfig == null)
		{
			Debug.LogWarning($"[Combat] {characterName} has no CombatFormulaConfig assigned.");
			yield break;
		}

		while (IsTargetValid(target) && stats != null && stats.IsAlive)
		{
			if (target is not Component targetComponent)
				break;

			CharacterStats targetStats = targetComponent.GetComponent<CharacterStats>();
			if (targetStats != null && !targetStats.IsAlive)
				break;

			AttackProfile profile = CombatFormulaResolver.BuildAttackProfile(this, combatFormulaConfig);
			if (Vector3.Distance(transform.position, targetComponent.transform.position) > profile.Range)
			{
				MoveTo(targetComponent.transform.position);
				yield return null;
				continue;
			}

			if (agent != null)
				agent.ResetPath();

			target.ExecuteInteraction(this, actionId);
			yield return new WaitForSeconds(Mathf.Max(0.1f, profile.Interval));
		}

		combatLoopRoutine = null;
		ReleaseReservedSlot();
	}

	private static bool IsAttackAction(string actionId)
	{
		if (string.IsNullOrWhiteSpace(actionId))
			return false;

		return actionId.IndexOf("attack", System.StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private IEnumerator MoveAndPerform(IInteractable target, string actionId)
	{
		Vector3 dest = target.GetInteractionPoint();
		MoveTo(dest);

		while (Vector3.Distance(transform.position, dest) > 2f)
		{
			if (!CanStillPerform(target, actionId))
				yield break;
			yield return null;
		}

		// Some actions (e.g. Harvest) take time. Scale duration by the character's harvest speed.
		float duration = target.GetInteractionDuration(actionId);
		if (duration > 0f)
		{
			float scaledDuration = duration / stats.HarvestSpeedMultiplier;

			// Show a floating action label above the character while waiting.
			if (harvestingLabelPrefab != null)
			{
				activeActionLabel = Instantiate(harvestingLabelPrefab);
				activeActionLabel.transform.SetParent(transform);
				var actionLabel = activeActionLabel.GetComponent<HarvestingLabel>();
				if (actionLabel != null)
				{
					string text = target.GetInteractionLabel(actionId);
					if (!string.IsNullOrEmpty(text))
						actionLabel.SetText(text);
				}
			}

			yield return new WaitForSeconds(scaledDuration);

			ClearActionLabel();
		}

		// Another character may have picked up or used up the target while we walked or worked.
		if (CanStillPerform(target, actionId))
			target.ExecuteInteraction(this, actionId);
	}

	private void ClearActionLabel()
	{
		if (activeActionLabel != null)
			Destroy(activeActionLabel);
		activeActionLabel = null;
	}

	private bool CanStillPerform(IInteractable target, string actionId)
	{
		if (!IsTargetValid(target))
			return false;

		foreach (var option in target.GetAvailableInteractions(this))
			if (option.id == actionId)
				return true;
		return false;
	}

	private static bool IsTargetValid(IInteractable target)
	{
		// Destroyed Unity objects only compare equal to null through UnityEngine.Object's
		// operator, which a plain interface null check bypasses.
		return target is UnityEngine.Object unityObject ? unityObject != null : target != null;
	}

	public void Drink(float amount)
	{
		Debug.Log($"{characterName} is drinking");
		stats?.Drink(amount);
	}

	public void Eat(float nutrition)
	{
		Debug.Log($"{characterName} is eating");
		stats?.Eat(nutrition);
	}
	public void Sleep()
	{
		Debug.Log($"{characterName} is sleeping");
		stats?.Sleep(8f);
	}
	public void Attack(IInteractable target)
	{
		if (combatFormulaConfig == null)
		{
			Debug.LogWarning($"[Combat] {characterName} has no CombatFormulaConfig assigned.");
			return;
		}

		if (target is not Component targetComponent)
		{
			Debug.LogWarning($"[Combat] {characterName} attacked a non-component target and cannot resolve combat.");
			return;
		}

		if (stats != null && !stats.IsAlive)
			return;

		// Target already has its maximum number of attackers; CombatLoop keeps us close and retries.
		if (!TryReserveTargetSlot(targetComponent))
			return;

		AttackProfile attackerProfile = CombatFormulaResolver.BuildAttackProfile(this, combatFormulaConfig);

		// CombatLoop walks into range before calling Attack, so this only trips if the target moved away.
		float distance = Vector3.Distance(transform.position, targetComponent.transform.position);
		if (distance > attackerProfile.Range)
			return;

		CharacterStats defenderStats = targetComponent.GetComponent<CharacterStats>();
		if (defenderStats == null)
		{
			Debug.Log($"[Combat] {characterName} found no combat stats on target; awarding training XP only.");
			skillSet?.GainXP(attackerProfile.UsedWeaponSkill, 5f);
			return;
		}

		if (!defenderStats.IsAlive)
		{
			ReleaseReservedSlot();
			return;
		}

		OnAttackWindup?.Invoke(new CombatAnimationEventArgs(gameObject, targetComponent.gameObject, CombatReactionType.None, 0f));

		ICombatActor defender = ResolveDefender(targetComponent, defenderStats);
		ResolveExchange(defender, attackerProfile, defenderStats);
	}

	private ICombatActor ResolveDefender(Component targetComponent, CharacterStats defenderStats)
	{
		if (targetComponent.TryGetComponent<ICombatActor>(out var defenderActor))
			return defenderActor;

		EquipmentManager defenderEquipment = targetComponent.GetComponent<EquipmentManager>();
		bool canUseEquipmentProxy = defenderEquipment != null;
		return new CombatActorProxy(
			targetComponent.transform,
			defenderStats,
			targetComponent.GetComponent<SkillSet>(),
			defenderEquipment,
			canUseEquipment: canUseEquipmentProxy,
			canBlock: canUseEquipmentProxy,
			canCounter: true);
	}

	private void ResolveExchange(ICombatActor defender, AttackProfile attackerProfile, CharacterStats defenderStats)
	{
		CombatExchangeResult result = CombatExchangeResolver.ResolveAndApply(
			attackerProfile,
			this,
			defender,
			defenderStats,
			combatFormulaConfig,
			gameObject,
			reaction => OnReactionResolved?.Invoke(new CombatAnimationEventArgs(gameObject, defender.ActorTransform.gameObject, reaction, 0f)));

		if (result.HitLanded)
		{
			OnAttackImpact?.Invoke(new CombatAnimationEventArgs(gameObject, defender.ActorTransform.gameObject, result.Reaction, result.DamageDealt));
			Debug.Log($"[Combat] {characterName} hit for {result.DamageDealt:F1} ({result.Reaction} failed).");
			skillSet?.GainXP(attackerProfile.UsedWeaponSkill, 10f);
			skillSet?.GainXP(SkillType.Strength, 2f);
		}
		else
		{
			if (result.CounterDamage > 0f)
				Debug.Log($"[Combat] {characterName}'s attack was countered for {result.CounterDamage:F1} damage.");

			Debug.Log($"[Combat] {characterName}'s attack was prevented by {result.Reaction}.");
			skillSet?.GainXP(attackerProfile.UsedWeaponSkill, 3f);
		}

		if (!defenderStats.IsAlive)
			ReleaseReservedSlot();
	}

	private bool TryReserveTargetSlot(Component targetComponent)
	{
		return reservedTargetSlot.TryReserve(gameObject, targetComponent);
	}

	private void OnDisable()
	{
		CancelCurrentAction();
	}

	private void ReleaseReservedSlot()
	{
		reservedTargetSlot.Release(gameObject);
	}

	public void collectItem(Item item, float harvestXP = 15f)
	{
		if (item == null)
		{
			Debug.LogWarning($"{characterName} tried to collect an item, but no Item asset is assigned.");
			return;
		}
		Debug.Log($"{characterName} is collecting {item.itemName}");
		inventory?.AddItem(item);
		skillSet?.GainXP(SkillType.Harvesting, harvestXP);
	}

}
