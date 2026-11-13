using System;
using System.Collections.Generic;

/// <summary>How two factions treat each other. Serialized by number: never renumber, only append.</summary>
public enum FactionRelation
{
	/// <summary>Starts fights and fights back.</summary>
	Hostile = 0,
	/// <summary>Never starts a fight, but fights back when attacked.</summary>
	Neutral = 1,
	/// <summary>Never fights back (friendly fire) and can have wounds bound.</summary>
	Allied = 2
}

/// <summary>Why FactionRelationTable.Get gave its answer. Used by the debug view.</summary>
public enum FactionRelationSource
{
	/// <summary>One side has no faction.</summary>
	Unaffiliated = 0,
	/// <summary>The pair has its own entry.</summary>
	Explicit = 1,
	/// <summary>Both sides are the same faction.</summary>
	Same = 2,
	/// <summary>The more hostile of the two factions' default stances.</summary>
	DefaultStance = 3
}

/// <summary>
/// Relations between factions, keyed by id. Each faction has a default stance toward factions
/// it has no entry with; pairs can override that. An instance, so tests use their own copy.
/// </summary>
public sealed class FactionRelationTable
{
	private readonly Dictionary<FactionId, FactionRelation> stances = new Dictionary<FactionId, FactionRelation>();
	private readonly Dictionary<(FactionId, FactionId), FactionRelation> pairs = new Dictionary<(FactionId, FactionId), FactionRelation>();

	/// <summary>Sets the faction's stance toward every faction it has no entry with. Overwrites.</summary>
	public void Define(FactionId id, FactionRelation defaultStance)
	{
		if (!id.IsNone)
			stances[id] = defaultStance;
	}

	public bool IsDefined(FactionId id) => !id.IsNone && stances.ContainsKey(id);

	/// <summary>The faction's default stance; Neutral when it was never defined.</summary>
	public FactionRelation DefaultStanceOf(FactionId id) =>
		!id.IsNone && stances.TryGetValue(id, out FactionRelation stance) ? stance : FactionRelation.Neutral;

	/// <summary>An explicit relation for the pair, the same both ways.</summary>
	public void Set(FactionId a, FactionId b, FactionRelation relation)
	{
		if (a.IsNone || b.IsNone)
			return;
		pairs[Key(a, b)] = relation;
	}

	public void Clear(FactionId a, FactionId b)
	{
		if (a.IsNone || b.IsNone)
			return;
		pairs.Remove(Key(a, b));
	}

	public FactionRelation Get(FactionId a, FactionId b) => Explain(a, b, out _);

	public FactionRelation Explain(FactionId a, FactionId b, out FactionRelationSource source)
	{
		if (a.IsNone || b.IsNone)
		{
			source = FactionRelationSource.Unaffiliated;
			return FactionRelation.Neutral;
		}

		if (pairs.TryGetValue(Key(a, b), out FactionRelation explicitRelation))
		{
			source = FactionRelationSource.Explicit;
			return explicitRelation;
		}

		if (a == b)
		{
			source = FactionRelationSource.Same;
			return FactionRelation.Allied;
		}

		// The more hostile of the two stances: Hostile (0) < Neutral (1) < Allied (2).
		source = FactionRelationSource.DefaultStance;
		FactionRelation sa = DefaultStanceOf(a);
		FactionRelation sb = DefaultStanceOf(b);
		return sa < sb ? sa : sb;
	}

	// Ordered by ordinal compare so (a, b) and (b, a) share one entry.
	private static (FactionId, FactionId) Key(FactionId a, FactionId b) =>
		string.CompareOrdinal(a.Value, b.Value) <= 0 ? (a, b) : (b, a);
}
