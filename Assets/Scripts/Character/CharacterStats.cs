using System.Collections;
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

	[Header("Starvation (HP lost per in-game hour while the bar is empty)")]
	[Min(0f)] public float starvationDamagePerHour = 3f;
	[Min(0f)] public float dehydrationDamagePerHour = 5f;

	[Header("Healing")]
	[Tooltip("Regeneration rates. Leave empty for no regeneration (e.g. animals).")]
	[SerializeField] private HealingConfig healingConfig;
	public HealingConfig HealingConfig => healingConfig;

	/// <summary>True from the start of a sleep until it ends, is interrupted, or the character dies.</summary>
	public bool IsSleeping { get; private set; }

	// TODO: Replace with a real skill-based calculation once the skill system is implemented.
	// Higher values = faster harvesting. E.g. a Foraging skill of 50 could give 1.5x speed.
	public float HarvestSpeedMultiplier => 1.0f;

	private TimeManager timeManager;
	public float lastHour;

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

	void HandleTimeChanged(float currentTime)
	{
		int hour = Mathf.FloorToInt(currentTime);
		if (hour != Mathf.FloorToInt(lastHour))
		{
			UpdateStats(hour);
			lastHour = currentTime;
		}
	}

	void UpdateStats(float currentHour)
	{
		if (!IsAlive || !hasNeeds)
			return;

		float hungerRate = hungerLossRate;
		float thirstRate = thirstLossRate;
		float tiredRate = tirednessGainRate;

		// Increase tiredness faster at night
		if (currentHour >= 22 || currentHour < 6)
			tiredRate *= 2f;

		hunger = Mathf.Max(0f, hunger - hungerRate);
		thirst = Mathf.Max(0f, thirst - thirstRate);
		tiredness = Mathf.Min(100f, tiredness + tiredRate);

		Debug.Log($"{name} -> H:{hunger} T:{thirst} Td:{tiredness}");

		ApplyStarvation();
		ApplyRegeneration();
	}

	// Natural and sleep healing, once per in-game hour. Does nothing without a HealingConfig.
	void ApplyRegeneration()
	{
		if (healingConfig == null || !IsAlive)
			return;

		float rate = HealingRules.RegenPerHour(IsSleeping, IsStarving, IsDehydrated,
			healingConfig.naturalRegenPerHour, healingConfig.sleepRegenPerHour);
		float gained = Heal(rate);
		if (gained > 0f)
			Debug.Log($"{name} healed {gained:F0} HP ({currentHealth:F0}/{maxHealth:F0})");
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
	public void Sleep(float sleepHours)
	{
		StopAllCoroutines();
		StartCoroutine(SleepRoutine(sleepHours));
		IsSleeping = true;
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
			StopAllCoroutines(); // e.g. sleeping
			IsSleeping = false;
			OnDied?.Invoke(source);

			// After the OnDied listeners, so they can still use the NavMeshAgent etc.
			if (Application.isPlaying && GetComponent<Corpse>() == null)
				gameObject.AddComponent<Corpse>();
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

	/// <summary>Ends a sleep early (or at its end). The hours already slept are kept.</summary>
	public void WakeUp()
	{
		if (!IsSleeping)
			return;

		StopAllCoroutines();
		IsSleeping = false;
	}

	private IEnumerator SleepRoutine(float sleepHours)
	{
		float inGameHoursSlept = 0f;
		float restPerHour = 15f;

		float startTiredness = tiredness;
		float targetTiredness = Mathf.Max(0f, tiredness - sleepHours * restPerHour);

		Debug.Log($"{name} started sleeping for {sleepHours} hours");

		while (inGameHoursSlept < sleepHours)
		{
			// Wait until time advances one in-game hour
			float hourStart = Mathf.Floor(timeManager.currentTime);
			yield return new WaitUntil(() => Mathf.Floor(timeManager.currentTime) != hourStart);

			inGameHoursSlept += 1f;

			// Reduce tiredness gradually
			tiredness = Mathf.MoveTowards(tiredness, targetTiredness, restPerHour);

			// Slight hunger/thirst loss while sleeping
			hunger = Mathf.Max(0f, hunger - hungerLossRate * 0.5f);
			thirst = Mathf.Max(0f, thirst - thirstLossRate * 0.5f);

			Debug.Log($"{name} slept {inGameHoursSlept}h so far � tiredness now {tiredness}");
		}

		Debug.Log($"{name} woke up after {sleepHours}h sleep � final tiredness {tiredness}");
		WakeUp();
	}
}
