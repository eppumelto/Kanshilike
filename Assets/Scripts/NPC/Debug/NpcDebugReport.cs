using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A readable snapshot of one character for the NPC Debugger: sections of text lines. Other systems
/// add sections by registering a provider, so the window never changes.
/// </summary>
public class NpcDebugReport
{
	public class Section
	{
		public string Title;
		public readonly List<string> Lines = new List<string>();
		public void Add(string line) => Lines.Add(line);
	}

	public readonly List<Section> Sections = new List<Section>();

	/// <summary>Fills a report for a character. Faction, Attitude and Brain are built in.</summary>
	public static readonly List<Action<Character, NpcDebugReport>> Providers = new List<Action<Character, NpcDebugReport>>
	{
		AddFaction,
		AddAttitude,
		AddBrain
	};

	public Section Add(string title)
	{
		var section = new Section { Title = title };
		Sections.Add(section);
		return section;
	}

	public Section Find(string title) => Sections.Find(s => s.Title == title);

	public static NpcDebugReport Build(Character character)
	{
		var report = new NpcDebugReport();
		if (character == null)
			return report;

		foreach (Action<Character, NpcDebugReport> provider in Providers.ToArray())
		{
			try
			{
				provider?.Invoke(character, report);
			}
			catch (Exception e)
			{
				report.Add("Error").Add($"{provider?.Method.Name}: {e.Message}");
			}
		}
		return report;
	}

	private static void AddFaction(Character c, NpcDebugReport report)
	{
		Section s = report.Add("Faction");
		s.Add("Effective: " + CharacterFaction.Explain(c));
		FactionDefinition own = c.OwnFaction;
		s.Add("Own: " + (own != null ? own.DisplayName : "none") + (c.UsesLegacyFaction ? " (legacy field in use)" : ""));
	}

	private static void AddAttitude(Character c, NpcDebugReport report)
	{
		Section s = report.Add("Attitude");
		int count = 0;
		foreach (PlayerControllable member in PlayerControllable.All)
		{
			if (member == null || member.Character == null || member.Character == c || !member.IsAlive)
				continue;
			AttitudeResult r = Attitude.Explain(c, member.Character);
			s.Add($"Toward {Name(member.Character)}: {r.Relation} ({r.Reason}){(r.RefusesDealings ? ", refuses dealings" : "")}");
			count++;
		}
		if (count == 0)
			s.Add("No other living squad members.");
	}

	private static void AddBrain(Character c, NpcDebugReport report)
	{
		NpcBrain brain = c.GetComponent<NpcBrain>();
		if (brain == null)
			return;

		Section s = report.Add("Brain");
		if (!brain.enabled)
			s.Add("Disabled.");
		s.Add("Archetype: " + (brain.Archetype != null ? brain.Archetype.name : "none"));

		NpcBrainDiagnostics d = brain.Diagnostics;
		CharacterOrder current = c.CurrentOrder;
		string role = brain.IsRoleOrder(current) ? $" from role {d.IssuedRole.GetType().Name}" : "";
		s.Add($"Order: {(current != null ? current.GetType().Name : "none")} ({d.CurrentPriority}){role}");
		s.Add($"Hour: {(d.HasClock ? d.Hour.ToString("0.0") : "no clock")}");

		if (d.EntryIndex >= 0)
		{
			string anchor = d.Anchor != null ? d.Anchor.name : d.Placement == ActivityPlacement.Normal ? "none needed" : "not found, fallback " + d.Placement;
			s.Add($"Schedule entry {d.EntryIndex}: {(d.Activity != null ? d.Activity.name : "no activity")}" +
				$"{(d.AnchorTag != null ? " at " + d.AnchorTag.DisplayName : "")}, anchor: {anchor}");
		}
		else
		{
			s.Add("Schedule entry: none");
		}

		s.Add($"Home: {brain.HomePosition}");
		s.Add(d.Threat != null ? $"Threat: {Name(d.Threat)}, {d.ThreatSecondsLeft:0.0} s left" : "Threat: none");
		s.Add("In combat: " + (brain.InCombat ? "yes" : "no"));
	}

	private static string Name(CombatActor actor)
	{
		if (actor is Character ch && !string.IsNullOrEmpty(ch.characterName))
			return ch.characterName;
		return actor != null ? actor.name : "none";
	}
}
