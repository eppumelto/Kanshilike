using System.Collections.Generic;
using System.Linq;

/// <summary>Adds memberships, relationships, grudges and known deaths to the NPC Debugger report.</summary>
public static class SocialDebugProvider
{
	private const int MaxRelationships = 8;

	public static void EnsureRegistered()
	{
		if (!NpcDebugReport.Providers.Contains(Add))
			NpcDebugReport.Providers.Add(Add);
	}

	private static void Add(Character c, NpcDebugReport report)
	{
		SocialSimulation sim = Social.Sim;
		if (!Social.Active || sim == null)
		{
			report.Add("Social").Add("Social system inactive (no SocialWorld).");
			return;
		}
		if (!sim.Registry.TryGet(c.PersonId, out PersonRecord me))
		{
			report.Add("Social").Add($"Not registered (person id '{c.PersonId}').");
			return;
		}
		SocialTuning t = sim.Tuning;

		NpcDebugReport.Section m = report.Add("Memberships");
		CommunityRecord community = sim.Communities.Get(me.CommunityId);
		m.Add($"Person {me.Name} ({me.Sex}), {me.State}, vengefulness {me.Vengefulness:0.##}, id {me.Id}");
		m.Add("Tribe: " + Or(me.TribeId));
		m.Add("Community: " + (community != null
			? $"{community.Name} ({community.Kind}, elder {NameOf(sim, community.ElderId)}, faction {FactionRelations.DisplayName(community.Faction)})"
			: Or(me.CommunityId) + (string.IsNullOrEmpty(me.CommunityId) ? "" : " (not registered)")));
		m.Add("Kin group: " + Or(me.KinGroupId) + ", household: " + Or(me.HouseholdId) + ", squad: " + Or(me.SquadId));

		NpcDebugReport.Section r = report.Add("Relationships");
		var rows = new List<(PersonRecord other, Relationship rel, float closeness)>();
		foreach (PersonRecord other in sim.Registry.All)
		{
			if (other.Id == me.Id)
				continue;
			Relationship rel = sim.Relationship(me.Id, other.Id);
			if (rel.Bonds == BondFlags.None && rel.Opinion == 0)
				continue;
			rows.Add((other, rel, GrudgeRules.Closeness(rel, t)));
		}
		foreach (var row in rows.OrderByDescending(x => x.closeness).ThenByDescending(x => System.Math.Abs(x.rel.Opinion)).Take(MaxRelationships))
		{
			StoredRelationship stored = sim.Relations.GetStored(me.Id, row.other.Id);
			string kin = KinTerms.Term(sim.Relations.KinOf(me.Id, row.other.Id), row.other.Sex);
			r.Add($"{row.other.Name}{(row.other.IsAlive ? "" : " (" + row.other.State + ")")}: {row.rel.Bonds}, opinion {row.rel.Opinion} " +
				$"(earned {stored.Delta:+0;-0;0}), closeness {row.closeness:0.##}{(kin != null ? ", close kin: " + kin : "")}");
		}
		if (rows.Count == 0)
			r.Add("None.");

		NpcDebugReport.Section g = report.Add("Grudges");
		List<Grudge> held = sim.Grudges.HeldBy(me.Id);
		foreach (Grudge gr in held)
			g.Add($"Against {NameOf(sim, gr.TargetId)}: {gr.Intensity:0.#} {GrudgeRules.Tier(gr.Intensity, t)} " +
				$"(for {NameOf(sim, gr.VictimId)}, gen {gr.Generation}{(gr.Inherited ? ", inherited" : "")}, " +
				$"{GrudgeRules.DaysUntilForgotten(gr.Intensity, t)} day(s) left{(gr.HasLastKnown ? ", last seen " + gr.LastKnown : "")})");
		if (held.Count == 0)
			g.Add("Holds none.");
		foreach (Grudge gr in sim.Grudges.Against(me.Id))
			g.Add($"Held against them by {NameOf(sim, gr.HolderId)}: {gr.Intensity:0.#} {GrudgeRules.Tier(gr.Intensity, t)}");

		NpcDebugReport.Section d = report.Add("Deaths known");
		IReadOnlyList<int> known = sim.Knowledge.KnownBy(me.Id);
		foreach (int index in known)
		{
			DeathRecord death = sim.Deaths.Get(index);
			if (death == null)
				continue;
			string killer = sim.Knowledge.KnowsKiller(me.Id, index) ? NameOf(sim, death.KillerId) : "unknown";
			d.Add($"{NameOf(sim, death.VictimId)}, killed by {killer}, day {death.Day} {death.Hour:0.0}h, {(death.KillerWitnessed ? "witnessed" : "no witness")}");
		}
		if (known.Count == 0)
			d.Add("None.");

		NpcDebugReport.Section a = report.Add("Feelings toward the squad");
		int count = 0;
		foreach (PersonRecord other in sim.Registry.All)
		{
			if (other.Id == me.Id || other.SquadId == null || !other.IsAlive)
				continue;
			float grudge = sim.EffectiveGrudge(me.Id, other.Id, out Grudge source);
			Relationship rel = sim.Relationship(me.Id, other.Id);
			string grudgeText = source != null
				? $"{GrudgeRules.Tier(grudge, t)} grudge {grudge:0} ({NameOf(sim, source.VictimId)})"
				: "no grudge";
			a.Add($"{other.Name}: opinion {rel.Opinion}, {grudgeText}");
			count++;
		}
		if (count == 0)
			a.Add("No living squad members.");
	}

	private static string NameOf(SocialSimulation sim, string id)
	{
		if (string.IsNullOrEmpty(id))
			return "none";
		PersonRecord r = sim.Registry.Get(id);
		return r != null ? r.Name : id;
	}

	private static string Or(string s) => string.IsNullOrEmpty(s) ? "none" : s;
}
