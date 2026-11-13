using System;
using System.Collections.Generic;

/// <summary>Serialized by number: never renumber.</summary>
public enum CommunityKind
{
	Village = 0,
	Camp = 1
}

/// <summary>A village or camp: holds the faction for its members and has an elder.</summary>
[Serializable]
public class CommunityRecord
{
	public string Id;
	public string Name;
	public CommunityKind Kind;
	public FactionId Faction;
	public string ElderId;

	public CommunityRecord Clone() => (CommunityRecord)MemberwiseClone();
	public override string ToString() => string.IsNullOrEmpty(Name) ? Id : Name;
}

public sealed class CommunityRegistry
{
	private readonly Dictionary<string, CommunityRecord> communities = new Dictionary<string, CommunityRecord>(StringComparer.Ordinal);
	private readonly List<CommunityRecord> ordered = new List<CommunityRecord>();

	public IReadOnlyList<CommunityRecord> All => ordered;

	public bool Add(CommunityRecord record)
	{
		if (record == null || string.IsNullOrEmpty(record.Id) || communities.ContainsKey(record.Id))
			return false;
		communities.Add(record.Id, record);
		ordered.Add(record);
		return true;
	}

	public bool TryGet(string id, out CommunityRecord record)
	{
		record = null;
		return !string.IsNullOrEmpty(id) && communities.TryGetValue(id, out record);
	}

	public CommunityRecord Get(string id) => TryGet(id, out CommunityRecord r) ? r : null;

	public bool SetFaction(string id, FactionId faction)
	{
		if (!TryGet(id, out CommunityRecord r))
			return false;
		r.Faction = faction;
		return true;
	}

	public void SetElder(string id, string personId)
	{
		if (TryGet(id, out CommunityRecord r))
			r.ElderId = string.IsNullOrEmpty(personId) ? null : personId;
	}

	/// <summary>Clears every community this person is the elder of. Returns their ids.</summary>
	public List<string> ClearElder(string personId)
	{
		var cleared = new List<string>();
		foreach (CommunityRecord r in ordered)
			if (PersonRegistry.SameId(r.ElderId, personId))
			{
				r.ElderId = null;
				cleared.Add(r.Id);
			}
		return cleared;
	}
}

/// <summary>A small cultural bias between strangers of different tribes. Directional.</summary>
public sealed class TribeOpinionTable
{
	private readonly Dictionary<(string, string), int> opinions = new Dictionary<(string, string), int>();

	public void Set(string fromTribeId, string toTribeId, int opinion)
	{
		if (string.IsNullOrEmpty(fromTribeId) || string.IsNullOrEmpty(toTribeId))
			return;
		opinions[(fromTribeId, toTribeId)] = opinion;
	}

	/// <summary>0 when either id is empty, the tribes are the same, or nothing is set.</summary>
	public int Get(string fromTribeId, string toTribeId)
	{
		if (string.IsNullOrEmpty(fromTribeId) || string.IsNullOrEmpty(toTribeId)
			|| string.Equals(fromTribeId, toTribeId, StringComparison.Ordinal))
			return 0;
		return opinions.TryGetValue((fromTribeId, toTribeId), out int v) ? v : 0;
	}
}
