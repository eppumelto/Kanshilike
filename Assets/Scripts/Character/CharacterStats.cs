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
			OnDied?.Invoke(source);

			// After the OnDied listeners, so they can still use the NavMeshAgent etc.
			if (Application.isPlaying && GetComponent<Corpse>() == null)
				gameObject.AddComponent<Corpse>();
		}
	}

	public void Heal(float amount)
	{
		if (amount <= 0f || !IsAlive)
			return;

		currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
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
	}
}
