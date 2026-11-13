using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The game's faction setup, loaded from Resources/Factions/FactionRelations. Names the player's
/// faction, maps the old Faction enum numbers to assets, and lists explicit pair relations.
/// </summary>
[CreateAssetMenu(menuName = "Kanshilike/Factions/Faction Relations", fileName = "FactionRelations")]
public class FactionRelationSet : ScriptableObject
{
	[Serializable]
	public class Entry
	{
		public FactionDefinition a;
		public FactionDefinition b;
		public FactionRelation relation = FactionRelation.Neutral;
	}

	[Tooltip("The faction squad members belong to.")]
	public FactionDefinition player;

	[Tooltip("Old Faction enum value -> asset, for scenes and prefabs saved before factions were assets. Index 0 = Player, 1 = Wildlife.")]
	public List<FactionDefinition> legacyEnumOrder = new List<FactionDefinition>();

	[Tooltip("Known factions, for lookups and the debug view. A faction not listed here still works with its default stance.")]
	public List<FactionDefinition> factions = new List<FactionDefinition>();

	[Tooltip("Pairs that do not follow the default stances. Symmetric.")]
	public List<Entry> relations = new List<Entry>();

	public FactionRelationTable BuildTable()
	{
		var table = new FactionRelationTable();
		DefineAll(table, player);
		foreach (FactionDefinition f in legacyEnumOrder)
			DefineAll(table, f);
		foreach (FactionDefinition f in factions)
			DefineAll(table, f);
		foreach (Entry e in relations)
		{
			if (e == null || e.a == null || e.b == null)
				continue;
			DefineAll(table, e.a);
			DefineAll(table, e.b);
		}
		foreach (Entry e in relations)
		{
			if (e != null && e.a != null && e.b != null)
				table.Set(e.a.FactionId, e.b.FactionId, e.relation);
		}
		return table;
	}

	/// <summary>Every definition this asset names, without duplicates.</summary>
	public IEnumerable<FactionDefinition> AllDefinitions()
	{
		var seen = new HashSet<FactionDefinition>();
		if (player != null && seen.Add(player)) yield return player;
		foreach (FactionDefinition f in legacyEnumOrder)
			if (f != null && seen.Add(f)) yield return f;
		foreach (FactionDefinition f in factions)
			if (f != null && seen.Add(f)) yield return f;
		foreach (Entry e in relations)
		{
			if (e == null) continue;
			if (e.a != null && seen.Add(e.a)) yield return e.a;
			if (e.b != null && seen.Add(e.b)) yield return e.b;
		}
	}

	private static void DefineAll(FactionRelationTable table, FactionDefinition f)
	{
		if (f != null)
			table.Define(f.FactionId, f.DefaultStance);
	}
}
