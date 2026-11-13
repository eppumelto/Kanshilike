using System.Collections.Generic;

/// <summary>NPC brains: archetype, schedule entries, and whether their anchors exist.</summary>
public class NpcBrainCheck : ISetupCheck
{
	public string Name => "NPC brains";

	public void Run(SetupContext ctx, List<SetupIssue> issues)
	{
		List<NpcAnchor> anchors = ctx.FindInScene<NpcAnchor>();

		foreach (NpcBrain brain in ctx.FindInScene<NpcBrain>())
		{
			NpcArchetype archetype = brain.Archetype;
			if (archetype == null)
			{
				issues.Add(new SetupIssue(SetupSeverity.Error, $"{brain.name}: NpcBrain has no archetype.", brain));
				continue;
			}
			if (archetype.schedule == null)
			{
				issues.Add(new SetupIssue(SetupSeverity.Warning, $"{brain.name}: archetype '{archetype.name}' has no schedule; it will wander at home.", archetype));
				continue;
			}

			List<NpcSchedule.Entry> entries = archetype.schedule.entries;
			for (int i = 0; i < entries.Count; i++)
			{
				NpcSchedule.Entry entry = entries[i];
				string where = $"{brain.name}: schedule '{archetype.schedule.name}' entry {i} ({(entry != null ? entry.fromHour : 0):0.#}:00)";
				if (entry == null || entry.activity == null)
				{
					issues.Add(new SetupIssue(SetupSeverity.Error, $"{where} has no activity.", archetype.schedule));
					continue;
				}

				NpcActivity activity = entry.activity;
				if (activity is FollowActivity && brain.Leader == null)
					issues.Add(new SetupIssue(SetupSeverity.Warning, $"{where} is Follow, but the brain has no leader; it will wander at home.", brain));

				if (!activity.usesAnchor)
					continue;
				if (entry.anchorTag == null)
				{
					issues.Add(new SetupIssue(SetupSeverity.Warning, $"{where}: '{activity.name}' uses an anchor but has no tag; it runs at home.", archetype.schedule));
					continue;
				}

				NpcAnchor anchor = FindAnchor(brain, entry.anchorTag, anchors);
				if (anchor == null)
				{
					issues.Add(new SetupIssue(SetupSeverity.Warning, $"{where}: no anchor tagged '{entry.anchorTag.DisplayName}' in the scene; it will fall back.", brain));
					continue;
				}
				if (anchor.GetWaypoints().Count < activity.minWaypoints)
					issues.Add(new SetupIssue(SetupSeverity.Warning, $"{where}: anchor '{anchor.name}' has {anchor.GetWaypoints().Count} waypoint(s); '{activity.name}' needs {activity.minWaypoints}.", anchor));
			}
		}
	}

	// Same order as the brain: assigned anchors, then any anchor with the tag (distance is ignored here).
	private static NpcAnchor FindAnchor(NpcBrain brain, NpcAnchorTag tag, List<NpcAnchor> anchors)
	{
		foreach (NpcAnchor a in brain.AssignedAnchors)
			if (a != null && a.anchorTag == tag)
				return a;
		foreach (NpcAnchor a in anchors)
			if (a != null && a.anchorTag == tag)
				return a;
		return null;
	}
}

public class NpcAnchorCheck : ISetupCheck
{
	public string Name => "NPC anchors";

	public void Run(SetupContext ctx, List<SetupIssue> issues)
	{
		foreach (NpcAnchor anchor in ctx.FindInScene<NpcAnchor>())
			if (anchor.anchorTag == null)
				issues.Add(new SetupIssue(SetupSeverity.Error, $"{anchor.name}: NpcAnchor has no tag; no schedule can find it.", anchor));
	}
}

/// <summary>Components that don't belong together on an NPC.</summary>
public class NpcComponentCheck : ISetupCheck
{
	public string Name => "NPC components";

	public void Run(SetupContext ctx, List<SetupIssue> issues)
	{
		foreach (NpcBrain brain in ctx.FindInScene<NpcBrain>())
		{
			if (brain.GetComponent<PlayerControllable>() != null)
				issues.Add(new SetupIssue(SetupSeverity.Warning, $"{brain.name}: has both NpcBrain and PlayerControllable; squad members should not have a brain.", brain));
			if (brain.GetComponent<CombatEntityInteractable>() != null)
				issues.Add(new SetupIssue(SetupSeverity.Warning, $"{brain.name}: CombatEntityInteractable is for animals; NPCs get NpcInteractable.", brain));
		}
	}
}
