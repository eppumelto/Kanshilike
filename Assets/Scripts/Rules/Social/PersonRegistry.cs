using System;
using System.Collections.Generic;

/// <summary>Serialized by number: only append.</summary>
public enum PersonState
{
	Alive = 0,
	Dead = 1,
	/// <summary>Destroyed without dying (despawned).</summary>
	Gone = 2
}

/// <summary>Serialized by number: only append.</summary>
public enum Sex
{
	Male = 0,
	Female = 1
}

/// <summary>
/// One person as the social rules see them. Memberships stay after death, so bonds to the dead
/// can still be worked out. Any membership id may be null.
/// </summary>
[Serializable]
public class PersonRecord
{
	public string Id;
	public string Name;
	public Sex Sex;
	public FactionId OwnFaction;
	public string TribeId;
	public string CommunityId;
	public string KinGroupId;
	public string HouseholdId;
	/// <summary>"player" for the player's squad, else null.</summary>
	public string SquadId;
	public float Vengefulness;
	public PersonState State;

	public bool IsAlive => State == PersonState.Alive;
	public override string ToString() => string.IsNullOrEmpty(Name) ? Id : Name;
}

public sealed class PersonRegistry
{
	private readonly Dictionary<string, PersonRecord> people = new Dictionary<string, PersonRecord>(StringComparer.Ordinal);
	private readonly List<PersonRecord> ordered = new List<PersonRecord>();

	/// <summary>Both non-empty and equal (ordinal). Every membership comparison uses this.</summary>
	public static bool SameId(string a, string b) =>
		!string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && string.Equals(a, b, StringComparison.Ordinal);

	public int Count => ordered.Count;
	public IReadOnlyList<PersonRecord> All => ordered;

	public bool Add(PersonRecord record)
	{
		if (record == null || string.IsNullOrEmpty(record.Id) || people.ContainsKey(record.Id))
			return false;
		people.Add(record.Id, record);
		ordered.Add(record);
		return true;
	}

	public bool TryGet(string id, out PersonRecord record)
	{
		record = null;
		return !string.IsNullOrEmpty(id) && people.TryGetValue(id, out record);
	}

	public PersonRecord Get(string id) => TryGet(id, out PersonRecord r) ? r : null;

	public bool IsAlive(string id) => TryGet(id, out PersonRecord r) && r.IsAlive;

	public void SetState(string id, PersonState state)
	{
		if (TryGet(id, out PersonRecord r))
			r.State = state;
	}

	public void SetSquad(string id, string squadId)
	{
		if (TryGet(id, out PersonRecord r))
			r.SquadId = string.IsNullOrEmpty(squadId) ? null : squadId;
	}

	public void SetCommunity(string id, string communityId)
	{
		if (TryGet(id, out PersonRecord r))
			r.CommunityId = string.IsNullOrEmpty(communityId) ? null : communityId;
	}

	public void SetOwnFaction(string id, FactionId faction)
	{
		if (TryGet(id, out PersonRecord r))
			r.OwnFaction = faction;
	}

	public List<PersonRecord> MembersOf(string communityId) => Alive(r => SameId(r.CommunityId, communityId));
	public List<PersonRecord> KinGroupOf(string kinGroupId) => Alive(r => SameId(r.KinGroupId, kinGroupId));
	public List<PersonRecord> HouseholdOf(string householdId) => Alive(r => SameId(r.HouseholdId, householdId));

	private List<PersonRecord> Alive(Func<PersonRecord, bool> match)
	{
		var list = new List<PersonRecord>();
		foreach (PersonRecord r in ordered)
			if (r.IsAlive && match(r))
				list.Add(r);
		return list;
	}
}
