using System.Collections.Generic;
using UnityEngine;

/// <summary>Faction assets: unique ids, the Resources relations asset and its entries.</summary>
public class FactionSetupCheck : ISetupCheck
{
	public string Name => "Factions";

	public void Run(SetupContext ctx, List<SetupIssue> issues)
	{
		var ids = new Dictionary<string, FactionDefinition>();
		foreach (FactionDefinition f in ctx.FindAssets<FactionDefinition>())
		{
			if (f == null)
				continue;
			if (ids.TryGetValue(f.Id, out FactionDefinition other))
				issues.Add(new SetupIssue(SetupSeverity.Error, $"Factions '{other.name}' and '{f.name}' share the id '{f.Id}'.", f));
			else
				ids[f.Id] = f;
		}

		List<FactionRelationSet> sets = ctx.FindAssets<FactionRelationSet>();
		FactionRelationSet set = Resources.Load<FactionRelationSet>("Factions/FactionRelations");
		if (set == null)
		{
			issues.Add(new SetupIssue(SetupSeverity.Error, "Missing Resources/Factions/FactionRelations.asset: every faction would be neutral to the rest."));
			return;
		}
		if (!sets.Contains(set))
			sets.Add(set);

		foreach (FactionRelationSet s in sets)
		{
			if (s.player == null)
				issues.Add(new SetupIssue(SetupSeverity.Error, $"{s.name}: no player faction set.", s));
			if (s.legacyEnumOrder == null || s.legacyEnumOrder.Count < 2 || s.legacyEnumOrder[0] == null || s.legacyEnumOrder[1] == null)
				issues.Add(new SetupIssue(SetupSeverity.Error, $"{s.name}: legacy order needs Player and Wildlife.", s));

			var pairs = new Dictionary<(FactionDefinition, FactionDefinition), FactionRelation>();
			foreach (FactionRelationSet.Entry e in s.relations)
			{
				if (e == null || e.a == null || e.b == null)
				{
					issues.Add(new SetupIssue(SetupSeverity.Error, $"{s.name}: a relation entry has an empty side.", s));
					continue;
				}
				var key = e.a.GetInstanceID() <= e.b.GetInstanceID() ? (e.a, e.b) : (e.b, e.a);
				if (pairs.TryGetValue(key, out FactionRelation earlier) && earlier != e.relation)
					issues.Add(new SetupIssue(SetupSeverity.Error, $"{s.name}: {e.a.name} and {e.b.name} are listed twice with different relations.", s));
				pairs[key] = e.relation;
			}
		}
	}
}

/// <summary>Characters and animals still using the hidden legacy faction value.</summary>
public class ActorFactionCheck : ISetupCheck
{
	public string Name => "Actor factions";

	public void Run(SetupContext ctx, List<SetupIssue> issues)
	{
		foreach (Character c in ctx.FindInScene<Character>())
			if (c.UsesLegacyFaction)
				issues.Add(new SetupIssue(SetupSeverity.Warning, $"{c.name}: no faction asset set (legacy value in use). Run Kanshilike > Factions > Migrate Legacy Faction Fields.", c));
		foreach (AnimalAI a in ctx.FindInScene<AnimalAI>())
			if (a.UsesLegacyFaction)
				issues.Add(new SetupIssue(SetupSeverity.Warning, $"{a.name}: no faction asset set (legacy value in use). Run Kanshilike > Factions > Migrate Legacy Faction Fields.", a));
	}
}
