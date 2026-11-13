using System;
using System.Collections.Generic;

/// <summary>One recent damager of a victim. AttackerId is null for animals.</summary>
public struct DamageEntry
{
	public string AttackerId;
	public FactionId Faction;
	public double Time;
}

/// <summary>Who hurt whom recently, so a death with no direct source can still be blamed.</summary>
public sealed class DamageLog
{
	public const int MaxPerVictim = 6;

	private readonly Dictionary<string, List<DamageEntry>> log = new Dictionary<string, List<DamageEntry>>(StringComparer.Ordinal);

	/// <summary>A repeat damager only updates its time. Keeps the 6 most recent distinct damagers.</summary>
	public void Record(string victimId, string attackerId, FactionId attackerFaction, double now)
	{
		if (string.IsNullOrEmpty(victimId))
			return;
		if (!log.TryGetValue(victimId, out List<DamageEntry> list))
			log[victimId] = list = new List<DamageEntry>();

		for (int i = 0; i < list.Count; i++)
		{
			bool same = attackerId != null
				? string.Equals(list[i].AttackerId, attackerId, StringComparison.Ordinal)
				: list[i].AttackerId == null && list[i].Faction == attackerFaction;
			if (same)
			{
				list.RemoveAt(i);
				break;
			}
		}

		list.Add(new DamageEntry { AttackerId = attackerId, Faction = attackerFaction, Time = now });
		if (list.Count > MaxPerVictim)
			list.RemoveAt(0);
	}

	/// <summary>The most recent damager within the window.</summary>
	public bool TryLastAttacker(string victimId, double now, double window, out DamageEntry entry)
	{
		entry = default;
		if (string.IsNullOrEmpty(victimId) || !log.TryGetValue(victimId, out List<DamageEntry> list) || list.Count == 0)
			return false;
		DamageEntry last = list[list.Count - 1];
		if (now - last.Time > window + 1e-9)
			return false;
		entry = last;
		return true;
	}

	/// <summary>Person ids (no animals) who damaged the victim within the window.</summary>
	public List<string> RecentPersonAttackers(string victimId, double now, double window)
	{
		var ids = new List<string>();
		if (string.IsNullOrEmpty(victimId) || !log.TryGetValue(victimId, out List<DamageEntry> list))
			return ids;
		foreach (DamageEntry e in list)
			if (e.AttackerId != null && now - e.Time <= window + 1e-9)
				ids.Add(e.AttackerId);
		return ids;
	}

	public void Clear(string victimId)
	{
		if (!string.IsNullOrEmpty(victimId))
			log.Remove(victimId);
	}
}

/// <summary>A grudge a dead holder leaves for their children to take over.</summary>
[Serializable]
public struct InheritedGrudge
{
	public string TargetId;
	public string VictimId;
	public int DeathIndex;
	public float Intensity;
	public int Generation;
}

/// <summary>One death, as remembered by everyone.</summary>
[Serializable]
public class DeathRecord
{
	public int Index;
	public string VictimId;
	/// <summary>Null when no person killed them (an animal, or unknown).</summary>
	public string KillerId;
	public bool KillerFactionKnown;
	public FactionId KillerFaction;
	/// <summary>Someone outside the killer's side saw it. With no witness, nobody ever learns the killer.</summary>
	public bool KillerWitnessed;
	public bool VictimWasElder;
	public string ElderOfCommunityId;
	public int Day;
	public float Hour;
	public WorldPoint Position;
	public int FeudGeneration;
	public int GrudgesCreated;
	public List<InheritedGrudge> InheritableGrudges = new List<InheritedGrudge>();
}

public sealed class DeathLedger
{
	private readonly List<DeathRecord> records = new List<DeathRecord>();

	public IReadOnlyList<DeathRecord> All => records;

	public DeathRecord Add(DeathRecord record)
	{
		record.Index = records.Count;
		records.Add(record);
		return record;
	}

	public DeathRecord Get(int index) => index >= 0 && index < records.Count ? records[index] : null;

	/// <summary>The latest death of this person, or null.</summary>
	public DeathRecord LatestOf(string victimId)
	{
		for (int i = records.Count - 1; i >= 0; i--)
			if (string.Equals(records[i].VictimId, victimId, StringComparison.Ordinal))
				return records[i];
		return null;
	}
}

/// <summary>News of a death on its way to someone.</summary>
public struct PendingNews
{
	public string PersonId;
	public int DeathIndex;
	public double DueHours;
}

/// <summary>Who knows of which death, and whether they know the killer.</summary>
public sealed class KnowledgeBook
{
	private const byte KnowsDeathBit = 1;
	private const byte KnowsKillerBit = 2;

	private readonly Dictionary<(string, int), byte> known = new Dictionary<(string, int), byte>();
	private readonly Dictionary<string, List<int>> byPerson = new Dictionary<string, List<int>>(StringComparer.Ordinal);
	private readonly List<PendingNews> pending = new List<PendingNews>();

	public IReadOnlyList<PendingNews> Pending => pending;

	/// <summary>Returns true when the death was newly learned. killerNewlyKnown is true the first time the killer becomes known.</summary>
	public bool Learn(string personId, int deathIndex, bool knowsKiller, out bool killerNewlyKnown)
	{
		killerNewlyKnown = false;
		if (string.IsNullOrEmpty(personId))
			return false;

		known.TryGetValue((personId, deathIndex), out byte bits);
		bool newDeath = (bits & KnowsDeathBit) == 0;
		if (knowsKiller && (bits & KnowsKillerBit) == 0)
		{
			killerNewlyKnown = true;
			bits |= KnowsKillerBit;
		}
		bits |= KnowsDeathBit;
		known[(personId, deathIndex)] = bits;

		if (newDeath)
		{
			if (!byPerson.TryGetValue(personId, out List<int> list))
				byPerson[personId] = list = new List<int>();
			list.Add(deathIndex);
		}
		return newDeath;
	}

	public bool Knows(string personId, int deathIndex) =>
		!string.IsNullOrEmpty(personId) && known.TryGetValue((personId, deathIndex), out byte b) && (b & KnowsDeathBit) != 0;

	public bool KnowsKiller(string personId, int deathIndex) =>
		!string.IsNullOrEmpty(personId) && known.TryGetValue((personId, deathIndex), out byte b) && (b & KnowsKillerBit) != 0;

	public IReadOnlyList<int> KnownBy(string personId) =>
		!string.IsNullOrEmpty(personId) && byPerson.TryGetValue(personId, out List<int> list) ? list : (IReadOnlyList<int>)Array.Empty<int>();

	public void Schedule(PendingNews news)
	{
		int i = pending.Count;
		while (i > 0 && pending[i - 1].DueHours > news.DueHours)
			i--;
		pending.Insert(i, news);
	}

	/// <summary>Moves every item due by now into the list, in due order.</summary>
	public void TakeDue(double now, List<PendingNews> into)
	{
		int n = 0;
		while (n < pending.Count && pending[n].DueHours <= now + 1e-6)
			n++;
		for (int i = 0; i < n; i++)
			into.Add(pending[i]);
		pending.RemoveRange(0, n);
	}
}
