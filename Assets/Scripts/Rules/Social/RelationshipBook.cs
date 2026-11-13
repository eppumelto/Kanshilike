using System;
using System.Collections.Generic;

/// <summary>
/// Bonds between two people, read from the holder's side. Serialized by number: never renumber.
/// Child on (Aino, Veikko) means "Veikko is Aino's child". Elder means "other is my elder".
/// </summary>
[Flags]
public enum BondFlags
{
	None = 0,
	Family = 1,
	Friend = 2,
	Lover = 4,
	Comrade = 8,
	Rival = 16,
	Parent = 32,
	Child = 64,
	Sibling = 128,
	Spouse = 256,
	Neighbour = 512,
	Elder = 1024
}

public static class BondMasks
{
	/// <summary>Exact kin: always stored, always mirrored, never removed.</summary>
	public const BondFlags Kin = BondFlags.Parent | BondFlags.Child | BondFlags.Sibling | BondFlags.Spouse;
}

/// <summary>The effective relationship: what everyone reads.</summary>
[Serializable]
public struct Relationship
{
	public int Opinion;
	public BondFlags Bonds;

	public Relationship(int opinion, BondFlags bonds)
	{
		Opinion = opinion;
		Bonds = bonds;
	}

	public override string ToString() => $"{Bonds} {Opinion:+0;-0;0}";
}

/// <summary>Only what cannot be derived from memberships: authored bonds, kin, an authored base, earned opinion.</summary>
[Serializable]
public struct StoredRelationship
{
	public BondFlags Bonds;
	public bool HasBase;
	public int Base;
	/// <summary>Opinion earned in play, added to the base.</summary>
	public int Delta;

	public bool IsDefault => Bonds == BondFlags.None && !HasBase && Delta == 0;
}

public readonly struct RelationshipEntry
{
	public readonly string From;
	public readonly string To;
	public readonly StoredRelationship Stored;

	public RelationshipEntry(string from, string to, StoredRelationship stored)
	{
		From = from;
		To = to;
		Stored = stored;
	}
}

/// <summary>
/// Sparse, directional store of relationships that cannot be derived. Grows with authored bonds
/// and pairs that actually interacted, not with population squared.
/// </summary>
public sealed class RelationshipBook
{
	private readonly Dictionary<(string, string), StoredRelationship> entries = new Dictionary<(string, string), StoredRelationship>();

	public int Count => entries.Count;

	public IEnumerable<RelationshipEntry> Entries
	{
		get
		{
			foreach (KeyValuePair<(string, string), StoredRelationship> e in entries)
				yield return new RelationshipEntry(e.Key.Item1, e.Key.Item2, e.Value);
		}
	}

	public StoredRelationship GetStored(string from, string to)
	{
		if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
			return default;
		return entries.TryGetValue((from, to), out StoredRelationship s) ? s : default;
	}

	public void SetDelta(string from, string to, int delta) => Change(from, to, s => { s.Delta = delta; return s; });
	public void SetBase(string from, string to, int opinion) => Change(from, to, s => { s.HasBase = true; s.Base = opinion; return s; });
	public void ClearBase(string from, string to) => Change(from, to, s => { s.HasBase = false; s.Base = 0; return s; });

	/// <summary>Adds non-kin flags. Kin flags go through SetKin.</summary>
	public void AddBonds(string from, string to, BondFlags flags)
	{
		RefuseKin(flags);
		Change(from, to, s => { s.Bonds |= flags; return s; });
	}

	/// <summary>Replaces the non-kin flags; kin flags stay.</summary>
	public void SetBonds(string from, string to, BondFlags flags)
	{
		RefuseKin(flags);
		Change(from, to, s => { s.Bonds = (s.Bonds & BondMasks.Kin) | flags; return s; });
	}

	/// <summary>Sets exact kin both ways: Child on a→b gives Parent on b→a; Sibling and Spouse mirror.</summary>
	public void SetKin(string a, string b, BondFlags kin)
	{
		BondFlags mirror;
		switch (kin)
		{
			case BondFlags.Child: mirror = BondFlags.Parent; break;
			case BondFlags.Parent: mirror = BondFlags.Child; break;
			case BondFlags.Sibling:
			case BondFlags.Spouse: mirror = kin; break;
			default: throw new ArgumentException($"SetKin takes exactly one of Parent, Child, Sibling or Spouse, not {kin}.");
		}
		if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b) || a == b)
			throw new ArgumentException("SetKin needs two different people.");

		Change(a, b, s => { s.Bonds |= kin; return s; });
		Change(b, a, s => { s.Bonds |= mirror; return s; });
	}

	public BondFlags KinOf(string holder, string other) => GetStored(holder, other).Bonds & BondMasks.Kin;

	public bool IsCloseKin(string holder, string other) => KinOf(holder, other) != BondFlags.None;

	private void Change(string from, string to, Func<StoredRelationship, StoredRelationship> change)
	{
		if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
			return;
		StoredRelationship s = change(GetStored(from, to));
		if (s.IsDefault)
			entries.Remove((from, to));
		else
			entries[(from, to)] = s;
	}

	private static void RefuseKin(BondFlags flags)
	{
		if ((flags & BondMasks.Kin) != 0)
			throw new ArgumentException($"Kin flags ({flags & BondMasks.Kin}) must be set with SetKin so both sides match.");
	}
}
