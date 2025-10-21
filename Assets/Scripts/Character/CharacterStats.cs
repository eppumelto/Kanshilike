using System.Collections;
using UnityEngine;

public class CharacterStats : MonoBehaviour
{
	[Header("Base Stats")]
	public float hunger = 100f;
	public float thirst = 100f;
	public float tiredness = 0f;

	[Header("Rates (per hour)")]
	public float hungerLossRate = 2f;
	public float thirstLossRate = 3f;
	public float tirednessGainRate = 1f;

	private TimeManager timeManager;
	public float lastHour;

	void Start()
	{
		timeManager = FindObjectOfType<TimeManager>();
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

			Debug.Log($"{name} slept {inGameHoursSlept}h so far — tiredness now {tiredness}");
		}

		Debug.Log($"{name} woke up after {sleepHours}h sleep — final tiredness {tiredness}");
	}
}
