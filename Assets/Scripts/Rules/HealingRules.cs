using System;

/// <summary>Plain healing formulas with no Unity dependency, so they can be tested on their own.</summary>
public static class HealingRules
{
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

	/// <summary>
	/// Whether a dressing that would heal <paramref name="heal"/> is worth using: the patient is alive
	/// and missing at least <paramref name="minMissingShare"/> of the heal (or of max health, if that is smaller).
	/// </summary>
	public static bool WorthTreating(float current, float max, float heal, float minMissingShare)
	{
		if (!CanTreat(current, max))
			return false;
		float needed = Math.Min(Math.Max(0f, heal), max) * Math.Max(0f, Math.Min(1f, minMissingShare));
		return max - current >= needed;
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
