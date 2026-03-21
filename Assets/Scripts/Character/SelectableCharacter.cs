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
	private Coroutine pendingAttackRoutine;
	private Coroutine combatLoopRoutine;
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

	public void InteractWith(IInteractable target, string actionId)
	{
		if (IsAttackAction(actionId))
		{
			StopAllCoroutines();
			combatLoopRoutine = StartCoroutine(CombatLoop(target, actionId));
			return;
		}

		if (!string.Equals(actionId, "attack", System.StringComparison.OrdinalIgnoreCase))
			ReleaseReservedSlot();

		StopAllCoroutines();
		StartCoroutine(MoveAndPerform(target, actionId));
	}

	private IEnumerator CombatLoop(IInteractable target, string actionId)
	{
		while (target != null && stats != null && stats.IsAlive)
		{
			if (target is Component targetComponent)
			{
				CharacterStats targetStats = targetComponent.GetComponent<CharacterStats>();
				if (targetStats != null && !targetStats.IsAlive)
					break;
			}

			target.ExecuteInteraction(this, actionId);
			yield return new WaitForSeconds(GetCurrentAttackInterval());
		}

		combatLoopRoutine = null;
		ReleaseReservedSlot();
	}

	private float GetCurrentAttackInterval()
	{
		if (combatFormulaConfig == null)
			return 0.8f;

		AttackProfile profile = CombatFormulaResolver.BuildAttackProfile(this, combatFormulaConfig);
		return Mathf.Max(0.1f, profile.Interval);
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
			yield return null;
		}

		// Some actions (e.g. Harvest) take time. Scale duration by the character's harvest speed.
		float duration = target.GetInteractionDuration(actionId);
		if (duration > 0f)
		{
			float scaledDuration = duration / stats.HarvestSpeedMultiplier;

			// Show a floating action label above the character while waiting.
			GameObject label = null;
			if (harvestingLabelPrefab != null)
			{
					label = Instantiate(harvestingLabelPrefab);
						label.transform.SetParent(transform);
						// label.transform.localPosition = Vector3.up * 2.5f;
						// label.transform.localRotation = Quaternion.identity;
				var actionLabel = label.GetComponent<HarvestingLabel>();
				if (actionLabel != null)
				{
					string text = target.GetInteractionLabel(actionId);
					if (!string.IsNullOrEmpty(text))
						actionLabel.SetText(text);
				}
			}

			yield return new WaitForSeconds(scaledDuration);

			if (label != null)
				Destroy(label);
		}

		target.ExecuteInteraction(this, actionId);
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

		if (!TryReserveTargetSlot(targetComponent))
		{
			MoveTo(targetComponent.transform.position);
			return;
		}

		Debug.Log($"{characterName} is attacking {target}");
		AttackProfile attackerProfile = CombatFormulaResolver.BuildAttackProfile(this, combatFormulaConfig);
		Debug.Log($"[Combat] {characterName} cadence interval: {attackerProfile.Interval:F2}s");

		float distance = Vector3.Distance(transform.position, targetComponent.transform.position);
		if (distance > attackerProfile.Range)
		{
			MoveTo(targetComponent.transform.position);
			if (pendingAttackRoutine != null)
				StopCoroutine(pendingAttackRoutine);
			pendingAttackRoutine = StartCoroutine(RetryAttackInRange(target, targetComponent, attackerProfile.Range));
			Debug.Log($"[Combat] {characterName} is out of range ({distance:F1} > {attackerProfile.Range:F1}) and moves closer.");
			return;
		}

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
			if (result.Reaction == CombatReactionType.Block)
				skillSet?.GainXP(SkillType.Strength, 1f);

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
		if (combatLoopRoutine != null)
			StopCoroutine(combatLoopRoutine);

		ReleaseReservedSlot();
	}

	private void ReleaseReservedSlot()
	{
		reservedTargetSlot.Release(gameObject);
	}

	private IEnumerator RetryAttackInRange(IInteractable target, Component targetComponent, float range)
	{
		while (targetComponent != null && Vector3.Distance(transform.position, targetComponent.transform.position) > range)
			yield return null;

		pendingAttackRoutine = null;
		if (targetComponent != null)
			Attack(target);
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
