using System;
using System.Collections.Generic;

/// <summary>Serialized by number: only append. Each tier includes the behaviour of the ones below.</summary>
public enum GrudgeTier
{
	None = 0,
	/// <summary>Refuses dealings and barks, but does not attack.</summary>
	Cold = 1,
	/// <summary>Hostile toward the target; attacks on sight.</summary>
	Hostile = 2,
	/// <summary>Calls out and rallies close friends.</summary>
	Rally = 3,
	/// <summary>Hunts the target when out of sight.</summary>
	Hunt = 4
}

public static class GrudgeRules
{
	/// <summary>clamp01(opinion / 100 + sum of bond weights).</summary>
	public static float Closeness(Relationship r, SocialTuning t) => Clamp01(r.Opinion / 100f + t.WeightOf(r.Bonds));

	/// <summary>(closeness × vengefulness × 100 + kin bonus) × falloff ^ generation.</summary>
	public static float Intensity(float closeness, float vengefulness, BondFlags kinToVictim, int generation, SocialTuning t)
	{
		float kinBonus = (kinToVictim & BondMasks.Kin) != 0 ? t.kinGrudgeBonus : 0f;
		float raw = closeness * vengefulness * 100f + kinBonus;
		return Math.Min(100f, raw * (float)Math.Pow(t.generationFalloff, Math.Max(0, generation)));
	}

	/// <summary>Float noise allowed when comparing an intensity with a threshold (80 × 0.75 must count as 60).</summary>
	public const float Epsilon = 0.001f;

	public static bool AtLeast(float intensity, float threshold) => intensity + Epsilon >= threshold;

	public static GrudgeTier Tier(float intensity, SocialTuning t)
	{
		if (AtLeast(intensity, t.huntAt)) return GrudgeTier.Hunt;
		if (AtLeast(intensity, t.rallyAt)) return GrudgeTier.Rally;
		if (AtLeast(intensity, t.hostileAt)) return GrudgeTier.Hostile;
		if (AtLeast(intensity, t.minGrudgeIntensity)) return GrudgeTier.Cold;
		return GrudgeTier.None;
	}

	public static float Merge(float a, float b, SocialTuning t) => Math.Min(100f, Math.Max(a, b) + t.stackBonus);

	public static float Decayed(float intensity, int days, SocialTuning t) =>
		Math.Max(0f, intensity - t.decayPerDay * Math.Max(0, days));

	public static bool Forgotten(float intensity, SocialTuning t) => intensity < t.forgetBelow;

	/// <summary>Whole days until the grudge drops below forgetBelow. 0 when already below.</summary>
	public static int DaysUntilForgotten(float intensity, SocialTuning t)
	{
		if (Forgotten(intensity, t))
			return 0;
		if (t.decayPerDay <= 0f)
			return int.MaxValue;
		return (int)Math.Floor((intensity - t.forgetBelow) / t.decayPerDay) + 1;
	}

	public static int GrudgeCapFor(bool victimWasElder, SocialTuning t) =>
		victimWasElder ? t.elderDeathGrudgeCap : t.maxGrudgesPerDeath;

	/// <summary>Only killing grudges at or above inheritAt pass to the holder's children.</summary>
	public static bool Inheritable(Grudge g, SocialTuning t) =>
		g != null && g.Cause == GrudgeCause.Killing && AtLeast(g.Intensity, t.inheritAt);

	/// <summary>The intensity a child takes over.</summary>
	public static float Inherit(float intensity, SocialTuning t) => intensity * t.inheritFactor;

	private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
}

public static class KinTerms
{
	/// <summary>son, mother, sister, husband... for how the holder relates to other; null for non-kin.</summary>
	public static string Term(BondFlags kin, Sex other)
	{
		bool male = other == Sex.Male;
		if ((kin & BondFlags.Child) != 0) return male ? "son" : "daughter";
		if ((kin & BondFlags.Parent) != 0) return male ? "father" : "mother";
		if ((kin & BondFlags.Sibling) != 0) return male ? "brother" : "sister";
		if ((kin & BondFlags.Spouse) != 0) return male ? "husband" : "wife";
		return null;
	}
}

public static class AttitudeRules
{
	/// <summary>Personal feelings over faction relations, in the order from the feuds plan.</summary>
	public static AttitudeResult Resolve(FactionRelation faction, bool closeKin, int opinion, float grudgeIntensity, SocialTuning t)
	{
		FactionRelation relation;
		AttitudeReason reason;

		if (faction == FactionRelation.Hostile && !closeKin)
		{
			relation = FactionRelation.Hostile;
			reason = AttitudeReason.Faction;
		}
		else if (GrudgeRules.AtLeast(grudgeIntensity, t.hostileAt))
		{
			relation = FactionRelation.Hostile;
			reason = AttitudeReason.Grudge;
		}
		else if (opinion <= t.hostileOpinion)
		{
			relation = FactionRelation.Hostile;
			reason = AttitudeReason.Opinion;
		}
		else if (opinion >= t.alliedOpinion)
		{
			relation = FactionRelation.Allied;
			reason = AttitudeReason.Opinion;
		}
		else if (faction == FactionRelation.Hostile)
		{
			relation = FactionRelation.Neutral;
			reason = AttitudeReason.Kin;
		}
		else
		{
			relation = faction;
			reason = AttitudeReason.Faction;
		}

		bool refuses = relation == FactionRelation.Hostile || GrudgeRules.AtLeast(grudgeIntensity, t.minGrudgeIntensity);
		return new AttitudeResult(relation, reason, refuses);
	}
}

public static class DefaultBondRules
{
	/// <summary>
	/// The bond two people get from what they share: household, kin group, community and elder.
	/// Union of flags, highest opinion. False when nothing is shared or from == to.
	/// fromCommunity is from's registered community, or null.
	/// </summary>
	public static bool Between(PersonRecord from, PersonRecord to, CommunityRecord fromCommunity, SocialTuning t, out Relationship derived)
	{
		derived = default;
		if (from == null || to == null || string.Equals(from.Id, to.Id, StringComparison.Ordinal))
			return false;

		BondFlags flags = BondFlags.None;
		int opinion = int.MinValue;

		void Layer(BondFlags f, int o)
		{
			flags |= f;
			opinion = Math.Max(opinion, o);
		}

		if (PersonRegistry.SameId(from.HouseholdId, to.HouseholdId))
			Layer(BondFlags.Family, t.householdBondOpinion);
		if (PersonRegistry.SameId(from.KinGroupId, to.KinGroupId))
			Layer(BondFlags.Family, t.kinGroupBondOpinion);

		bool communityKnown = fromCommunity != null && PersonRegistry.SameId(fromCommunity.Id, from.CommunityId);
		if (communityKnown && PersonRegistry.SameId(from.CommunityId, to.CommunityId))
		{
			if (fromCommunity.Kind == CommunityKind.Camp)
				Layer(BondFlags.Comrade, t.campBondOpinion);
			else
				Layer(BondFlags.Neighbour, t.neighbourBondOpinion);
		}
		if (communityKnown && PersonRegistry.SameId(fromCommunity.ElderId, to.Id))
			Layer(BondFlags.Elder, t.elderBondOpinion);

		if (flags == BondFlags.None)
			return false;
		derived = new Relationship(opinion, flags);
		return true;
	}
}

public static class RelationshipRules
{
	public const int MinOpinion = -100;
	public const int MaxOpinion = 100;

	/// <summary>Derived flags | stored flags; opinion = (stored base, else derived, else 0) + delta, clamped.</summary>
	public static Relationship Effective(bool hasDerived, Relationship derived, StoredRelationship stored)
	{
		BondFlags flags = (hasDerived ? derived.Bonds : BondFlags.None) | stored.Bonds;
		return new Relationship(Clamp(BaseOf(hasDerived, derived, stored) + stored.Delta), flags);
	}

	public static int BaseOf(bool hasDerived, Relationship derived, StoredRelationship stored) =>
		stored.HasBase ? stored.Base : hasDerived ? derived.Opinion : 0;

	/// <summary>The new delta after a change, keeping the effective opinion in -100..100 (no pile-up past the clamp).</summary>
	public static int DeltaAfterChange(int baseOpinion, int delta, int change)
	{
		int effective = Clamp(baseOpinion + delta);
		return Clamp(effective + change) - baseOpinion;
	}

	/// <summary>The delta that raises the effective opinion to at least floor; never lowers it.</summary>
	public static int DeltaAfterRaise(int baseOpinion, int delta, int floor)
	{
		int effective = Clamp(baseOpinion + delta);
		return effective >= floor ? delta : Clamp(floor) - baseOpinion;
	}

	public static int Clamp(int opinion) => opinion < MinOpinion ? MinOpinion : opinion > MaxOpinion ? MaxOpinion : opinion;
}

public static class NewsRules
{
	/// <summary>
	/// How long news of the victim's death takes to reach the recipient: household or community
	/// newsDelayHours, else kin group kinNewsDelayHours. False when they share none, or are the victim.
	/// </summary>
	public static bool Delay(PersonRecord victim, PersonRecord recipient, SocialTuning t, out double hours)
	{
		hours = 0;
		if (victim == null || recipient == null || string.Equals(victim.Id, recipient.Id, StringComparison.Ordinal))
			return false;
		if (PersonRegistry.SameId(victim.HouseholdId, recipient.HouseholdId) || PersonRegistry.SameId(victim.CommunityId, recipient.CommunityId))
		{
			hours = t.newsDelayHours;
			return true;
		}
		if (PersonRegistry.SameId(victim.KinGroupId, recipient.KinGroupId))
		{
			hours = t.kinNewsDelayHours;
			return true;
		}
		return false;
	}
}

public struct GrudgePreviewRow
{
	public string Bond;
	public float Closeness;
	public float Intensity;
	public GrudgeTier Tier;
	/// <summary>Below grudgeClosenessThreshold: no grudge at all.</summary>
	public bool BelowCloseness;
	/// <summary>Sits exactly on a tier or closeness threshold (a tuning smell).</summary>
	public bool OnThreshold;
}

/// <summary>What each typical bond gives when the victim is killed, for the SocialConfig inspector.</summary>
public static class GrudgePreview
{
	public static IReadOnlyList<GrudgePreviewRow> Rows(SocialTuning t, float vengefulness)
	{
		int household = t.householdBondOpinion;
		var rows = new List<GrudgePreviewRow>
		{
			Row("Village neighbour", new Relationship(t.neighbourBondOpinion, BondFlags.Neighbour), t, vengefulness),
			Row("Camp comrade", new Relationship(t.campBondOpinion, BondFlags.Comrade), t, vengefulness),
			Row("Kin-group relative", new Relationship(t.kinGroupBondOpinion, BondFlags.Family), t, vengefulness),
			Row("Elder (villager)", new Relationship(Math.Max(t.neighbourBondOpinion, t.elderBondOpinion), BondFlags.Neighbour | BondFlags.Elder), t, vengefulness),
			Row("Household member", new Relationship(household, BondFlags.Family), t, vengefulness),
			Row("Parent", new Relationship(household, BondFlags.Family | BondFlags.Parent), t, vengefulness),
			Row("Child", new Relationship(household, BondFlags.Family | BondFlags.Child), t, vengefulness),
			Row("Sibling", new Relationship(household, BondFlags.Family | BondFlags.Sibling), t, vengefulness),
			Row("Spouse", new Relationship(household, BondFlags.Family | BondFlags.Spouse), t, vengefulness),
		};
		return rows;
	}

	private static GrudgePreviewRow Row(string bond, Relationship r, SocialTuning t, float vengefulness)
	{
		float closeness = GrudgeRules.Closeness(r, t);
		float intensity = GrudgeRules.Intensity(closeness, vengefulness, r.Bonds & BondMasks.Kin, 0, t);
		bool below = closeness < t.grudgeClosenessThreshold - 0.0001f;
		return new GrudgePreviewRow
		{
			Bond = bond,
			Closeness = closeness,
			Intensity = intensity,
			Tier = below ? GrudgeTier.None : GrudgeRules.Tier(intensity, t),
			BelowCloseness = below,
			OnThreshold = Near(intensity, t.minGrudgeIntensity) || Near(intensity, t.hostileAt) || Near(intensity, t.rallyAt)
				|| Near(intensity, t.huntAt) || Math.Abs(closeness - t.grudgeClosenessThreshold) < 0.001f
		};
	}

	private static bool Near(float a, float b) => Math.Abs(a - b) < 0.001f;
}
