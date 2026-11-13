using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

/// <summary>
/// A person in the world: walks, fights, carries things, eats and carries out orders. Selection
/// lives in PlayerControllable and the on-screen status in CharacterHud, so a Character with no
/// player attached (a bandit, a trader) works the same way.
/// </summary>
public class Character : CombatActor
{
	public string characterName;

	private NavMeshAgent agent;
	private Inventory inventory;
	private GameObject activeActionLabel;
	private static readonly Vector3 LabelOffset = Vector3.up * 1.6f;
	private const float FaceAttackerSeconds = 3f;
	private CharacterOrder currentOrder;
	private Coroutine orderRoutine;
	private Transform lastAttacker;
	private float lastAttackedTime;

	private Vector3 lastPosition;

	[Tooltip("World-space Canvas prefab shown above the character while harvesting. Assign the HarvestingLabel prefab here.")]
	[SerializeField] private GameObject harvestingLabelPrefab;

	[Header("Combat")]
	[Tooltip("This person's own faction. Squad members always count as Player; a community member uses its community's faction.")]
	[SerializeField] private FactionDefinition ownFaction;
	// The old Faction enum value (0 Player, 1 Wildlife), read from scenes saved before factions were
	// assets. Used only while ownFaction is unset; Kanshilike > Factions > Migrate fills ownFaction.
	[SerializeField, HideInInspector, FormerlySerializedAs("faction")] private int legacyFaction = 0;
	private FactionResolution factionResolution;
	private int factionVersion;
	private bool factionCached;
	[SerializeField] private CombatFormulaConfig combatFormulaConfig;
	[SerializeField] private bool canUseEquipment = true;
	[SerializeField] private bool canBlock = true;
	[SerializeField] private bool canCounter = true;
	[Tooltip("When attacked while idle or busy with a task, the character fights back, but gives up chasing the attacker beyond this distance.")]
	[SerializeField, Min(0f)] private float selfDefenseChaseDistance = 15f;

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

	[Header("Training")]
	[Tooltip("Athletics XP per meter walked.")]
	[SerializeField, Min(0f)] private float athleticsXPPerMeter = 0.05f;
	[Tooltip("Strength XP per meter walked under a heavy load, scaled by how heavy.")]
	[SerializeField, Min(0f)] private float strengthXPPerLoadedMeter = 0.1f;
	[Tooltip("Harvesting XP per item harvested.")]
	[SerializeField, Min(0f)] private float harvestXP = 15f;
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

	/// <summary>This person's own faction asset, or the legacy enum mapping when the asset is unset.</summary>
	public FactionDefinition OwnFaction => ownFaction != null ? ownFaction : FactionRelations.FromLegacy(legacyFaction);

	/// <summary>True while the faction still comes from the pre-asset enum field.</summary>
	public bool UsesLegacyFaction => ownFaction == null;

	/// <summary>The effective faction: squad, then community, then own (FactionRules.Resolve). Cached.</summary>
	public override FactionId Faction => FactionResolution.Id;

	public FactionResolution FactionResolution
	{
		get
		{
			if (!factionCached || factionVersion != CharacterFaction.Version)
			{
				FactionDefinition own = OwnFaction;
				FactionRelations.Register(own);

				PlayerControllable squad = GetComponent<PlayerControllable>();
				bool inSquad = squad != null && squad.isActiveAndEnabled;

				FactionId community = FactionId.None;
				if (CharacterFaction.CommunitySource != null)
					CharacterFaction.CommunitySource.TryGetCommunityFaction(this, out community, out _);

				factionResolution = FactionRules.Resolve(inSquad, community, own != null ? own.FactionId : FactionId.None, FactionRelations.PlayerId);
				factionVersion = CharacterFaction.Version;
				factionCached = true;
			}
			return factionResolution;
		}
	}

	/// <summary>Makes the next Faction read work it out again (squad membership changed).</summary>
	internal void InvalidateFaction() => factionCached = false;

	/// <summary>Changes this person's own faction at runtime.</summary>
	public void SetOwnFaction(FactionDefinition value)
	{
		ownFaction = value;
		InvalidateFaction();
		CharacterFaction.RaiseOwnFactionChanged(this);
	}

	/// <summary>What the character is doing now, or null when idle.</summary>
	public CharacterOrder CurrentOrder => currentOrder;
	internal NavMeshAgent Agent => agent;
	internal Inventory CarriedInventory => inventory;
	internal CombatFormulaConfig CombatConfig => combatFormulaConfig;
	/// <summary>Who the character is fighting; FaceOpponent turns toward them.</summary>
	internal Transform CombatOpponent { get; set; }
	public override bool CanUseEquipment => canUseEquipment;
	public override bool CanBlock => canBlock;
	public override bool CanDodge => true;
	public override bool CanCounter => canCounter;

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
			skills?.GainXP(SkillType.Athletics, moved * athleticsXPPerMeter);

			// Kenshi-style: hauling a heavy load trains Strength.
			if (encumbrance > 0.5f)
				skills?.GainXP(SkillType.Strength, moved * strengthXPPerLoadedMeter * Mathf.Min(2f, (encumbrance - 0.5f) * 2f));

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

		Transform opponent = CombatOpponent;
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

		if (currentOrder != null && currentOrder.StopsWhenAttacked)
		{
			EndCurrentOrder();
			ShowFloatingText("Interrupted", 2f);
		}

		DefendAgainst(attacker);
	}

	// Fights back against a hostile attacker, unless walking away or already fighting.
	private void DefendAgainst(GameObject attacker)
	{
		CombatActor enemy = attacker != null ? attacker.GetComponentInParent<CombatActor>() : null;
		// Anyone not allied fights back: a neutral townsperson who is punched hits back.
		bool fightsBack = enemy != null && enemy != this && enemy.IsAlive && Attitude.FightsBack(this, enemy);
		bool orderYields = currentOrder == null || currentOrder.YieldsToSelfDefence;

		if (CombatRules.ShouldDefendSelf(IsAlive, orderYields, fightsBack))
			GiveOrder(new AttackOrder(enemy, selfDefenseChaseDistance));
	}

	public void MoveTo(Vector3 position)
	{
		if (agent != null && agent.isActiveAndEnabled)
			agent.SetDestination(position);
	}

	internal void StopMoving()
	{
		if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
			agent.ResetPath();
	}

	/// <summary>Player move order: drops whatever the character was doing and walks there.</summary>
	public void CommandMove(Vector3 position)
	{
		GiveOrder(new MoveOrder(position));
	}

	public void InteractWith(IInteractable target, InteractionOption option)
	{
		if (option.kind != InteractionKind.Attack)
		{
			GiveOrder(new InteractOrder(target, option.id));
			return;
		}

		CombatActor actor = target is Component component ? component.GetComponentInParent<CombatActor>() : null;
		if (actor == null)
		{
			Debug.LogWarning($"[Combat] {characterName} can't attack {target.GetName()}: it has no CombatActor.");
			return;
		}

		AttackTarget(actor);
	}

	/// <summary>Fight this actor until it dies or another order is given. The one way to start a fight, for players and AI.</summary>
	public void AttackTarget(CombatActor target)
	{
		if (target == null || target == this)
			return;

		GiveOrder(new AttackOrder(target));
	}

	/// <summary>Ends the current order and starts this one. Null just stops.</summary>
	public void GiveOrder(CharacterOrder order)
	{
		EndCurrentOrder();
		if (order == null || !IsAlive || !isActiveAndEnabled)
			return;

		currentOrder = order;
		Coroutine routine = StartCoroutine(RunOrder(order));
		// An order that finished straight away has already cleared itself.
		if (currentOrder == order)
			orderRoutine = routine;
	}

	// Steps the order by hand, so stopping this one coroutine stops the order too.
	private IEnumerator RunOrder(CharacterOrder order)
	{
		IEnumerator run = order.Run(this);
		while (run.MoveNext())
			yield return run.Current;

		if (currentOrder == order)
		{
			currentOrder = null;
			orderRoutine = null;
			order.End(this);
		}
	}

	private void EndCurrentOrder()
	{
		CharacterOrder order = currentOrder;
		if (order == null)
			return;

		currentOrder = null;
		if (orderRoutine != null)
			StopCoroutine(orderRoutine);
		orderRoutine = null;
		order.End(this);
	}

	private void HandleDied(GameObject killer)
	{
		EndCurrentOrder();
		// Gear goes into the bag so the body can be looted.
		InventoryActions.UnequipAllInto(equipment, inventory);
	}

	// Floating label above the character that stays until ClearActionLabel.
	internal void ShowActionLabel(string text)
	{
		if (harvestingLabelPrefab == null)
			return;

		ClearActionLabel();
		activeActionLabel = SpawnLabel();
		var actionLabel = activeActionLabel.GetComponent<HarvestingLabel>();
		if (actionLabel != null && !string.IsNullOrEmpty(text))
			actionLabel.SetText(text);
	}

	internal void ClearActionLabel()
	{
		if (activeActionLabel != null)
			Destroy(activeActionLabel);
		activeActionLabel = null;
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
		GiveOrder(new SleepOrder(8f));
	}
	/// <summary>One swing or shot at the target. AttackOrder walks into range and calls this.</summary>
	internal void StrikeAt(CombatActor target, BowItem bow, AttackProfile profile)
	{
		if (!IsAlive || target == null || !target.IsAlive)
			return;

		// The target already has its maximum number of attackers: AttackOrder keeps us close and retries.
		// Archers shoot from a distance, so they don't take up a place around the target.
		if (bow == null && !TryTakeAttackSlot(target))
			return;

		// AttackOrder walks into range first, so this only trips if the target moved away.
		if (Vector3.Distance(transform.position, target.transform.position) > profile.Range)
			return;

		if (bow != null)
		{
			CharacterArchery.Shoot(this, target, bow, profile);
			return;
		}

		CombatExchangeResult result = ResolveStrike(target, profile, combatFormulaConfig);

		if (result.HitLanded)
		{
			string defense = result.Reaction == CombatReactionType.None ? "undefended" : $"{result.Reaction} failed";
			Debug.Log($"[Combat] {characterName} hit {target.name} for {result.DamageDealt:F1} ({defense}).");
			skills?.GainXP(profile.UsedWeaponSkill, combatFormulaConfig.meleeHitXP);
			skills?.GainXP(combatFormulaConfig.strengthSkill, combatFormulaConfig.meleeHitStrengthXP);
		}
		else
		{
			if (result.CounterDamage > 0f)
				Debug.Log($"[Combat] {characterName}'s attack was countered for {result.CounterDamage:F1} damage.");

			Debug.Log($"[Combat] {characterName}'s attack was prevented by {result.Reaction}.");
			skills?.GainXP(profile.UsedWeaponSkill, combatFormulaConfig.meleeMissXP);
		}
	}

	// Runs when the arrow lands, by which time the archer may be gone.
	internal void HandleArrowHit(ICombatActor defender, CombatExchangeResult result, SkillType skill)
	{
		if (this == null)
			return;

		GameObject defenderObject = defender.ActorTransform.gameObject;

		if (result.HitLanded)
		{
			Debug.Log($"[Combat] {characterName}'s arrow hit {defenderObject.name} for {result.DamageDealt:F1}.");
			skills?.GainXP(skill, combatFormulaConfig.arrowHitXP);
		}
		else
		{
			Debug.Log($"[Combat] {characterName}'s arrow was blocked by {defenderObject.name}.");
			skills?.GainXP(skill, combatFormulaConfig.arrowBlockedXP);
		}
	}

	private void OnDisable()
	{
		EndCurrentOrder();
	}

	private void OnDestroy()
	{
		if (stats != null)
		{
			stats.OnDied -= HandleDied;
			stats.OnAttacked -= HandleAttacked;
		}
	}

	/// <summary>
	/// Harvests one item into this character's inventory and grants Harvesting XP. Returns false
	/// if it did not fit.
	/// </summary>
	public bool collectItem(Item item)
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
	/// Uses one item from a stack in this character's inventory: eats it, drinks it, equips it,
	/// binds a wound with it... Each item type decides what using it means (Item.TryUse).
	/// </summary>
	public bool TryUseItem(Inventory.Entry entry, out string reason)
	{
		reason = null;
		if (inventory == null || !IsAlive || entry == null || !inventory.Contains(entry))
		{
			reason = "Can't use that";
			return false;
		}

		return entry.item.TryUse(this, entry, out reason);
	}

	/// <summary>Binds this character's own wounds with a healing item from its inventory (see TreatWoundsOrder).</summary>
	public bool TryTreatWounds(Inventory.Entry entry, out string reason)
	{
		return TryTreatWounds(this, entry, out reason);
	}

	/// <summary>Walks to a squadmate (or stays put for itself) and binds its wounds with the first dressing carried.</summary>
	public bool TryTreatWounds(Character patient, out string reason)
	{
		Inventory.Entry dressing = inventory != null ? inventory.FindFirst<HealingItem>() : null;
		if (dressing == null)
		{
			reason = "No dressings";
			return false;
		}

		return TryTreatWounds(patient, dressing, out reason);
	}

	/// <summary>
	/// Starts binding the patient's wounds with a healing item from this character's inventory. The
	/// healer's First Aid skill and HealingConfig decide the heal. Refuses when the dressing would mostly go to waste.
	/// </summary>
	public bool TryTreatWounds(Character patient, Inventory.Entry entry, out string reason)
	{
		reason = null;
		if (inventory == null || stats == null || !IsAlive)
		{
			reason = "Can't treat wounds";
			return false;
		}

		if (patient == null || patient.Stats == null || !patient.IsAlive || FactionRelations.AreHostile(Faction, patient.Faction))
		{
			reason = "Can't treat them";
			return false;
		}

		if (currentOrder is TreatWoundsOrder)
		{
			reason = "Already binding wounds";
			return false;
		}

		if (entry == null || !inventory.Contains(entry) || entry.item is not HealingItem item)
		{
			reason = "Can't use that";
			return false;
		}

		HealingConfig config = stats.HealingConfig;
		if (config == null)
		{
			Debug.LogWarning($"{characterName} has no HealingConfig assigned.");
			reason = "Can't treat wounds";
			return false;
		}

		CharacterStats patientStats = patient.Stats;
		if (!HealingRules.CanTreat(patientStats.currentHealth, patientStats.maxHealth))
		{
			reason = "Not wounded";
			return false;
		}

		float heal = HealingRules.TreatmentHeal(item.healAmount, FirstAidBonus, config.firstAidHealScale);
		if (!HealingRules.WorthTreating(patientStats.currentHealth, patientStats.maxHealth, heal, config.minMissingShareOfHeal))
		{
			reason = "Not wounded enough";
			return false;
		}

		GiveOrder(new TreatWoundsOrder(patient, entry, item));
		return true;
	}

	internal float FirstAidBonus => skills != null ? skills.GetBonus(SkillType.FirstAid) : 1f;

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
				TryUseItem(food, out _);
		}

		if (stats.thirst < autoDrinkBelow)
		{
			var drink = inventory.FindFirst<DrinkItem>();
			if (drink != null)
				TryUseItem(drink, out _);
		}
	}

	private GameObject SpawnLabel()
	{
		GameObject label = Instantiate(harvestingLabelPrefab, transform);
		label.transform.localPosition = LabelOffset;
		return label;
	}

	internal void ShowFloatingText(string text, float seconds)
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
