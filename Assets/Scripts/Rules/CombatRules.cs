using System;

/// <summary>
/// Plain combat formulas with no Unity dependency, so they can be tested on their own.
/// CombatFormulaResolver reads skills and items from the actors and passes the numbers in.
/// "Progress" is a skill's bonus minus one: 0 for untrained, 1 at the default maximum.
/// </summary>
public static class CombatRules
{
	/// <summary>A skill's bonus multiplier as progress. Never negative.</summary>
	public static float SkillProgress(float bonus)
	{
		return Math.Max(0f, bonus - 1f);
	}

	public static float AttackRating(float baseRating, float ratingMultiplier, float weaponProgress, float weaponScale, float strengthProgress, float strengthScale)
	{
		float rating = baseRating * ratingMultiplier * (1f + weaponProgress * weaponScale + strengthProgress * strengthScale);
		return Math.Max(0.1f, rating);
	}

	public static float Damage(float baseDamage, float strengthProgress, float strengthScale, float weaponProgress, float weaponScale)
	{
		return Math.Max(1f, baseDamage * (1f + strengthProgress * strengthScale + weaponProgress * weaponScale));
	}

	public static float Initiative(float initiativeProgress, float initiativeScale, float strengthProgress, float strengthScale)
	{
		return 1f + initiativeProgress * initiativeScale + strengthProgress * strengthScale;
	}

	/// <summary>Seconds between attacks, never below <paramref name="minInterval"/>.</summary>
	public static float AttackInterval(float baseInterval, float minInterval, float speed, float initiative)
	{
		return Math.Max(minInterval, baseInterval / Math.Max(0.1f, speed * initiative));
	}

	/// <summary>Block, dodge or counter rating. Zero when the defender can't use that reaction.</summary>
	public static float DefenseRating(bool canUse, float baseRating, float progress, float scale, float shieldMultiplier = 1f)
	{
		return canUse ? baseRating * (1f + progress * scale) * shieldMultiplier : 0f;
	}

	/// <summary>Chance (0–1) that a defense of the given rating stops an attack of the given rating.</summary>
	public static float DefenseChance(float defenseRating, float attackRating)
	{
		if (defenseRating <= 0f)
			return 0f;

		return defenseRating / (defenseRating + Math.Max(0.1f, attackRating));
	}

	/// <summary>How likely a reaction is to be picked: skilled defenders lean toward what they are good at.</summary>
	public static float ReactionWeight(bool canUse, float weight, float progress, float shieldMultiplier = 1f)
	{
		return canUse ? weight * (1f + progress) * shieldMultiplier : 0f;
	}

	/// <summary>Picks a reaction by weight. <paramref name="roll01"/> is a random value from 0 up to 1.</summary>
	public static CombatReactionType ChooseReaction(float blockWeight, float dodgeWeight, float counterWeight, float roll01)
	{
		float total = blockWeight + dodgeWeight + counterWeight;
		if (total <= 0f)
			return CombatReactionType.None;

		float roll = roll01 * total;
		if (roll < blockWeight)
			return CombatReactionType.Block;

		roll -= blockWeight;
		if (roll < dodgeWeight)
			return CombatReactionType.Dodge;

		return CombatReactionType.Counter;
	}

	/// <summary>A hit that gets through a failed block with a shield in the way.</summary>
	public static float FailedBlockDamage(float damage, float shieldReduction)
	{
		return damage * (1f - shieldReduction);
	}

	public static float CounterDamage(float defenderDamage, float counterFraction)
	{
		return Math.Max(1f, defenderDamage * counterFraction);
	}

	/// <summary>Widest a shot can stray from the aim point, in degrees. Skill narrows it, a moving target widens it.</summary>
	public static float BowSpread(float bowProgress, float aimProgress, float aimShare, float untrainedSpread, float masterSpread, float movingTargetSpread, float targetSpeed, float movingTargetSpeed)
	{
		float skill = Lerp(bowProgress, aimProgress, aimShare);
		return Lerp(untrainedSpread, masterSpread, skill) + movingTargetSpread * Clamp01(targetSpeed / movingTargetSpeed);
	}

	/// <summary>Rough chance (0–1) that a shot lands: how much of the spread the target fills.</summary>
	public static float BowHitChance(float spreadDegrees, float distance, float targetRadius)
	{
		if (spreadDegrees <= 0f)
			return 1f;

		float targetDegrees = (float)(Math.Atan2(targetRadius, Math.Max(0.01f, distance)) * (180.0 / Math.PI));
		float fill = targetDegrees / spreadDegrees;
		return Clamp01(fill * fill);
	}

	/// <summary>
	/// Whether someone who was just attacked should turn on the attacker. They defend themselves
	/// when idle or doing something that can wait, but never turn on a friend.
	/// </summary>
	public static bool ShouldDefendSelf(bool alive, bool orderYields, bool attackerHostile)
	{
		return alive && orderYields && attackerHostile;
	}

	private static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

	private static float Clamp01(float value) => Math.Max(0f, Math.Min(1f, value));
}
