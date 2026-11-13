using System.Collections.Generic;
using UnityEngine;

public class CharacterStats : MonoBehaviour
{
	[Header("Base Stats")]
	[Min(1f)] public float maxHealth = 100f;
	public float currentHealth = 100f;
	public float hunger = 100f;
	public float thirst = 100f;
	public float tiredness = 0f;

	[Header("Rates (per hour)")]
	[Tooltip("Off for creatures that have no way to eat or drink yet (e.g. wolves): their hunger, thirst and tiredness stay put.")]
	public bool hasNeeds = true;
	public float hungerLossRate = 2f;
	public float thirstLossRate = 3f;
	public float tirednessGainRate = 1f;

	[Header("Sleep (per hour asleep)")]
	[Tooltip("Tiredness removed per in-game hour of sleep.")]
	[Min(0f)] public float restPerHour = 15f;
	[Tooltip("Hunger and thirst fall at this fraction of their normal rate while asleep.")]
	[Range(0f, 1f)] public float sleepNeedsMultiplier = 0.5f;

	[Header("Starvation (HP lost per in-game hour while the bar is empty)")]
	[Min(0f)] public float starvationDamagePerHour = 3f;
	[Min(0f)] public float dehydrationDamagePerHour = 5f;

	[Header("Healing")]
	[Tooltip("First Aid settings used when this character treats wounds. Leave empty for creatures that can't (e.g. animals). There is no natural regeneration.")]
	[SerializeField] private HealingConfig healingConfig;
	public HealingConfig HealingConfig => healingConfig;

	/// <summary>True from the start of a sleep until it ends, is interrupted, or the character dies.</summary>
	public bool IsSleeping => clock.IsSleeping;

	// TODO: Replace with a real skill-based calculation once the skill system is implemented.
	// Higher values = faster harvesting. E.g. a Foraging skill of 50 could give 1.5x speed.
	public float HarvestSpeedMultiplier => 1.0f;

	private TimeManager timeManager;
	private readonly HourClock clock = new HourClock();
	private readonly List<HourTick> ticks = new List<HourTick>();

	public bool IsAlive => currentHealth > 0f;
	public bool IsStarving => hasNeeds && hunger <= 0f;
	public bool IsDehydrated => hasNeeds && thirst <= 0f;
	public event System.Action<float, GameObject> OnDamaged;
	public event System.Action<GameObject> OnDied;
	/// <summary>Someone swung at this entity, hit or not. Args: (attacker, blocked with a shield).</summary>
	public event System.Action<GameObject, bool> OnAttacked;

	public void NotifyAttacked(GameObject attacker, bool blockedWithShield)
	{
		WakeUp();
		OnAttacked?.Invoke(attacker, blockedWithShield);
	}

	/// <summary>How much wounds have slowed this entity: 1 is unhurt. Animals move at this fraction of their speed.</summary>
	public float WoundSpeedMultiplier { get; private set; } = 1f;

	/// <summary>Multiplies the wound slowdown, which lasts until death and never goes below the minimum.</summary>
	public void Wound(float speedMultiplier, float minSpeedMultiplier)
	{
		WoundSpeedMultiplier = Mathf.Min(WoundSpeedMultiplier, Mathf.Max(minSpeedMultiplier, WoundSpeedMultiplier * speedMultiplier));
	}

	void Start()
	{
		currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

		timeManager = FindFirstObjectByType<TimeManager>();
		if (timeManager != null)
			timeManager.OnTimeChanged += HandleTimeChanged;
	}

	void OnDestroy()
	{
		if (timeManager != null)
			timeManager.OnTimeChanged -= HandleTimeChanged;
	}

	// The first call only sets the starting hour. After that, every whole hour passed runs one
	// update, even when several pass in a single frame.
	void HandleTimeChanged(float currentTime)
	{
		ticks.Clear();
		bool sleepEnded = clock.Advance(currentTime, ticks);

		foreach (HourTick tick in ticks)
			UpdateStats(tick);

		if (sleepEnded)
			Debug.Log($"{name} woke up - tiredness {tiredness:F0}");
	}

	void UpdateStats(HourTick tick)
	{
		if (!IsAlive || !hasNeeds)
			return;

		UpdateNeeds(tick);
		ApplyStarvation();
	}

	// Hunger, thirst and tiredness for one hour. The part of the hour spent asleep drains needs
	// more slowly and rests instead of tiring.
	void UpdateNeeds(HourTick tick)
	{
		float asleep = tick.AsleepFraction;
		float awake = 1f - asleep;
		float needsScale = awake + asleep * sleepNeedsMultiplier;

		float tiredRate = tirednessGainRate;
		if (tick.Hour >= 22 || tick.Hour < 6)
			tiredRate *= 2f; // tire faster at night

		hunger = Mathf.Max(0f, hunger - hungerLossRate * needsScale);
		thirst = Mathf.Max(0f, thirst - thirstLossRate * needsScale);
		tiredness = Mathf.Clamp(tiredness + tiredRate * awake - restPerHour * asleep, 0f, 100f);

		Debug.Log($"{name} -> H:{hunger} T:{thirst} Td:{tiredness}");
	}

	// Empty hunger/thirst bars hurt every hour until the character eats/drinks or dies.
	void ApplyStarvation()
	{
		float damage = 0f;
		if (IsStarving)
			damage += starvationDamagePerHour;
		if (IsDehydrated)
			damage += dehydrationDamagePerHour;

		if (damage <= 0f)
			return;

		string cause = IsStarving && IsDehydrated ? "starving and dehydrated" : IsStarving ? "starving" : "dehydrated";
		Debug.LogWarning($"{name} is {cause}: -{damage} HP ({Mathf.Max(0f, currentHealth - damage):F0} left).");
		ReceiveDamage(damage, null);
	}

	public void Eat(float nutrition)
	{
		hunger = Mathf.Min(100f, hunger + nutrition);
	}

	public void Drink(float amount)
	{
		thirst = Mathf.Min(100f, thirst + amount);
	}

	/// <summary>Sleeps for this many in-game hours. Needs a TimeManager to wake up again.</summary>
	public void Sleep(float sleepHours)
	{
		if (!IsAlive)
			return;
		if (timeManager == null)
		{
			Debug.LogWarning($"{name} can't sleep: no TimeManager in the scene.");
			return;
		}

		clock.StartSleep(sleepHours);
		Debug.Log($"{name} started sleeping for {sleepHours} hours");
	}

	public void ReceiveDamage(float amount, GameObject source = null)
	{
		if (!IsAlive || amount <= 0f)
			return;

		// Contract: source should be the attacker actor root GameObject when known.
		// Consumers (retaliation, aggro) may resolve attacker data from this reference.
		currentHealth = Mathf.Max(0f, currentHealth - amount);
		OnDamaged?.Invoke(amount, source);

		if (!IsAlive)
		{
			Debug.Log($"{name} died.");
			clock.StopSleep();
			OnDied?.Invoke(source);
		}
	}

	/// <summary>Restores health and returns how much was actually gained.</summary>
	public float Heal(float amount)
	{
		float gained = HealingRules.HealthGained(currentHealth, maxHealth, amount);
		if (gained <= 0f)
			return 0f;

		currentHealth = Mathf.Min(maxHealth, currentHealth + gained);
		return gained;
	}

	/// <summary>Ends a sleep early. The time already slept still counts at the next hour.</summary>
	public void WakeUp()
	{
		clock.StopSleep();
	}
}
