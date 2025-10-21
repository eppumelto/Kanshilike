using UnityEngine;
using System.Collections.Generic;

public class StatManager : MonoBehaviour
{
	[Header("References")]
	public TimeManager timeManager;
	public List<CharacterStats> characters = new List<CharacterStats>();

	[Header("Stat Rates (per hour)")]
	public float baseHungerRate = 2f;
	public float baseThirstRate = 3f;
	public float baseTirednessRate = 1f;

	private float lastHour;

	void Start()
	{
		timeManager.OnTimeChanged += HandleTimeChanged;
	}

	void HandleTimeChanged(float currentTime)
	{
		// Run only when hour changes to optimize
		int hour = Mathf.FloorToInt(currentTime);
		if (hour != Mathf.FloorToInt(lastHour))
		{
			UpdateCharacterStats(hour);
			lastHour = currentTime;
		}
	}

	void UpdateCharacterStats(float hour)
	{
		foreach (var character in characters)
		{
			float hungerRate = baseHungerRate;
			float thirstRate = baseThirstRate;
			float tiredRate = baseTirednessRate;

			// Example: at night (22–6), characters get tired faster
			if (hour >= 22 || hour < 6) tiredRate *= 2f;

			//character.UpdateStats(hungerRate, thirstRate, tiredRate);
		}
	}
}
