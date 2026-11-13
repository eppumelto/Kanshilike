using System;
using System.Collections.Generic;

/// <summary>Why a grudge exists. Serialized by number: only append. Only Killing grudges are inherited.</summary>
public enum GrudgeCause
{
	Killing = 0
}

/// <summary>One person's grudge against another. At most one per (holder, target).</summary>
[Serializable]
public class Grudge
{
	public string HolderId;
	public string TargetId;
	/// <summary>The latest victim this grudge is about (for barks).</summary>
	public string VictimId;
	public int DeathIndex;
	public GrudgeCause Cause;
	/// <summary>0..100.</summary>
	public float Intensity;
	public int CreatedDay;
	public int LastDecayDay;
	/// <summary>0 for an ordinary killing; each round of revenge adds one.</summary>
	public int Generation;
	/// <summary>Taken over from a dead parent.</summary>
	public bool Inherited;
	public bool HasLastKnown;
	public WorldPoint LastKnown;

	public override string ToString() => $"{HolderId}→{TargetId} {Intensity:0.#} (gen {Generation}{(Inherited ? ", inherited" : "")})";
}

public sealed class GrudgeBook
{
	private readonly Dictionary<(string, string), Grudge> grudges = new Dictionary<(string, string), Grudge>();
	private readonly List<Grudge> ordered = new List<Grudge>();

	public IReadOnlyList<Grudge> All => ordered;
	public int Count => ordered.Count;

	public Grudge Get(string holder, string target)
	{
		if (string.IsNullOrEmpty(holder) || string.IsNullOrEmpty(target))
			return null;
		return grudges.TryGetValue((holder, target), out Grudge g) ? g : null;
	}

	public List<Grudge> HeldBy(string holder) => Where(g => string.Equals(g.HolderId, holder, StringComparison.Ordinal));
	public List<Grudge> Against(string target) => Where(g => string.Equals(g.TargetId, target, StringComparison.Ordinal));

	/// <summary>
	/// Adds the grudge, or merges it into the existing one against the same target. Applies the
	/// per-holder cap: a new target replaces the weakest when stronger, else it is dropped (null).
	/// </summary>
	public Grudge AddOrMerge(Grudge grudge, SocialTuning t, out bool created)
	{
		created = false;
		if (grudge == null || string.IsNullOrEmpty(grudge.HolderId) || string.IsNullOrEmpty(grudge.TargetId))
			return null;

		Grudge existing = Get(grudge.HolderId, grudge.TargetId);
		if (existing != null)
		{
			existing.Intensity = GrudgeRules.Merge(existing.Intensity, grudge.Intensity, t);
			existing.VictimId = grudge.VictimId;
			existing.DeathIndex = grudge.DeathIndex;
			existing.Generation = Math.Max(existing.Generation, grudge.Generation);
			existing.Inherited = existing.Inherited && grudge.Inherited;
			existing.LastDecayDay = Math.Max(existing.LastDecayDay, grudge.LastDecayDay);
			if (grudge.HasLastKnown)
			{
				existing.HasLastKnown = true;
				existing.LastKnown = grudge.LastKnown;
			}
			return existing;
		}

		List<Grudge> held = HeldBy(grudge.HolderId);
		if (held.Count >= Math.Max(1, t.maxGrudgesPerHolder))
		{
			Grudge weakest = held[0];
			foreach (Grudge g in held)
				if (g.Intensity < weakest.Intensity)
					weakest = g;
			if (grudge.Intensity <= weakest.Intensity)
				return null;
			Remove(weakest);
		}

		grudges[(grudge.HolderId, grudge.TargetId)] = grudge;
		ordered.Add(grudge);
		created = true;
		return grudge;
	}

	public bool Remove(Grudge grudge)
	{
		if (grudge == null || !grudges.Remove((grudge.HolderId, grudge.TargetId)))
			return false;
		ordered.Remove(grudge);
		return true;
	}

	public int RemoveHeldBy(string holder) => RemoveAll(HeldBy(holder));
	public int RemoveAgainst(string target) => RemoveAll(Against(target));

	private int RemoveAll(List<Grudge> list)
	{
		foreach (Grudge g in list)
			Remove(g);
		return list.Count;
	}

	private List<Grudge> Where(Func<Grudge, bool> match)
	{
		var list = new List<Grudge>();
		foreach (Grudge g in ordered)
			if (match(g))
				list.Add(g);
		return list;
	}
}
