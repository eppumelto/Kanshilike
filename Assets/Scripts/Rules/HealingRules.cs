using System;

/// <summary>Plain healing formulas with no Unity dependency, so they can be tested on their own.</summary>
public static class HealingRules
{
	/// <summary>
	/// HP regained for one in-game hour. Zero while starving or dehydrated. The sleep rate replaces the
	/// natural one for the share of the hour spent asleep (<paramref name="asleepFraction"/>, 0-1).
	/// </summary>
	public static float RegenPerHour(float asleepFraction, bool starving, bool dehydrated, float naturalPerHour, float sleepPerHour)
	{
		if (starving || dehydrated)
			return 0f;
		float asleep = Math.Max(0f, Math.Min(1f, asleepFraction));
		return Math.Max(0f, naturalPerHour) * (1f - asleep) + Math.Max(0f, sleepPerHour) * asleep;
	}

	/// <summary>How much health a heal of <paramref name="amount"/> really restores. Never negative, and zero for the dead.</summary>
	public static float HealthGained(float current, float max, float amount)
	{
		if (current <= 0f || amount <= 0f)
			return 0f;
		return Math.Max(0f, Math.Min(max, current + amount) - current);
	}

	/// <summary>Alive and below max health.</summary>
	public static bool CanTreat(float current, float max)
	{
		return current > 0f && current < max;
	}

	/// <summary>Dressing heal after the First Aid bonus. (bonus - 1) is the skill progress, as in CombatFormulaConfig.</summary>
	public static float TreatmentHeal(float baseHeal, float skillBonus, float scale)
	{
		return baseHeal * (1f + (skillBonus - 1f) * scale);
	}

	/// <summary>Dressing action time after the First Aid bonus, never below <paramref name="minSeconds"/>.</summary>
	public static float TreatmentSeconds(float baseSeconds, float skillBonus, float scale, float minSeconds)
	{
		float divisor = Math.Max(0.01f, 1f + (skillBonus - 1f) * scale);
		return Math.Max(minSeconds, baseSeconds / divisor);
	}

	public static float TreatmentXP(float healthGained, float xpPerHealthPoint)
	{
		return Math.Max(0f, healthGained) * xpPerHealthPoint;
	}
}
