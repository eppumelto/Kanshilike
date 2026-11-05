using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A person in the world: walks, fights, carries things, eats and carries out orders. Selection
/// lives in PlayerControllable and the on-screen status in CharacterHud, so a Character with no
/// player attached (a bandit, a trader) works the same way.
/// </summary>
public class Character : CombatActor, ICombatAnimationEventSource
{
	public string characterName;

	private NavMeshAgent agent;
	private Inventory inventory;
	private Coroutine combatLoopRoutine;
	private GameObject activeActionLabel;
	private readonly CombatSlotReservation reservedTargetSlot = new CombatSlotReservation();
	private static readonly Vector3 LabelOffset = Vector3.up * 1.6f;
	private const float FaceAttackerSeconds = 3f;
	private Transform combatOpponent;
	private Transform lastAttacker;
	private float lastAttackedTime;

	private const float MaxShootingAngle = 15f;
	private const float ArrowSpawnForward = 0.6f;
	private const float ArrowSpawnUp = 0.3f;
	private const float ArrowShotXP = 2f;
	private const float DefaultBodyRadius = 0.5f;
	// Shared by all characters: only used within a single check.
	private static readonly RaycastHit[] ClearShotHits = new RaycastHit[16];

	// Athletics XP: award XP per unit of distance traveled
	private Vector3 lastPosition;
	private const float AthleticsXPPerUnit = 0.05f;

	[Tooltip("World-space Canvas prefab shown above the character while harvesting. Assign the HarvestingLabel prefab here.")]
	[SerializeField] private GameObject harvestingLabelPrefab;

	[Header("Combat")]
	[SerializeField] private Faction faction = Faction.Player;
	[SerializeField] private CombatFormulaConfig combatFormulaConfig;
	[SerializeField] private bool canUseEquipment = true;
	[SerializeField] private bool canBlock = true;
	[SerializeField] private bool canCounter = true;

	[Header("Auto Eat / Drink")]
	[Tooltip("Eats food from the inventory whenever hunger drops below this (0-100). 0 = never.")]
	[SerializeField, Range(0f, 100f)] private float autoEatBelow = 40f;
	[Tooltip("Drinks from the inventory whenever thirst drops below this (0-100). 0 = never.")]
	[SerializeField, Range(0f, 100f)] private float autoDrinkBelow = 40f;
	private const float AutoConsumeCheckSeconds = 1f;
	private float nextAutoConsumeCheck;

	[Header("Carrying")]
	[Tooltip("Kilograms an untrained character carries before slowing down badly. Strength raises it (x2 at master level).")]
	[SerializeField, Min(1f)] private float baseCarryCapacity = 30f;
	// Strength XP per unit walked under a heavy load (scaled by how heavy).
	private const float StrengthXPPerUnit = 0.1f;
	private float baseMoveSpeed;

	/// <summary>Kilograms in the inventory plus everything equipped.</summary>
	public float CarryWeight =>
		(inventory != null ? inventory.TotalWeight : 0f) + (equipment != null ? equipment.TotalWeight : 0f);

	public float CarryCapacity => baseCarryCapacity * (skills != null ? skills.GetBonus(SkillType.Strength) : 1f);

	/// <summary>Carried weight as a fraction of capacity: 0.5 is where it starts to slow you, 1 is full.</summary>
	public float Encumbrance => CarryWeight / Mathf.Max(1f, CarryCapacity);

	public string EncumbranceLabel => Encumbrance switch
	{
		<= 0.5f => "Light",
		<= 1f => "Heavy",
		<= 1.5f => "Overloaded",
		_ => "Crushed"
	};

	/// <summary>Movement speed factor: full speed up to half capacity, 80% at capacity, a crawl when overloaded.</summary>
	public static float SpeedMultiplierFor(float encumbrance)
	{
		if (encumbrance <= 0.5f) return 1f;
		if (encumbrance <= 1f) return Mathf.Lerp(1f, 0.8f, (encumbrance - 0.5f) / 0.5f);
		if (encumbrance <= 1.5f) return Mathf.Lerp(0.8f, 0.4f, (encumbrance - 1f) / 0.5f);
		return 0.25f;
	}

	public override Faction Faction => faction;
	public override bool CanUseEquipment => canUseEquipment;
	public override bool CanBlock => canBlock;
	public override bool CanDodge => true;
	public override bool CanCounter => canCounter;

	public event System.Action<CombatAnimationEventArgs> OnAttackWindup;
	public event System.Action<CombatAnimationEventArgs> OnAttackImpact;
	public event System.Action<CombatAnimationEventArgs> OnReactionResolved;

	void Start()
	{
		agent = GetComponent<NavMeshAgent>();
		inventory = GetComponent<Inventory>();
		lastPosition = transform.position;
		if (agent != null)
			baseMoveSpeed = agent.speed;

		if (stats != null)
		{
			stats.OnDied += HandleDied;
			stats.OnAttacked += HandleAttacked;
		}
	}

	private void Update()
	{
		// Athletics: grant XP proportional to distance walked
		float moved = Vector3.Distance(transform.position, lastPosition);
		float encumbrance = Encumbrance;
		if (moved > 0.01f)
		{
			skills?.GainXP(SkillType.Athletics, moved * AthleticsXPPerUnit);

			// Kenshi-style: hauling a heavy load trains Strength.
			if (encumbrance > 0.5f)
				skills?.GainXP(SkillType.Strength, moved * StrengthXPPerUnit * Mathf.Min(2f, (encumbrance - 0.5f) * 2f));

			lastPosition = transform.position;
		}

		if (agent != null && agent.enabled && baseMoveSpeed > 0f)
			agent.speed = baseMoveSpeed * SpeedMultiplierFor(encumbrance);

		FaceOpponent();

		if (IsAlive && Time.time >= nextAutoConsumeCheck)
		{
			nextAutoConsumeCheck = Time.time + AutoConsumeCheckSeconds;
			AutoConsume();
		}
	}

	// Standing fighters turn to whoever they fight or whoever just swung at them, which is what
	// lets a shield cover them. While walking the agent steers, so a fleeing back stays exposed.
	private void FaceOpponent()
	{
		if (!IsAlive || agent == null || !agent.enabled || agent.velocity.sqrMagnitude > 0.04f)
			return;

		Transform opponent = combatOpponent;
		if (opponent == null && lastAttacker != null && Time.time - lastAttackedTime <= FaceAttackerSeconds)
			opponent = lastAttacker;

		if (opponent != null)
			CombatFacing.TurnToward(transform, opponent.position);
	}

	private void HandleAttacked(GameObject attacker, bool blockedWithShield)
	{
		lastAttacker = attacker != null ? attacker.transform : null;
		lastAttackedTime = Time.time;

		if (blockedWithShield)
			ShowFloatingText("Blocked!", 0.6f);
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

	public void InteractWith(IInteractable target, InteractionOption option)
	{
		if (!IsAlive)
			return;

		CancelCurrentAction();

		if (option.kind == InteractionKind.Attack)
			combatLoopRoutine = StartCoroutine(CombatLoop(target, option.id));
		else
			StartCoroutine(MoveAndPerform(target, option.id));
	}

	private void HandleDied(GameObject killer)
	{
		CancelCurrentAction();
		// Gear goes into the bag so the body can be looted.
		InventoryActions.UnequipAllInto(equipment, inventory);
	}

	private void CancelCurrentAction()
	{
		StopAllCoroutines();
		combatLoopRoutine = null;
		combatOpponent = null;
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

		float nextShotTime = 0f;

		while (IsTargetValid(target) && stats != null && stats.IsAlive)
		{
			if (target is not Component targetComponent)
				break;

			CharacterStats targetStats = targetComponent.GetComponent<CharacterStats>();
			if (targetStats != null && !targetStats.IsAlive)
				break;

			combatOpponent = targetComponent.transform;

			BowItem bow = CombatFormulaResolver.GetBow(this);
			if (bow != null && (inventory == null || inventory.FindFirst<ArrowItem>() == null))
			{
				ShowFloatingText("Out of arrows", 2f);
				break;
			}

			AttackProfile profile = CombatFormulaResolver.BuildAttackProfile(this, combatFormulaConfig);
			float distance = Vector3.Distance(transform.position, targetComponent.transform.position);
			if (bow != null && distance > bow.maxPursuitDistance)
			{
				ShowFloatingText("Target got away", 2f);
				break;
			}

			bool inRange = distance <= profile.Range;
			if (!inRange || (bow != null && !HasGoodShot(targetComponent, bow, distance)))
			{
				MoveTo(targetComponent.transform.position);
				yield return null;
				continue;
			}

			if (agent != null)
				agent.ResetPath();

			// FaceOpponent is turning us; an archer waits until the target is in front.
			if (bow != null && (Time.time < nextShotTime || !IsFacing(targetComponent.transform.position)))
			{
				yield return null;
				continue;
			}

			target.ExecuteInteraction(this, actionId);

			// Between shots an archer keeps following: the checks above run again while reloading.
			if (bow != null)
			{
				nextShotTime = Time.time + Mathf.Max(0.1f, profile.Interval);
				yield return null;
				continue;
			}

			yield return new WaitForSeconds(Mathf.Max(0.1f, profile.Interval));
		}

		// The fight is over: don't keep walking to where the target last was.
		if (agent != null && agent.isOnNavMesh)
			agent.ResetPath();

		combatLoopRoutine = null;
		combatOpponent = null;
		ReleaseReservedSlot();
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
				activeActionLabel = SpawnLabel();
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

		BowItem bow = CombatFormulaResolver.GetBow(this);

		// Target already has its maximum number of attackers; CombatLoop keeps us close and retries.
		// Archers shoot from a distance, so they don't take up a place around the target.
		if (bow == null && !TryReserveTargetSlot(targetComponent))
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
			skills?.GainXP(attackerProfile.UsedWeaponSkill, 5f);
			return;
		}

		if (!defenderStats.IsAlive)
		{
			ReleaseReservedSlot();
			return;
		}

		OnAttackWindup?.Invoke(new CombatAnimationEventArgs(gameObject, targetComponent.gameObject, CombatReactionType.None, 0f));

		if (bow != null)
		{
			ShootArrow(targetComponent, bow, attackerProfile);
			return;
		}

		ICombatActor defender = CombatExchangeResolver.ResolveDefender(targetComponent, defenderStats);
		ResolveExchange(defender, attackerProfile, defenderStats);
	}

	// Spends one arrow and sends it toward where the target will be, strayed by the archer's spread.
	private void ShootArrow(Component target, BowItem bow, AttackProfile attackProfile)
	{
		Inventory.Entry arrows = inventory != null ? inventory.FindFirst<ArrowItem>() : null;
		if (arrows == null)
			return;

		ArrowProjectile prefab = ((ArrowItem)arrows.item).projectilePrefab;
		if (prefab == null)
		{
			Debug.LogWarning($"[Combat] {arrows.item.itemName} has no projectile prefab assigned.");
			return;
		}

		inventory.RemoveOne(arrows);

		Vector3 targetVelocity = VelocityOf(target);
		Vector3 origin = BodyCenter(this) + transform.forward * ArrowSpawnForward + Vector3.up * ArrowSpawnUp;
		Vector3 aimPoint = BodyCenter(target);
		float flightSeconds = Vector3.Distance(origin, aimPoint) / bow.arrowSpeed;
		aimPoint += targetVelocity * flightSeconds;

		// Lobbed just enough that gravity brings it down on the aim point.
		Vector3 velocity = (aimPoint - origin) / flightSeconds - 0.5f * flightSeconds * Physics.gravity;

		float spread = CombatFormulaResolver.GetBowSpread(this, bow, combatFormulaConfig, targetVelocity.magnitude);
		Vector2 stray = Random.insideUnitCircle * spread;
		velocity = Quaternion.AngleAxis(stray.x, Vector3.up) * Quaternion.AngleAxis(stray.y, transform.right) * velocity;

		ArrowProjectile projectile = Instantiate(prefab, origin, Quaternion.LookRotation(velocity));
		projectile.Launch(velocity, attackProfile, combatFormulaConfig, gameObject,
			(defender, result) => HandleArrowHit(defender, result, attackProfile.UsedWeaponSkill));

		skills?.GainXP(attackProfile.UsedWeaponSkill, ArrowShotXP);
	}

	// Runs when the arrow lands, by which time the archer may be gone.
	private void HandleArrowHit(ICombatActor defender, CombatExchangeResult result, SkillType skill)
	{
		if (this == null)
			return;

		GameObject defenderObject = defender.ActorTransform.gameObject;
		OnReactionResolved?.Invoke(new CombatAnimationEventArgs(gameObject, defenderObject, result.Reaction, 0f));

		if (result.HitLanded)
		{
			OnAttackImpact?.Invoke(new CombatAnimationEventArgs(gameObject, defenderObject, result.Reaction, result.DamageDealt));
			Debug.Log($"[Combat] {characterName}'s arrow hit {defenderObject.name} for {result.DamageDealt:F1}.");
			skills?.GainXP(skill, 10f);
		}
		else
		{
			Debug.Log($"[Combat] {characterName}'s arrow was blocked by {defenderObject.name}.");
			skills?.GainXP(skill, 3f);
		}
	}

	// Worth an arrow? Otherwise the archer holds fire and moves closer.
	private bool HasGoodShot(Component target, BowItem bow, float distance)
	{
		if (!HasClearShot(target))
			return false;

		float chance = CombatFormulaResolver.GetBowHitChance(this, bow, combatFormulaConfig, distance, VelocityOf(target).magnitude, BodyRadius(target));
		return chance >= bow.minHitChance;
	}

	private static Vector3 VelocityOf(Component target)
	{
		return target.TryGetComponent<NavMeshAgent>(out var targetAgent) ? targetAgent.velocity : Vector3.zero;
	}

	private static float BodyRadius(Component body)
	{
		Collider bodyCollider = body.GetComponent<Collider>();
		if (bodyCollider == null)
			return DefaultBodyRadius;

		Vector3 extents = bodyCollider.bounds.extents;
		return Mathf.Max(extents.x, extents.z);
	}

	// Only the world blocks a shot. Characters in the way don't stop the archer firing, and may catch the arrow.
	private bool HasClearShot(Component target)
	{
		Vector3 from = BodyCenter(this);
		Vector3 toTarget = BodyCenter(target) - from;
		int count = Physics.RaycastNonAlloc(from, toTarget.normalized, ClearShotHits, toTarget.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

		for (int i = 0; i < count; i++)
		{
			if (ClearShotHits[i].collider.GetComponentInParent<CharacterStats>() == null)
				return false;
		}

		return true;
	}

	private bool IsFacing(Vector3 point)
	{
		Vector3 toPoint = point - transform.position;
		toPoint.y = 0f;
		return Vector3.Angle(transform.forward, toPoint) <= MaxShootingAngle;
	}

	private static Vector3 BodyCenter(Component body)
	{
		Collider bodyCollider = body.GetComponent<Collider>();
		return bodyCollider != null ? bodyCollider.bounds.center : body.transform.position;
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
			string defense = result.Reaction == CombatReactionType.None ? "undefended" : $"{result.Reaction} failed";
			Debug.Log($"[Combat] {characterName} hit for {result.DamageDealt:F1} ({defense}).");
			skills?.GainXP(attackerProfile.UsedWeaponSkill, 10f);
			skills?.GainXP(SkillType.Strength, 2f);
		}
		else
		{
			if (result.CounterDamage > 0f)
				Debug.Log($"[Combat] {characterName}'s attack was countered for {result.CounterDamage:F1} damage.");

			Debug.Log($"[Combat] {characterName}'s attack was prevented by {result.Reaction}.");
			skills?.GainXP(attackerProfile.UsedWeaponSkill, 3f);
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

	private void OnDestroy()
	{
		if (stats != null)
		{
			stats.OnDied -= HandleDied;
			stats.OnAttacked -= HandleAttacked;
		}
	}

	private void ReleaseReservedSlot()
	{
		reservedTargetSlot.Release(gameObject);
	}

	/// <summary>
	/// Harvests one item into this character's inventory and grants Harvesting XP. Returns false
	/// if it did not fit.
	/// </summary>
	public bool collectItem(Item item, float harvestXP = 15f)
	{
		if (CollectItems(item, 1) <= 0)
			return false;

		skills?.GainXP(SkillType.Harvesting, harvestXP);
		return true;
	}

	/// <summary>
	/// Puts up to <paramref name="count"/> of the item in this character's inventory. Returns how
	/// many were taken. Shows "Inventory full" when some did not fit. Grants no XP: picking things
	/// up is not harvesting.
	/// </summary>
	public int CollectItems(Item item, int count)
	{
		if (item == null)
		{
			Debug.LogWarning($"{characterName} tried to collect an item, but no Item asset is assigned.");
			return 0;
		}

		count = Mathf.Max(1, count);
		int taken = inventory == null ? 0 : count - inventory.Add(item, count);
		if (taken < count)
			ShowFloatingText("Inventory full", 2f);
		if (taken <= 0)
			return 0;

		Debug.Log($"{characterName} collected {taken} {item.itemName}");
		return taken;
	}

	/// <summary>
	/// Eats or drinks one item from a stack in this character's inventory. Returns false if
	/// it holds nothing consumable.
	/// </summary>
	public bool TryConsume(Inventory.Entry entry)
	{
		if (inventory == null || !IsAlive || !inventory.Contains(entry))
			return false;

		if (entry.item is FoodItem food)
		{
			Eat(food.nutritionValue);
			inventory.RemoveOne(entry);
		}
		else if (entry.item is DrinkItem drink)
		{
			Drink(drink.hydrationValue);
			Vector2Int pos = entry.position;
			bool rotated = entry.rotated;
			bool lastOne = entry.count == 1;
			inventory.RemoveOne(entry);

			// Keep the empty container (waterskin) so it can be refilled at a well,
			// in the same spot when that was the last one of the stack.
			Item empty = drink.emptyContainer;
			if (empty != null
				&& !(lastOne && inventory.Place(empty, 1, pos, rotated))
				&& !inventory.AddItem(empty))
				ShowFloatingText($"No room, dropped {empty.itemName}", 2f);
		}
		else
			return false;

		return true;
	}

	/// <summary>Refills every empty container this drink comes in (e.g. empty waterskins). Returns how many.</summary>
	public int RefillContainers(DrinkItem drink)
	{
		if (inventory == null || drink == null || drink.emptyContainer == null)
			return 0;

		int filled = inventory.ReplaceAll(drink.emptyContainer, drink);
		if (filled > 0)
			ShowFloatingText($"Filled {filled} {drink.itemName}", 2f);
		return filled;
	}

	// Kenshi-style: characters feed themselves from their own inventory when they get hungry
	// or thirsty, one item per check, until they are above the threshold or run out.
	private void AutoConsume()
	{
		if (inventory == null || stats == null || !stats.hasNeeds)
			return;

		if (stats.hunger < autoEatBelow)
		{
			var food = inventory.FindFirst<FoodItem>();
			if (food != null)
				TryConsume(food);
		}

		if (stats.thirst < autoDrinkBelow)
		{
			var drink = inventory.FindFirst<DrinkItem>();
			if (drink != null)
				TryConsume(drink);
		}
	}

	private GameObject SpawnLabel()
	{
		GameObject label = Instantiate(harvestingLabelPrefab, transform);
		label.transform.localPosition = LabelOffset;
		return label;
	}

	private void ShowFloatingText(string text, float seconds)
	{
		if (harvestingLabelPrefab == null)
			return;

		GameObject label = SpawnLabel();
		var labelText = label.GetComponent<HarvestingLabel>();
		if (labelText != null)
			labelText.SetText(text);
		Destroy(label, seconds);
	}

}
